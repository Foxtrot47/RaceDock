// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>Replays a RaceDock Formula 1 NDJSON feed dump without opening a live connection.</summary>
public sealed class F1FeedDumpReplaySource : ITimingArchiveSource
{
    private const long MaximumInputBytes = 256L * 1024 * 1024;
    private const int MaximumFrames = 250_000;
    private const char RecordSeparator = '\u001e';

    private readonly IClock _hostClock;
    private readonly ReplayClock _recordedClock = new();
    private readonly TimingStateProcessor _processor;
    private readonly IReadOnlyList<DumpFrame> _frames;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private TimeSpan? _remainingAtUpdate;
    private DateTimeOffset _clockUpdatedAt;
    private bool _clockExtrapolating;
    private int _nextFrame;
    private int _generation;
    private double _speed = 4;
    private bool _paused = true;
    private bool _completed;
    private bool _active;
    private bool _publishing;
    private bool _disposed;

    private F1FeedDumpReplaySource(string fullPath, IClock hostClock, LoadedDump loaded)
    {
        _hostClock = hostClock;
        ArchivePath = fullPath;
        ArchiveName = loaded.Name;
        _frames = loaded.Frames;
        _processor = new TimingStateProcessor(_recordedClock, new AlertArbiter(_recordedClock));
        _processor.SnapshotChanged += ForwardSnapshot;
    }

