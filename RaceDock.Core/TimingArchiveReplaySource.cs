// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

public sealed class TimingArchiveReplaySource : ITimingArchiveSource
{
    private readonly IClock _clock;
    private readonly JsonObject _manifest;
    private readonly IReadOnlyList<ArchiveFrame> _frames;
    private JsonObject? _state;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private int _nextFrame;
    private int _generation;
    private double _speed = 4;
    private volatile bool _paused;
    private volatile bool _completed;
    private bool _disposed;
    private bool _active;

    public TimingArchiveReplaySource(string path, IClock clock)
        : this(Path.GetFullPath(path), clock, TimingArchiveLoader.Load(Path.GetFullPath(path)))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
    }

    private TimingArchiveReplaySource(string fullPath, IClock clock, TimingArchiveLoader.LoadedArchive archive)
    {
        _clock = clock;
        ArchivePath = fullPath;
        ArchiveName = archive.Name;
        _manifest = archive.Manifest;
        _frames = archive.Frames;
    }

    public static Task<TimingArchiveReplaySource> CreateAsync(string path, IClock clock, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var fullPath = Path.GetFullPath(path);
            var archive = TimingArchiveLoader.Load(fullPath, cancellationToken);
            return new TimingArchiveReplaySource(fullPath, clock, archive);
        }, cancellationToken);

    public string ArchivePath { get; }
    public string ArchiveName { get; }
    public int FrameIndex { get { lock (_gate) return Math.Clamp(_nextFrame, 1, _frames.Count); } }
    public int FrameCount => _frames.Count;
    public bool IsPaused => _paused;
    public bool IsCompleted => _completed;
    public double Speed
    {
        get => Volatile.Read(ref _speed);
        set => Volatile.Write(ref _speed, Math.Clamp(value, .25, 20));
    }

    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived { add { } remove { } }
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (_cts is not null) return;
            CancellationToken runToken;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                runToken = _cts.Token;
                _nextFrame = 1;
                _generation++;
                _completed = false;
                _paused = false;
                _active = true;
                ConnectionChanged?.Invoke(ConnectionState.Connected);
                Publish(ReadFrame(0));
            }
            _worker = Task.Run(() => RunAsync(runToken), runToken);
        }
        finally { _lifecycle.Release(); }
    }

    public void Pause()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active) return;
            _paused = true;
            _generation++;
        }
    }
    public void Resume()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active || _completed) return;
            _paused = false;
        }
        Signal();
    }

    public void Restart()
    {
        _paused = true;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active) return;
            _nextFrame = 1;
            _generation++;
            _completed = false;
            Publish(ReadFrame(0));
        }
        Signal();
    }

    public void Step()
    {
        _paused = true;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active) return;
            if (_nextFrame >= _frames.Count)
            {
                _completed = true;
                return;
            }
            var frame = ReadFrame(_nextFrame++);
            _generation++;
            _completed = _nextFrame >= _frames.Count;
            Publish(frame);
        }
        Signal();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_paused) await _wake.WaitAsync(cancellationToken);
                int index;
                int generation;
                lock (_gate) { index = _nextFrame; generation = _generation; }
                if (index >= _frames.Count)
                {
                    _completed = true;
                    _paused = true;
                    continue;
                }

                var rawDelay = _frames[index].Timestamp - _frames[index - 1].Timestamp;
                var replayDelay = rawDelay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(50)
                    : rawDelay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(1)
                    : rawDelay;
                if (!await DelayForArchiveTimeAsync(replayDelay, generation, cancellationToken)) continue;

                lock (_gate)
                {
                    if (generation != _generation || index != _nextFrame) continue;
                    var frame = ReadFrame(_nextFrame++);
                    if (_nextFrame >= _frames.Count)
                    {
                        _completed = true;
                        _paused = true;
                    }
                    Publish(frame);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _paused = true;
            ConnectionChanged?.Invoke(ConnectionState.Faulted);
            Failed?.Invoke(new($"Archive replay failed: {ex.Message}", ex, _clock.UtcNow));
        }
    }

    private async Task<bool> DelayForArchiveTimeAsync(TimeSpan duration, int generation, CancellationToken cancellationToken)
    {
        var remaining = duration.TotalSeconds;
        while (remaining > 0)
        {
            if (_paused)
            {
                await _wake.WaitAsync(cancellationToken);
                continue;
            }
            lock (_gate) if (generation != _generation) return false;
            var speed = Speed;
            var wallSeconds = Math.Min(.1, remaining / speed);
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(.001, wallSeconds)), cancellationToken);
            remaining -= System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds * speed;
        }
        return true;
    }

    private TimingSnapshot ReadFrame(int index)
    {
        var frame = _frames[index];
        _state = TimingArchiveLoader.AdvanceState(_manifest, _state, frame, CancellationToken.None);
        return TimingArchiveLoader.BuildSnapshot(_manifest, _state, frame.Timestamp);
    }

    private void Publish(TimingSnapshot snapshot) => SnapshotReceived?.Invoke(snapshot with
    {
        FreshnessTimestamp = _clock.UtcNow,
        ConnectionState = ConnectionState.Connected
    });

    private void Signal()
    {
        try { _wake.Release(); }
        catch (SemaphoreFullException) { }
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync();
        try { await StopCoreAsync(); }
        finally { _lifecycle.Release(); }
    }

    private async Task StopCoreAsync()
    {
        if (_cts is null) return;
        lock (_gate)
        {
            _active = false;
            _paused = true;
            _generation++;
        }
        await _cts.CancelAsync();
        Signal();
        if (_worker is not null)
            try { await _worker; }
            catch (OperationCanceledException) { }
        _worker = null;
        _cts.Dispose();
        _cts = null;
        ConnectionChanged?.Invoke(ConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _active = false;
                _generation++;
            }
            await StopCoreAsync();
        }
        finally { _lifecycle.Release(); }
    }

    internal sealed record ArchiveFrame(DateTimeOffset Timestamp, byte[] Payload, string Source, bool IsIncrement);
}