    public static Task<F1FeedDumpReplaySource> CreateAsync(
        string path, IClock clock, CancellationToken cancellationToken = default) => Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var fullPath = Path.GetFullPath(path);
            return new F1FeedDumpReplaySource(fullPath, clock, Load(fullPath, cancellationToken));
        }, cancellationToken);

    public string ArchivePath { get; }
    public string ArchiveName { get; }
    public int FrameIndex { get { lock (_gate) return Math.Clamp(_nextFrame, 1, _frames.Count); } }
    public int FrameCount => _frames.Count;
    public bool IsPaused { get { lock (_gate) return _paused; } }
    public bool IsCompleted { get { lock (_gate) return _completed; } }
    public double Speed
    {
        get => Volatile.Read(ref _speed);
        set => Volatile.Write(ref _speed, Math.Clamp(value, .25, 20));
    }

    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived;
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_cts is not null) return Task.CompletedTask;
            ObjectDisposedException.ThrowIf(_disposed, this);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _nextFrame = 1;
            _generation++;
            _paused = true;
            _completed = _frames.Count == 1;
            _active = true;
            ResetProcessor();
            ProcessFrame(_frames[0]);
            ConnectionChanged?.Invoke(ConnectionState.Connected);
            _worker = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
        }
        return Task.CompletedTask;
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
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active) return;
            _nextFrame = 1;
            _generation++;
            _paused = true;
            _completed = _frames.Count == 1;
            ResetProcessor();
            ProcessFrame(_frames[0]);
        }
        Signal();
    }

    public void Step()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active) return;
            _paused = true;
            _generation++;
            if (_nextFrame >= _frames.Count)
            {
                _completed = true;
                return;
            }
            ProcessFrame(_frames[_nextFrame++]);
            _completed = _nextFrame >= _frames.Count;
        }
        Signal();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (IsPaused) await _wake.WaitAsync(cancellationToken);
                int index;
                int generation;
                lock (_gate) { index = _nextFrame; generation = _generation; }
                if (index >= _frames.Count)
                {
                    lock (_gate) { _completed = true; _paused = true; }
                    continue;
                }

                var rawDelay = _frames[index].Timestamp - _frames[index - 1].Timestamp;
                var replayDelay = rawDelay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(10)
                    : rawDelay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(1)
                    : rawDelay;
                if (!await DelayForArchiveTimeAsync(replayDelay, generation, cancellationToken)) continue;

                lock (_gate)
                {
                    if (generation != _generation || index != _nextFrame) continue;
                    ProcessFrame(_frames[_nextFrame++]);
                    if (_nextFrame >= _frames.Count)
                    {
                        _completed = true;
                        _paused = true;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            lock (_gate) _paused = true;
            ConnectionChanged?.Invoke(ConnectionState.Faulted);
            Failed?.Invoke(new($"F1 feed-dump replay failed: {ex.Message}", ex, _hostClock.UtcNow));
        }
    }

    private async Task<bool> DelayForArchiveTimeAsync(
        TimeSpan duration, int generation, CancellationToken cancellationToken)
    {
        var remaining = duration.TotalSeconds;
        while (remaining > 0)
        {
            if (IsPaused)
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

    private void ProcessFrame(DumpFrame frame)
    {
        _recordedClock.UtcNowValue = frame.Timestamp;
        _publishing = true;
        try
        {
            foreach (var part in frame.Payload.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                using var document = JsonDocument.Parse(part);
                var root = document.RootElement;
                if (root.TryGetProperty("type", out var type) && type.GetInt32() == 3 &&
                    root.TryGetProperty("result", out var initial) && initial.ValueKind == JsonValueKind.Object)
                {
                    var state = (JsonObject)JsonNode.Parse(initial.GetRawText())!;
                    UpdateClock(state["ExtrapolatedClock"], frame.Timestamp);
                    _processor.ProcessInitial(state);
                    continue;
                }
                if (!root.TryGetProperty("type", out type) || type.GetInt32() != 1 ||
                    !root.TryGetProperty("target", out var target) ||
                    !string.Equals(target.GetString(), "feed", StringComparison.OrdinalIgnoreCase) ||
                    !root.TryGetProperty("arguments", out var arguments) || arguments.GetArrayLength() < 2) continue;
                var topic = arguments[0].GetString() ?? "";
                var data = JsonNode.Parse(arguments[1].GetRawText());
                if (data is null) continue;
                if (topic == "ExtrapolatedClock") UpdateClock(data, frame.Timestamp);
                var delta = new TimingDelta(topic, data, frame.Timestamp);
                DeltaReceived?.Invoke(delta);
                _processor.ProcessDelta(topic, data, frame.Timestamp);
            }
        }
        finally { _publishing = false; }
    }

    private void ForwardSnapshot(TimingSnapshot snapshot)
    {
        if (!_publishing) return;
        SnapshotReceived?.Invoke(snapshot with
        {
            FreshnessTimestamp = _hostClock.UtcNow,
            ConnectionState = ConnectionState.Connected,
            TimeRemaining = CurrentTimeRemaining()
        });
    }

    private void UpdateClock(JsonNode? value, DateTimeOffset timestamp)
    {
        _remainingAtUpdate = TimeSpan.TryParse(JsonSupport.String(value?["Remaining"]), out var remaining)
            ? remaining
            : null;
        _clockUpdatedAt = timestamp;
        _clockExtrapolating = JsonSupport.Bool(value?["Extrapolating"]);
    }

    private string? CurrentTimeRemaining()
    {
        if (!_remainingAtUpdate.HasValue) return null;
        var remaining = _remainingAtUpdate.Value -
            (_clockExtrapolating ? _recordedClock.UtcNow - _clockUpdatedAt : TimeSpan.Zero);
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        return $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private void ResetProcessor()
    {
        _publishing = false;
        _remainingAtUpdate = null;
        _clockExtrapolating = false;
        _processor.ClearAlerts();
        _processor.ProcessInitial(new JsonObject());
    }

    public async Task StopAsync()
    {
        Task? worker;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            if (_cts is null) return;
            _active = false;
            _paused = true;
            _generation++;
            worker = _worker;
            cts = _cts;
            _worker = null;
            _cts = null;
        }
        await cts.CancelAsync();
        Signal();
        if (worker is not null)
            try { await worker; }
            catch (OperationCanceledException) { }
        cts.Dispose();
        ConnectionChanged?.Invoke(ConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync();
        _disposed = true;
        _processor.SnapshotChanged -= ForwardSnapshot;
        _wake.Dispose();
    }

    private void Signal()
    {
        try { _wake.Release(); }
        catch (SemaphoreFullException) { }
    }

    private static LoadedDump Load(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("F1 feed dump was not found.", path);
        if (!string.Equals(Path.GetExtension(path), ".ndjson", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select a RaceDock Formula 1 .ndjson feed dump.");
        if (new FileInfo(path).Length > MaximumInputBytes)
            throw new InvalidDataException("The feed dump exceeds the 256 MiB input limit.");

        List<DumpFrame> frames = [];
        var foundInitial = false;
        string? meeting = null;
        string? session = null;
        foreach (var line in File.ReadLines(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;
            FeedDumpEntry entry;
            try { entry = JsonSerializer.Deserialize<FeedDumpEntry>(line); }
            catch (JsonException ex) { throw new InvalidDataException("The feed dump contains malformed NDJSON.", ex); }
            if (string.IsNullOrWhiteSpace(entry.Payload)) continue;
            if (!foundInitial)
            {
                foundInitial = TryDescribeInitial(entry.Payload, out meeting, out session);
                if (!foundInitial) continue;
            }
            if (frames.Count >= MaximumFrames)
                throw new InvalidDataException($"The feed dump exceeds the {MaximumFrames:N0}-frame limit.");
            frames.Add(new(entry.Timestamp, entry.Payload));
        }
        if (!foundInitial || frames.Count == 0)
            throw new InvalidDataException("The feed dump has no complete Formula 1 initial state.");
        var name = string.Join(" · ", new[] { meeting, session }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return new(string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(path) : name, frames);
    }

    private static bool TryDescribeInitial(string payload, out string? meeting, out string? session)
    {
        meeting = null;
        session = null;
        foreach (var part in payload.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var document = JsonDocument.Parse(part);
                var root = document.RootElement;
                if (!root.TryGetProperty("type", out var type) || type.GetInt32() != 3 ||
                    !root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object) continue;
                if (result.TryGetProperty("SessionInfo", out var info))
                {
                    if (info.TryGetProperty("Name", out var name)) session = name.GetString();
                    if (info.TryGetProperty("Meeting", out var meetingNode) &&
                        meetingNode.TryGetProperty("Name", out var meetingName)) meeting = meetingName.GetString();
                }
                return true;
            }
            catch (JsonException) { }
        }
        return false;
    }

    private sealed record DumpFrame(DateTimeOffset Timestamp, string Payload);
    private sealed record LoadedDump(string Name, IReadOnlyList<DumpFrame> Frames);
    private sealed class ReplayClock : IClock
    {
        public DateTimeOffset UtcNowValue { get; set; }
        public DateTimeOffset UtcNow => UtcNowValue;
    }
}