internal static class TimingArchiveLoader
{
    private const long MaxInputBytes = 256L * 1024 * 1024;
    private const long MaxExpandedBytes = 512L * 1024 * 1024;
    private const long MaxEntryBytes = 32L * 1024 * 1024;
    private const int MaxEntries = 100_000;
    private const int MaxFrames = 20_000;
    private const int MaxArrayIndex = 10_000;
    private const int MaxCarsPerFrame = 5_000;
    private const int MaxColumns = 256;
    private const int MaxStateNodes = 250_000;
    private const int MaxStateDepth = 32;
    private const int MaxPatchOperationsPerFrame = 10_000;
    private const int ComplexityCheckInterval = 32;

    internal sealed record LoadedArchive(string Name, JsonObject Manifest, IReadOnlyList<TimingArchiveReplaySource.ArchiveFrame> Frames);
    private sealed record Payload(DateTimeOffset Timestamp, JsonObject Value, string Source, bool IsIncrement);

    public static LoadedArchive Load(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) throw new FileNotFoundException("Timing archive was not found.", path);
        var extension = Path.GetExtension(path);
        return extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ? LoadZip(path, cancellationToken)
            : extension.Equals(".har", StringComparison.OrdinalIgnoreCase) ? LoadHar(path, cancellationToken)
            : throw new InvalidDataException("Select a Timing71 .zip export or a .har archive.");
    }

    private static LoadedArchive LoadZip(string path, CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length > MaxInputBytes) throw new InvalidDataException("The archive exceeds the 256 MiB input limit.");
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count > MaxEntries) throw new InvalidDataException($"The archive exceeds the {MaxEntries:N0}-entry limit.");
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Length > MaxEntryBytes) throw new InvalidDataException($"{entry.FullName} exceeds the 32 MiB entry limit.");
            expandedBytes = checked(expandedBytes + entry.Length);
            if (expandedBytes > MaxExpandedBytes) throw new InvalidDataException("The archive exceeds the 512 MiB expanded-data limit.");
            if (entry.CompressedLength > 0 && entry.Length / (double)entry.CompressedLength > 1_000)
                throw new InvalidDataException($"{entry.FullName} has an unsafe compression ratio.");
        }
        var manifestEntry = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));
        if (manifestEntry is null) throw new InvalidDataException("The ZIP does not contain a Timing71 manifest.json file.");
        var manifest = ReadObject(manifestEntry.Open(), manifestEntry.FullName);
        ValidateManifest(manifest, recording: true);

        var frames = new List<TimingArchiveReplaySource.ArchiveFrame>();
        var frameEntries = archive.Entries
            .Where(entry => entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                !entry.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            .Select((entry, index) => (Entry: entry, Index: index, Timestamp: ParseArchiveTimestamp(entry.Name, index)))
            .OrderBy(value => value.Timestamp)
            .ThenBy(value => value.Index);
        foreach (var item in frameEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (frames.Count >= MaxFrames) throw new InvalidDataException($"The archive exceeds the {MaxFrames:N0}-frame limit.");
            var payload = ReadBytes(item.Entry);
            var incremental = Path.GetFileNameWithoutExtension(item.Entry.Name).EndsWith('i');
            frames.Add(new(item.Timestamp, payload, item.Entry.Name, incremental));
        }
        return Build(Path.GetFileName(path), manifest, frames, cancellationToken);
    }

    private static LoadedArchive LoadHar(string path, CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length > MaxInputBytes) throw new InvalidDataException("The HAR exceeds the 256 MiB input limit.");
        JsonObject root;
        using (var stream = File.OpenRead(path)) root = ReadObject(stream, path);
        var entries = root["log"]?["entries"] as JsonArray
            ?? throw new InvalidDataException("The HAR does not contain log.entries.");
        JsonObject? manifest = null;
        var frames = new List<TimingArchiveReplaySource.ArchiveFrame>();
        var sequence = 0;
        foreach (var node in entries.OfType<JsonObject>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryTime = ParseDate(node["startedDateTime"]?.GetValue<string>()) ?? DateTimeOffset.UnixEpoch.AddSeconds(sequence++);
            foreach (var candidate in HarCandidates(node, entryTime))
            {
                if (IsManifest(candidate.Value)) manifest ??= (JsonObject)candidate.Value.DeepClone();
                else if (IsState(candidate.Value))
                {
                    if (frames.Count >= MaxFrames) throw new InvalidDataException($"The HAR exceeds the {MaxFrames:N0}-frame limit.");
                    frames.Add(new(candidate.Timestamp, JsonSerializer.SerializeToUtf8Bytes(candidate.Value), candidate.Source, candidate.IsIncrement));
                }
            }
        }
        if (manifest is null)
            throw new InvalidDataException("No Timing71 manifest was found in the HAR. HAR replay currently requires Timing71 manifest and state messages.");
        ValidateManifest(manifest, recording: false);
        return Build(Path.GetFileName(path), manifest, frames.OrderBy(frame => frame.Timestamp).ToList(), cancellationToken);
    }

    private static IEnumerable<Payload> HarCandidates(JsonObject entry, DateTimeOffset entryTime)
    {
        var text = entry["response"]?["content"]?["text"]?.GetValue<string>();
        var encoding = entry["response"]?["content"]?["encoding"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase))
                try { text = Encoding.UTF8.GetString(Convert.FromBase64String(text)); }
                catch (FormatException) { text = null; }
            if (TryParseNode(text, out var node))
                foreach (var value in FindTimingObjects(node!)) yield return new(entryTime, value, "response", IsIncrementState(value));
        }

        if (entry["_webSocketMessages"] is not JsonArray messages) yield break;
        foreach (var message in messages.OfType<JsonObject>())
        {
            var timestamp = ParseHarSocketTime(message["time"], entryTime);
            var data = message["data"]?.GetValue<string>();
            if (!TryParseNode(data, out var node)) continue;
            foreach (var value in FindTimingObjects(node!)) yield return new(timestamp, value, "websocket", IsIncrementState(value));
        }
    }

    private static IEnumerable<JsonObject> FindTimingObjects(JsonNode node, int depth = 0)
    {
        if (depth > 6) yield break;
        if (node is JsonObject value)
        {
            if (IsManifest(value)) { yield return value; yield break; }
            if (IsState(value))
            {
                if (value["manifest"] is JsonObject embeddedManifest) yield return embeddedManifest;
                yield return value;
                yield break;
            }
            foreach (var child in value.Select(pair => pair.Value).OfType<JsonNode>())
                foreach (var found in FindTimingObjects(child, depth + 1)) yield return found;
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array.OfType<JsonNode>())
                foreach (var found in FindTimingObjects(child, depth + 1)) yield return found;
        }
        else if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            if (!TryParseNode(text, out var nested)) yield break;
            foreach (var found in FindTimingObjects(nested!, depth + 1)) yield return found;
        }
    }

    private static bool TryParseNode(string? text, out JsonNode? node)
    {
        node = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        try { node = JsonNode.Parse(text); return node is not null; }
        catch (JsonException) { }
        if (text.Length > 1 && text[0] == 'a')
            try { node = JsonNode.Parse(text[1..]); return node is not null; }
            catch (JsonException) { }
        return false;
    }

    private static LoadedArchive Build(string fileName, JsonObject manifest, IReadOnlyList<TimingArchiveReplaySource.ArchiveFrame> inputFrames, CancellationToken cancellationToken)
    {
        if (inputFrames.Count == 0) throw new InvalidDataException("The archive contains no Timing71 state frames.");
        JsonObject? state = null;
        var frames = new List<TimingArchiveReplaySource.ArchiveFrame>();
        foreach (var frame in inputFrames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state is null && frame.IsIncrement) continue;
            state = AdvanceState(manifest, state, frame, cancellationToken);
            frames.Add(frame);
        }
        if (frames.Count == 0) throw new InvalidDataException("The archive has no complete Timing71 state from which replay can start.");
        var title = Text(manifest["name"]) ?? fileName;
        var description = Text(manifest["description"]);
        return new(string.IsNullOrWhiteSpace(description) ? title : $"{title} — {description}", manifest, frames);
    }

    internal static JsonObject AdvanceState(JsonObject manifest, JsonObject? state, TimingArchiveReplaySource.ArchiveFrame frame, CancellationToken cancellationToken)
    {
        var value = ReadObject(frame.Payload, frame.Source);
        if (!frame.IsIncrement)
        {
            state = value;
            EnsureStateComplexity(state);
        }
        else if (state is not null) ApplyIncrement(state, value, frame.Source, cancellationToken);
        else throw new InvalidDataException("An archive increment has no preceding full state.");
        ValidateState(manifest, state);
        return state;
    }

    private static void ApplyIncrement(JsonObject state, JsonObject increment, string source, CancellationToken cancellationToken)
    {
        var applied = 0;
        void CheckBudget()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++applied > MaxPatchOperationsPerFrame)
                throw new InvalidDataException($"A frame exceeds the {MaxPatchOperationsPerFrame:N0}-operation patch limit.");
            if (applied % ComplexityCheckInterval == 0) EnsureStateComplexity(state);
        }

        foreach (var sectionName in new[] { "cars", "session" })
        {
            if (increment[sectionName] is not JsonArray operations) continue;
            var root = state[sectionName] ?? throw new InvalidDataException($"An update in {source} references a missing {sectionName} section.");
            foreach (var operationNode in operations)
            {
                if (operationNode is not JsonArray operation)
                    throw new InvalidDataException($"Malformed Timing71 operation in {source}.");
                var kind = Text(operation.ElementAtOrDefault(0));
                if (operation.Count < 3 || operation[2] is not JsonArray changes || kind is not ("change" or "add" or "remove"))
                    throw new InvalidDataException($"Unsupported Timing71 incremental operation in {source}.");
                var segments = PathSegments(operation[1]);
                if (kind == "change")
                {
                    if (segments.Count == 0 || changes.Count < 2)
                        throw new InvalidDataException($"Invalid Timing71 change operation in {source}.");
                    var parent = Navigate(root, segments.Take(segments.Count - 1), create: true);
                    Assign(parent, segments[^1], changes[1]);
                    CheckBudget();
                    continue;
                }

                var container = Navigate(root, segments, create: kind == "add");
                foreach (var changeNode in changes)
                {
                    if (changeNode is not JsonArray change)
                        throw new InvalidDataException($"Malformed Timing71 {kind} operation in {source}.");
                    if (change.Count < 1) throw new InvalidDataException($"Invalid Timing71 {kind} operation in {source}.");
                    if (kind == "add")
                    {
                        if (change.Count < 2) throw new InvalidDataException($"Invalid Timing71 add operation in {source}.");
                        Assign(container, change[0], change[1]);
                    }
                    else Remove(container, change[0], change.Count);
                    CheckBudget();
                }
            }
        }

        EnsureStateComplexity(state);
        if (increment["highlight"] is JsonArray highlight) state["highlight"] = highlight.DeepClone();
        if (increment["messages"] is JsonArray messages)
        {
            var combined = new JsonArray();
            foreach (var message in messages.Take(100)) combined.Add(message?.DeepClone());
            if (state["messages"] is JsonArray existing)
                foreach (var message in existing.Take(100 - combined.Count)) combined.Add(message?.DeepClone());
            state["messages"] = combined;
        }
        EnsureStateComplexity(state);
    }

    private static List<JsonNode?> PathSegments(JsonNode? pathNode)
    {
        if (pathNode is JsonArray path) return path.ToList();
        var text = Text(pathNode);
        if (string.IsNullOrEmpty(text)) return [];
        return text.Split('.').Select(value => (JsonNode?)JsonValue.Create(value)).ToList();
    }

    private static JsonNode Navigate(JsonNode root, IEnumerable<JsonNode?> segments, bool create)
    {
        var values = segments.ToList();
        JsonNode current = root;
        for (var i = 0; i < values.Count; i++)
        {
            var child = Child(current, values[i]);
            if (child is null && create)
            {
                child = i + 1 < values.Count && Int(values[i + 1]) is not null ? new JsonArray() : new JsonObject();
                Assign(current, values[i], child);
                child = Child(current, values[i]);
            }
            current = child ?? throw new InvalidDataException("An incremental update path does not exist.");
        }
        return current;
    }

    // Match Timing71's JavaScript patcher: array keys are assigned/deleted in place,
    // not inserted or spliced. Deletion therefore leaves a JSON null/hole.
    private static void Assign(JsonNode container, JsonNode? segment, JsonNode? value)
    {
        if (container is JsonArray array && Int(segment) is { } index && index >= 0)
        {
            if (index > MaxArrayIndex) throw new InvalidDataException($"An incremental array index exceeds the {MaxArrayIndex:N0} limit.");
            while (array.Count < index) array.Add(null);
            if (index == array.Count) array.Add(value?.DeepClone());
            else array[index] = value?.DeepClone();
        }
        else if (container is JsonObject obj && Text(segment) is { } key) obj[key] = value?.DeepClone();
        else throw new InvalidDataException("An incremental update path is invalid.");
    }

    private static void Remove(JsonNode container, JsonNode? segment, int tupleCount)
    {
        if (tupleCount < 2) throw new InvalidDataException("A Timing71 remove operation is missing its previous value.");
        if (container is JsonArray array && Int(segment) is { } index && index >= 0 && index < array.Count) array[index] = null;
        else if (container is JsonObject obj && Text(segment) is { } key) obj.Remove(key);
    }

    internal static void ValidateState(JsonObject manifest, JsonObject state)
    {
        var spec = manifest["colSpec"] as JsonArray ?? [];
        if (spec.Count > MaxColumns) throw new InvalidDataException($"The manifest exceeds the {MaxColumns}-column limit.");
        var rows = state["cars"] as JsonArray ?? [];
        if (rows.Count > MaxCarsPerFrame) throw new InvalidDataException($"A frame exceeds the {MaxCarsPerFrame:N0}-car limit.");
    }

    private static void EnsureStateComplexity(JsonNode root)
    {
        var pending = new Stack<(JsonNode Node, int Depth)>();
        pending.Push((root, 0));
        var nodes = 0;
        while (pending.Count > 0)
        {
            var (node, depth) = pending.Pop();
            if (depth > MaxStateDepth) throw new InvalidDataException($"A timing state exceeds the {MaxStateDepth}-level nesting limit.");
            if (++nodes > MaxStateNodes) throw new InvalidDataException($"A timing state exceeds the {MaxStateNodes:N0}-node limit.");
            if (node is JsonArray array)
            {
                nodes += array.Count(child => child is null);
                if (nodes > MaxStateNodes) throw new InvalidDataException($"A timing state exceeds the {MaxStateNodes:N0}-node limit.");
                foreach (var child in array.OfType<JsonNode>()) pending.Push((child, depth + 1));
            }
            else if (node is JsonObject obj)
            {
                nodes += obj.Count;
                if (nodes > MaxStateNodes) throw new InvalidDataException($"A timing state exceeds the {MaxStateNodes:N0}-node limit.");
                foreach (var child in obj.Select(pair => pair.Value).OfType<JsonNode>()) pending.Push((child, depth + 1));
            }
        }
    }

    private static JsonNode? Child(JsonNode current, JsonNode? segment) => current switch
    {
        JsonArray array when Int(segment) is { } index && index >= 0 && index < array.Count => array[index],
        JsonObject obj when Text(segment) is { } key => obj[key],
        _ => null
    };

    internal static TimingSnapshot BuildSnapshot(JsonObject manifest, JsonObject state, DateTimeOffset timestamp)
    {
        ValidateState(manifest, state);
        var spec = manifest["colSpec"] as JsonArray ?? [];
        // Columns are addressable by their short header ("T") and by their description ("Tyre"),
        // because feeds abbreviate headers inconsistently across championships.
        var columns = spec.SelectMany((node, index) => ColumnNames(node)
                .Select(name => (Name: name, Index: index)))
            .GroupBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);
        var rows = state["cars"] as JsonArray ?? [];
        var parsed = new List<(CompetitorStanding Standing, double Best)>();
        for (var position = 0; position < rows.Count; position++)
        {
            if (rows[position] is not JsonArray row) continue;
            var number = CellText(row, columns, "Num", "Number", "No") ?? (position + 1).ToString(CultureInfo.InvariantCulture);
            var rawName = CellText(row, columns, "Driver") ?? number;
            var name = DisplayName(rawName);
            var stateValue = (CellText(row, columns, "State") ?? "").ToUpperInvariant();
            var laps = CellInt(row, columns, "Laps") ?? 0;
            var best = CellDouble(row, columns, "Best") ?? 0;
            var gap = FormatGap(Cell(row, columns, "Gap"), position == 0);
            var interval = position == 0 ? null : FormatGap(Cell(row, columns, "Int", "Interval"), false);
            var retired = stateValue is "RET" or "RETIRED" or "DNF" or "DNS" or "DSQ";
            var stopped = stateValue is "STOP" or "STOPPED";
            var status = stateValue is "RUN" or "" ? null : stateValue;
            parsed.Add((new(
                number,
                position + 1,
                DriverCode(rawName, number),
                name,
                CellText(row, columns, "Team") ?? "",
                gap,
                interval,
                CellText(row, columns, "Tyre", "Tire", "Compound"),
                laps,
                stateValue == "PIT",
                retired,
                stopped,
                false,
                FormatTime(best),
                status,
                null,
                CellText(row, columns, "Class", "Category"),
                Manufacturer(CellText(row, columns, "Car", "Vehicle", "Bike")),
                FormatTime(CellDouble(row, columns, "Last", "Last lap time") ?? 0)), best));
        }
        var fastest = parsed.Where(value => value.Best > 0).OrderBy(value => value.Best).FirstOrDefault().Standing?.DriverId;
        var competitors = parsed.Select(value => value.Standing with { IsOverallFastest = value.Standing.DriverId == fastest }).ToList();
        var sessionState = state["session"] as JsonObject ?? [];
        var flag = (Text(sessionState["flagState"]) ?? "").ToLowerInvariant();
        var condition = flag switch
        {
            "green" or "clear" => TrackCondition.AllClear,
            "yellow" or "fcy" => TrackCondition.Yellow,
            "double-yellow" or "double yellow" => TrackCondition.DoubleYellow,
            "sc" or "safety-car" or "safety car" => TrackCondition.SafetyCar,
            "vsc" => TrackCondition.VirtualSafetyCar,
            "red" or "red-flag" => TrackCondition.RedFlag,
            _ => TrackCondition.Unknown
        };
        var currentLap = competitors.Select(value => value.Lap).DefaultIfEmpty(0).Max();
        var lapsRemain = Double(sessionState["lapsRemain"]);
        int? totalLaps = lapsRemain is >= 0 ? currentLap + (int)Math.Ceiling(lapsRemain.Value) : null;
        var remaining = lapsRemain is null ? FormatDuration(Double(sessionState["timeRemain"])) : null;
        SessionLifecycle lifecycle;
        if (flag == "chequered" || competitors.Count > 0 && competitors.All(value => value.StatusLabel == "FIN"))
            lifecycle = SessionLifecycle.Ended;
        else if (competitors.Count > 0)
            lifecycle = SessionLifecycle.Live;
        else
            lifecycle = SessionLifecycle.PreSession;
        var (meeting, session) = SplitDescription(Text(manifest["description"]));
        return new(
            meeting ?? Text(manifest["name"]) ?? "Archive replay",
            session ?? "Recorded session",
            "",
            currentLap,
            totalLaps,
            condition,
            competitors,
            timestamp,
            lifecycle,
            ConnectionState.Connected,
            remaining,
            ChampionshipFor(Text(manifest["name"])));
    }

    private const int HeaderPosition = 0;
    private const int DescriptionPosition = 2;

    /// <summary>Yields a column's short header and its description, skipping absent entries.</summary>
    private static IEnumerable<string> ColumnNames(JsonNode? node)
    {
        if (node is not JsonArray entry) yield break;
        foreach (var position in new[] { HeaderPosition, DescriptionPosition })
        {
            if (entry.Count <= position) continue;
            if (Text(entry[position]) is { } name && !string.IsNullOrWhiteSpace(name)) yield return name;
        }
    }

    private static JsonNode? Cell(JsonArray row, IReadOnlyDictionary<string, int> columns, params string[] names)
    {
        var index = names.Select(name => columns.TryGetValue(name, out var value) ? value : -1).FirstOrDefault(value => value >= 0, -1);
        if (index < 0 || index >= row.Count) return null;
        var node = row[index];
        return node is JsonArray values && values.Count > 0 ? values[0] : node;
    }

    private static string? CellText(JsonArray row, IReadOnlyDictionary<string, int> columns, params string[] names) => Text(Cell(row, columns, names));
    private static int? CellInt(JsonArray row, IReadOnlyDictionary<string, int> columns, params string[] names) => Int(Cell(row, columns, names));
    private static double? CellDouble(JsonArray row, IReadOnlyDictionary<string, int> columns, params string[] names) => Double(Cell(row, columns, names));

    private static string? FormatGap(JsonNode? node, bool leader)
    {
        var text = Text(node);
        if (string.IsNullOrWhiteSpace(text)) return leader ? "LEAD" : null;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return $"+{seconds:0.000}";
        return text.Equals("leader", StringComparison.OrdinalIgnoreCase) ? "LEAD" : text;
    }

    private static string? FormatTime(double seconds)
    {
        if (seconds <= 0) return null;
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}"
            : $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    private static string? FormatDuration(double? seconds)
    {
        if (seconds is null || seconds < 0) return null;
        var time = TimeSpan.FromSeconds(seconds.Value);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
    }

    private static string DisplayName(string value)
    {
        var comma = value.IndexOf(',');
        if (comma <= 0 || comma >= value.Length - 1) return value.Trim();
        var surname = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value[..comma].Trim().ToLowerInvariant());
        var given = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value[(comma + 1)..].Trim().ToLowerInvariant());
        return $"{given} {surname}";
    }

    private static string DriverCode(string name, string fallback)
    {
        var surname = name.Split(',', 2)[0].Trim();
        if (surname.Length == 0) return fallback.ToUpperInvariant();
        var code = new string(surname.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();
        return code.Length > 0 ? code : fallback.ToUpperInvariant();
    }

    private static string? Manufacturer(string? vehicle)
    {
        if (string.IsNullOrWhiteSpace(vehicle)) return null;
        var upper = vehicle.ToUpperInvariant();
        foreach (var brand in new[] { "ACURA", "ALPINE", "ASTON MARTIN", "AUDI", "BMW", "CADILLAC", "CHEVROLET", "CITROEN", "CITROËN", "DS", "FERRARI", "FORD", "GENESIS", "JAGUAR", "LAMBORGHINI", "LEXUS", "LOLA", "MAHINDRA", "MCLAREN", "MERCEDES", "NISSAN", "PEUGEOT", "PORSCHE", "TOYOTA" })
            if (upper.Contains(brand, StringComparison.Ordinal)) return brand;
        foreach (var (brand, displayName) in new[]
        {
            ("APRILIA", "Aprilia"), ("BOSCOSCURO", "Boscoscuro"), ("DUCATI", "Ducati"),
            ("HONDA", "Honda"), ("KALEX", "Kalex"), ("KTM", "KTM"), ("YAMAHA", "Yamaha")
        })
            if (upper.Contains(brand, StringComparison.Ordinal)) return displayName;
        return null;
    }

    private static Championship ChampionshipFor(string? name)
    {
        var value = name?.ToUpperInvariant() ?? "";
        if (value.Contains("MOTOGP") || value.Contains("MOTO GP")) return Championship.MotoGp;
        if (value.Contains("MOTO2") || value.Contains("MOTO 2")) return Championship.Moto2;
        if (value.Contains("MOTO3") || value.Contains("MOTO 3")) return Championship.Moto3;
        if (value.Contains("IMSA")) return Championship.Imsa;
        if (value.Contains("FIA WEC") || value.Contains("WORLD ENDURANCE CHAMPIONSHIP")) return Championship.WorldEnduranceChampionship;
        if (value.Contains("EUROPEAN LE MANS") || value.Contains("ELMS")) return Championship.EuropeanLeMansSeries;
        if (value.Contains("MICHELIN LE MANS CUP") || value.Contains("MLMC")) return Championship.MichelinLeMansCup;
        if (value.Contains("FORMULA E")) return Championship.FormulaE;
        if (value.Contains("DTM")) return Championship.Dtm;
        if (value.Contains("INDYCAR") || value.Contains("INDY CAR")) return Championship.IndyCar;
        if (value.Contains("FORMULA 1") || value.Contains("FORMULA ONE")) return Championship.Formula1;
        if (value.Contains("FORMULA 2")) return Championship.Formula2;
        if (value.Contains("FORMULA 3")) return Championship.Formula3;
        if (value.Contains("WORLD RALLY") || value.Contains(" WRC")) return Championship.WorldRallyChampionship;
        if (value.Contains("GT WORLD CHALLENGE")) return Championship.GtWorldChallengeEurope;
        return Championship.Unknown;
    }

    private static (string? Meeting, string? Session) SplitDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return (null, null);
        var separator = description.LastIndexOf(" - ", StringComparison.Ordinal);
        return separator > 0 ? (description[..separator].Trim(), description[(separator + 3)..].Trim()) : (description.Trim(), "Recorded session");
    }

    private static void ValidateManifest(JsonObject manifest, bool recording)
    {
        if (!IsManifest(manifest)) throw new InvalidDataException("The Timing71 manifest is missing name, description, or colSpec.");
        if (recording && Int(manifest["version"]) != 1)
            throw new InvalidDataException("Only Timing71 recording format version 1 is supported.");
    }

    private static bool IsManifest(JsonObject value) => value["colSpec"] is JsonArray && value["name"] is not null && value["description"] is not null;
    private static bool IsState(JsonObject value) => value["cars"] is JsonArray && value["session"] is not null;
    private static bool IsIncrementState(JsonObject value)
    {
        if (value["session"] is JsonArray) return true;
        if (value["cars"] is not JsonArray cars || cars.Count == 0) return false;
        return cars.All(node => node is JsonArray operation && Text(operation.ElementAtOrDefault(0)) is "change" or "add" or "remove");
    }

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        var payload = new byte[checked((int)entry.Length)];
        using var stream = entry.Open();
        stream.ReadExactly(payload);
        return payload;
    }

    private static JsonObject ReadObject(Stream stream, string source)
    {
        try { return JsonNode.Parse(stream) as JsonObject ?? throw new InvalidDataException($"{source} does not contain a JSON object."); }
        catch (JsonException ex) { throw new InvalidDataException($"{source} contains invalid JSON.", ex); }
    }

    private static JsonObject ReadObject(ReadOnlySpan<byte> payload, string source)
    {
        try { return JsonNode.Parse(payload) as JsonObject ?? throw new InvalidDataException($"{source} does not contain a JSON object."); }
        catch (JsonException ex) { throw new InvalidDataException($"{source} contains invalid JSON.", ex); }
    }

    private static DateTimeOffset ParseArchiveTimestamp(string name, int fallback)
    {
        var digits = new string(Path.GetFileNameWithoutExtension(name).TakeWhile(char.IsDigit).ToArray()).TrimStart('0');
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : DateTimeOffset.UnixEpoch.AddSeconds(fallback);
    }

    private static DateTimeOffset ParseHarSocketTime(JsonNode? node, DateTimeOffset fallback)
    {
        if (Double(node) is not { } value) return fallback;
        if (value > 1_000_000_000_000) return DateTimeOffset.FromUnixTimeMilliseconds((long)value);
        if (value > 1_000_000_000) return DateTimeOffset.FromUnixTimeSeconds((long)value);
        return fallback.AddSeconds(value);
    }

    private static DateTimeOffset? ParseDate(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    private static string? Text(JsonNode? node)
    {
        if (node is null) return null;
        return node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node.ToJsonString().Trim('"');
    }
    private static int? Int(JsonNode? node) => int.TryParse(Text(node), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static double? Double(JsonNode? node) => double.TryParse(Text(node), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
}
