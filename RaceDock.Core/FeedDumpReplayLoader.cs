// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>Detects supported raw-feed formats without changing their provider-specific validation.</summary>
public static class FeedDumpReplayLoader
{
    internal const long MaximumInputBytes = 1024L * 1024 * 1024;
    private const int MaximumLineCharacters = 8 * 1024 * 1024;

    public static async Task<ITimingArchiveSource> CreateAsync(string path, IClock clock, CancellationToken cancellationToken = default)
    {
        try
        {
            var format = await Task.Run(() => Detect(path, cancellationToken), cancellationToken);
            return format switch
            {
                FeedFormat.Gtwc => await GtwcFeedDumpReplayLoader.CreateAsync(path, clock, cancellationToken),
                FeedFormat.Wec => await WecFeedDumpReplayLoader.CreateAsync(path, clock, cancellationToken),
                FeedFormat.Nascar => await NascarFeedDumpReplayLoader.CreateAsync(path, clock, cancellationToken),
                FeedFormat.Fia => await FiaFeedDumpReplayLoader.CreateAsync(path, clock, cancellationToken),
                _ => await F1FeedDumpReplaySource.CreateAsync(path, clock, cancellationToken)
            };
        }
        catch (JsonException ex) { throw new InvalidDataException("The feed dump contains malformed payload JSON.", ex); }
    }

    private enum FeedFormat { F1, Gtwc, Wec, Nascar, Fia }

    /// <summary>The client methods the FIA F2, F3, and F1 Academy SignalR hub calls; Formula 1 uses others.</summary>
    private static readonly HashSet<string> FiaMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "datafeed", "statsfeed", "weatherfeed", "sessionfeed", "trackfeed", "timefeed", "racedetailsfeed", "commentaryfeed"
    };

    private static FeedFormat Detect(string path, CancellationToken cancellationToken)
    {
        var sawObject = false;
        foreach (var entry in Read(path, cancellationToken).Take(64))
        {
            if (string.IsNullOrWhiteSpace(entry.Payload)) continue;
            using var document = JsonDocument.Parse(entry.Payload.Split('\u001e', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "null");
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                if (!root.EnumerateArray().Any(IsWecSession)) continue;
                return FeedFormat.Wec;
            }
            if (root.ValueKind != JsonValueKind.Object) continue;
            sawObject = true;
            if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object &&
                content.TryGetProperty("full", out var full) && full.ValueKind == JsonValueKind.Object)
                return FeedFormat.Gtwc;
            if (root.TryGetProperty("sessionInfo", out var info) && info.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("participants", out var participants) && participants.ValueKind == JsonValueKind.Array)
                return FeedFormat.Wec;
            if (root.TryGetProperty("vehicles", out var vehicles) && vehicles.ValueKind == JsonValueKind.Array &&
                root.TryGetProperty("series_id", out _))
                return FeedFormat.Nascar;
            if (IsFiaFrame(root)) return FeedFormat.Fia;
            // SignalR keep-alives, handshakes, and invocation acknowledgements name no provider; read on.
            if (IsSignalREnvelope(root)) continue;
            // F1 uses record separators within its SignalR payload. Its own loader validates these.
            return FeedFormat.F1;
        }
        if (sawObject) return FeedFormat.F1;
        throw new InvalidDataException("No supported timing records were found in this feed dump.");
    }

    private static bool IsFiaFrame(JsonElement root)
    {
        if (root.TryGetProperty("R", out var response) && response.ValueKind == JsonValueKind.Object &&
            (response.TryGetProperty("racedetailsfeed", out _) || response.TryGetProperty("sessionfeed", out _)))
            return true;
        return root.TryGetProperty("M", out var messages) && messages.ValueKind == JsonValueKind.Array &&
            messages.EnumerateArray().Any(message => message.ValueKind == JsonValueKind.Object &&
                message.TryGetProperty("M", out var method) && method.ValueKind == JsonValueKind.String &&
                FiaMethods.Contains(method.GetString()!));
    }

    /// <summary>An ASP.NET SignalR frame that carries no hub data: <c>{}</c>, <c>{"C":..,"M":[]}</c>, <c>{"I":"0"}</c>.</summary>
    private static bool IsSignalREnvelope(JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name is "C" or "S" or "I" or "G" or "L") continue;
            if (property.Name == "M" && property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() == 0) continue;
            return false;
        }
        return true;
    }

    private static bool IsWecSession(JsonElement session) =>
        session.ValueKind == JsonValueKind.Object && session.TryGetProperty("series", out var series) &&
        series.ValueKind == JsonValueKind.Object && series.TryGetProperty("id", out var id) && id.TryGetInt32(out var value) &&
        value == GriiipSeries.Wec.ProviderId;

    internal static IEnumerable<FeedDumpEntry> Read(string path, CancellationToken cancellationToken)
    {
        if (!string.Equals(Path.GetExtension(path), ".ndjson", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select a RaceDock .ndjson feed dump.");
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        if (reader.BaseStream.Length > MaximumInputBytes)
            throw new InvalidDataException("The feed dump exceeds the 1 GiB input limit.");
        var buffer = new char[64 * 1024];
        var line = new StringBuilder();
        int length;
        while ((length = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = 0;
            for (var index = 0; index < length; index++)
            {
                if (buffer[index] != '\n') continue;
                Append(line, buffer, start, index - start);
                if (!string.IsNullOrWhiteSpace(line.ToString())) yield return Parse(line.ToString());
                line.Clear();
                start = index + 1;
            }
            Append(line, buffer, start, length - start);
        }
        if (!string.IsNullOrWhiteSpace(line.ToString())) yield return Parse(line.ToString());
    }

    private static void Append(StringBuilder line, char[] buffer, int start, int count)
    {
        if ((long)line.Length + count > MaximumLineCharacters)
            throw new InvalidDataException("A feed-dump record exceeds the 8 MiB character limit.");
        line.Append(buffer, start, count);
    }

    private static FeedDumpEntry Parse(string line)
    {
        try
        {
            var entry = JsonSerializer.Deserialize<FeedDumpEntry>(line);
            if (entry.Timestamp == default || entry.Payload is null)
                throw new InvalidDataException("A feed-dump record is missing its timestamp or payload.");
            return entry;
        }
        catch (JsonException ex) { throw new InvalidDataException("The feed dump contains malformed NDJSON.", ex); }
    }
}

/// <summary>Streams Swiss Timing captures into compact, validated GTWC snapshots for the existing replay transport.</summary>
internal static class GtwcFeedDumpReplayLoader
{
    private const int MaximumFrames = 250_000;
    private const int MaximumCompetitors = 500;
    private const int MaximumStandingSamples = 2_000_000;

    public static Task<TimingArchiveReplaySource> CreateAsync(string path, IClock clock, CancellationToken token) =>
        Task.Run(() => Load(Path.GetFullPath(path), clock, token), token);

    private static TimingArchiveReplaySource Load(string path, IClock clock, CancellationToken token)
    {
        var frames = new SnapshotBuilder(ReadMetadata(path, token));
        foreach (var entry in FeedDumpReplayLoader.Read(path, token)) frames.Accept(entry);
        frames.Flush();
        if (frames.Snapshots.Count == 0)
            throw new InvalidDataException("The GTWC dump contains no matching timing and competitor-detail records.");
        var first = frames.Snapshots[0];
        return new(path, $"GTWC · {first.Meeting} · {first.Session}", clock, frames.Snapshots, frames.Timestamps, "GTWC");
    }

    private sealed class SnapshotBuilder(Metadata metadata)
    {
        public List<TimingSnapshot> Snapshots { get; } = [];
        public List<DateTimeOffset> Timestamps { get; } = [];
        private JsonObject? _timing;
        private JsonObject? _detail;
        private DateTimeOffset _timestamp;
        private int _samples;

        public void Accept(FeedDumpEntry entry)
        {
            var full = Full(entry.Payload);
            if (full.ContainsKey("Results"))
            {
                Flush();
                _timing = full;
                _timestamp = entry.Timestamp;
                if (JsonSupport.String(_detail?["UnitId"]) != JsonSupport.String(_timing["UnitId"])) _detail = null;
            }
            else if (full.ContainsKey("Competitors"))
            {
                _detail = full;
                Flush();
            }
        }

        public void Flush()
        {
            if (_timing is null || _detail is null) return;
            var id = JsonSupport.String(_timing["UnitId"]) ?? "";
            if (!string.Equals(id, JsonSupport.String(_detail["UnitId"]), StringComparison.OrdinalIgnoreCase)) return;
            if (!metadata.Units.TryGetValue(id, out var unit))
                throw new InvalidDataException("The GTWC dump has timing for a session without championship schedule metadata.");
            ValidateFrame(_timing, _detail, Snapshots.Count);
            var snapshot = GtwcTiming.BuildSnapshot(unit.Meeting, unit.Unit, _timing, _detail, _timestamp, ConnectionState.Connected);
            _samples += snapshot.Competitors.Count;
            if (_samples > MaximumStandingSamples) throw new InvalidDataException("The GTWC dump exceeds the 2,000,000-standing sample limit.");
            Snapshots.Add(snapshot);
            Timestamps.Add(_timestamp);
            _timing = null;
        }
    }

    private static void ValidateFrame(JsonObject timing, JsonObject detail, int count)
    {
        if ((timing["Results"] as JsonObject)?.Count > MaximumCompetitors ||
            (detail["Competitors"] as JsonObject)?.Count > MaximumCompetitors)
            throw new InvalidDataException("The GTWC dump exceeds the 500-competitor limit.");
        if (count >= MaximumFrames) throw new InvalidDataException("The GTWC dump exceeds the 250,000-frame limit.");
    }

    private sealed record UnitMetadata(GtwcMeeting Meeting, GtwcUnit Unit);
    private sealed record Metadata(Dictionary<string, UnitMetadata> Units);

    private static Metadata ReadMetadata(string path, CancellationToken token)
    {
        Dictionary<string, GtwcMeeting> meetings = new(StringComparer.OrdinalIgnoreCase);
        List<JsonObject> schedules = [];
        var records = 0;
        foreach (var entry in FeedDumpReplayLoader.Read(path, token))
        {
            if (++records > 1_000_000) throw new InvalidDataException("The GTWC dump exceeds the 1,000,000-record limit.");
            var full = Full(entry.Payload);
            if (full["Meetings"] is JsonObject values)
                foreach (var (id, node) in values)
                {
                    if (node is not JsonObject meeting) continue;
                    var name = GtwcTiming.MeetingName(meeting) ?? "GT World Challenge Europe";
                    meetings[id] = new(id, name, name, 1);
                    if (meetings.Count > 500) throw new InvalidDataException("The GTWC dump exceeds the 500-meeting limit.");
                }
            if (full.ContainsKey("Units"))
            {
                // Only the latest copy of each meeting's timetable is needed for identity.
                var id = JsonSupport.String(full["MeetingId"]);
                schedules.RemoveAll(schedule => JsonSupport.String(schedule["MeetingId"]) == id);
                schedules.Add(full);
                if (schedules.Count > 500) throw new InvalidDataException("The GTWC dump exceeds the 500-schedule limit.");
            }
        }
        Dictionary<string, UnitMetadata> units = new(StringComparer.OrdinalIgnoreCase);
        foreach (var schedule in schedules)
        {
            var id = JsonSupport.String(schedule["MeetingId"]) ?? "";
            if (!meetings.TryGetValue(id, out var meeting)) continue;
            var competitions = GtwcTiming.GtwcEuropeCompetitionIds(schedule);
            foreach (var (unitId, node) in schedule["Units"] as JsonObject ?? [])
            {
                if (node is not JsonObject unit) continue;
                var competition = JsonSupport.String(unit["CompetitionId"]) ?? "";
                if (!competitions.Contains(competition)) continue;
                units[unitId] = new(meeting, new(unitId,
                    JsonSupport.String(unit["Name"]) ?? JsonSupport.String(unit["Code"]) ?? "Recorded session", competition, 1));
                if (units.Count > 10_000) throw new InvalidDataException("The GTWC dump exceeds the 10,000-session limit.");
            }
        }
        if (units.Count == 0) throw new InvalidDataException("The dump has no GTWC championship meeting and session metadata.");
        return new(units);
    }

    private static JsonObject Full(string payload)
    {
        try
        {
            var root = JsonNode.Parse(payload) as JsonObject;
            if (root?["content"] is JsonObject content && content["full"] is JsonObject full) return full;
            throw new InvalidDataException("The GTWC dump contains an unsupported response shape.");
        }
        catch (JsonException ex) { throw new InvalidDataException("The GTWC dump contains malformed payload JSON.", ex); }
    }
}

/// <summary>Streams Griiip WEC captures into compact snapshots through the shared provider mapping.</summary>
internal static class WecFeedDumpReplayLoader
{
    private const int MaximumRecords = 1_000_000;
    private const int MaximumFrames = 250_000;
    private const int MaximumCompetitors = 500;
    private const int MaximumStandingSamples = 2_000_000;
    private const int MaximumSessions = 10_000;

    public static Task<TimingArchiveReplaySource> CreateAsync(string path, IClock clock, CancellationToken token) =>
        Task.Run(() => Load(Path.GetFullPath(path), clock, token), token);

    private static TimingArchiveReplaySource Load(string path, IClock clock, CancellationToken token)
    {
        var builder = new SnapshotBuilder();
        foreach (var entry in FeedDumpReplayLoader.Read(path, token)) builder.Accept(entry);
        builder.Complete();
        var first = builder.Snapshots[0];
        return new(path, $"WEC · {first.Meeting} · {first.Session}", clock,
            builder.Snapshots, builder.Timestamps, "WEC");
    }

    private sealed class SnapshotBuilder
    {
        public List<TimingSnapshot> Snapshots { get; } = [];
        public List<DateTimeOffset> Timestamps { get; } = [];
        private readonly Dictionary<long, JsonObject> _sessions = [];
        private readonly Dictionary<string, long> _sessionsByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly GriiipSectorTiming _sectors = new();
        private int _records;
        private int _samples;

        public void Accept(FeedDumpEntry entry)
        {
            if (++_records > MaximumRecords) throw new InvalidDataException("The WEC dump exceeds the 1,000,000-record limit.");
            var root = Parse(entry.Payload);
            if (root is JsonArray sessions) AcceptSessions(sessions);
            else if (root is JsonObject bootstrap) AcceptBootstrap(entry.Timestamp, bootstrap);
        }

        private void AcceptSessions(JsonArray sessions)
        {
            foreach (var node in sessions.OfType<JsonObject>())
            {
                if (JsonSupport.Int((node["series"] as JsonObject)?["id"]) != GriiipSeries.Wec.ProviderId) continue;
                var id = JsonSupport.Int(node["id"]);
                if (id is null) continue;
                _sessions[id.Value] = node;
                if (SessionKey(node) is { } key) _sessionsByName[key] = id.Value;
                if (_sessions.Count > MaximumSessions) throw new InvalidDataException("The WEC dump exceeds the 10,000-session limit.");
            }
        }

        private void AcceptBootstrap(DateTimeOffset timestamp, JsonObject bootstrap)
        {
            if (bootstrap["sessionInfo"] is not JsonObject info || bootstrap["participants"] is not JsonArray participants)
                throw new InvalidDataException("The WEC dump contains an unsupported bootstrap shape.");
            if (Snapshots.Count >= MaximumFrames) throw new InvalidDataException("The WEC dump exceeds the 250,000-frame limit.");
            var session = Session(info);
            var ended = JsonSupport.Bool(info["hasSeenChequered"]) || bootstrap["chequeredFlags"] is JsonArray { Count: > 0 };
            if (ended) session["hasSeenChequered"] = true;
            var snapshot = GriiipTiming.BuildSnapshot(session, bootstrap, timestamp, ConnectionState.Connected,
                GriiipSeries.Wec, _sectors);
            if (snapshot.Competitors.Count > MaximumCompetitors) throw new InvalidDataException("The WEC dump exceeds the 500-competitor limit.");
            _samples += snapshot.Competitors.Count;
            if (_samples > MaximumStandingSamples) throw new InvalidDataException("The WEC dump exceeds the 2,000,000-standing sample limit.");
            Snapshots.Add(snapshot);
            Timestamps.Add(timestamp);
        }

        private JsonObject Session(JsonObject info)
        {
            var key = SessionKey(info);
            if (key is not null && _sessionsByName.TryGetValue(key, out var id) && _sessions.TryGetValue(id, out var found))
                return found.DeepClone().AsObject();
            var eventId = JsonSupport.Int(info["eventId"]);
            var name = JsonSupport.String(info["sessionName"]);
            var matches = _sessions.Values
                .Where(value => JsonSupport.Int(value["eventID"]) == eventId && string.Equals(
                    JsonSupport.String(value["name"]), name, StringComparison.OrdinalIgnoreCase))
                .Take(2).ToList();
            return matches.Count == 1
                ? matches[0].DeepClone().AsObject()
                : throw new InvalidDataException("The WEC dump contains timing without matching session discovery metadata.");
        }

        private static string? SessionKey(JsonObject session)
        {
            var eventId = JsonSupport.Int(session["eventID"]) ?? JsonSupport.Int(session["eventId"]);
            var name = JsonSupport.String(session["name"]) ?? JsonSupport.String(session["sessionName"]);
            return eventId is null || string.IsNullOrWhiteSpace(name) ? null : $"{eventId}|{name}";
        }

        public void Complete()
        {
            if (Snapshots.Count == 0) throw new InvalidDataException("The WEC dump contains no usable timing bootstraps.");
        }

        private static JsonNode Parse(string payload)
        {
            try
            {
                return JsonNode.Parse(payload) ?? throw new InvalidDataException("The WEC dump has an empty payload.");
            }
            catch (JsonException ex) { throw new InvalidDataException("The WEC dump contains malformed payload JSON.", ex); }
        }
    }
}

/// <summary>
/// Replays a NASCAR live-feed dump through the live mapping. The endpoint re-serves one assembly until it builds
/// the next, so a payload with the same series and <c>time_of_day_os</c> as the one before it is not a new frame.
/// Between sessions it answers with an empty field, which has nothing to show and is skipped.
/// </summary>
internal static class NascarFeedDumpReplayLoader
{
    private const int MaximumRecords = 1_000_000;
    private const int MaximumFrames = 250_000;
    private const int MaximumCompetitors = 500;
    private const int MaximumStandingSamples = 5_000_000;
    private static readonly NascarSeries[] Series = [NascarSeries.Cup, NascarSeries.Xfinity, NascarSeries.Trucks];

    public static Task<TimingArchiveReplaySource> CreateAsync(string path, IClock clock, CancellationToken token) =>
        Task.Run(() => Load(Path.GetFullPath(path), clock, token), token);

    private static TimingArchiveReplaySource Load(string path, IClock clock, CancellationToken token)
    {
        List<TimingSnapshot> snapshots = [];
        List<DateTimeOffset> timestamps = [];
        string? previous = null;
        var records = 0;
        var samples = 0;
        foreach (var entry in FeedDumpReplayLoader.Read(path, token))
        {
            if (++records > MaximumRecords) throw new InvalidDataException("The NASCAR dump exceeds the 1,000,000-record limit.");
            if (Parse(entry.Payload) is not { } payload) continue;
            var series = Series.FirstOrDefault(value => NascarTiming.IsSeries(payload, value));
            if (series is null || payload["vehicles"] is not JsonArray { Count: > 0 } vehicles) continue;
            if (vehicles.Count > MaximumCompetitors) throw new InvalidDataException("The NASCAR dump exceeds the 500-competitor limit.");
            var assembly = $"{series.SeriesId}|{JsonSupport.String(payload["time_of_day_os"])}";
            if (assembly == previous) continue;
            previous = assembly;
            if (snapshots.Count >= MaximumFrames) throw new InvalidDataException("The NASCAR dump exceeds the 250,000-frame limit.");
            var snapshot = NascarTiming.BuildSnapshot(payload, entry.Timestamp, ConnectionState.Connected, series);
            samples += snapshot.Competitors.Count;
            if (samples > MaximumStandingSamples) throw new InvalidDataException("The NASCAR dump exceeds the 5,000,000-standing sample limit.");
            snapshots.Add(snapshot);
            timestamps.Add(entry.Timestamp);
        }
        if (snapshots.Count == 0) throw new InvalidDataException("The NASCAR dump contains no session with a field.");
        var first = snapshots[0];
        return new(path, $"NASCAR · {first.Meeting} · {first.Session}", clock, snapshots, timestamps, "NASCAR");
    }

    private static JsonObject? Parse(string payload)
    {
        try { return JsonNode.Parse(payload) as JsonObject; }
        catch (JsonException ex) { throw new InvalidDataException("The NASCAR dump contains malformed payload JSON.", ex); }
    }
}

/// <summary>
/// Replays an FIA Formula 2, Formula 3, or F1 Academy SignalR dump through the live hub parser, so the replay
/// keeps every rule the live source applies. The series comes from the recording itself and, failing that,
/// from the dump's file name.
/// </summary>
internal static class FiaFeedDumpReplayLoader
{
    private const int MaximumRecords = 1_000_000;
    private const int MaximumFrames = 250_000;
    private const int MaximumCompetitors = 500;
    private const int MaximumStandingSamples = 5_000_000;

    public static Task<TimingArchiveReplaySource> CreateAsync(string path, IClock clock, CancellationToken token) =>
        Task.Run(() => Load(Path.GetFullPath(path), clock, token), token);

    private static TimingArchiveReplaySource Load(string path, IClock clock, CancellationToken token)
    {
        var series = SeriesOf(path, token);
        var recorded = new RecordedClock();
        var source = new F2LiveTimingSource(recorded, new AlertArbiter(recorded), series);
        List<TimingSnapshot> snapshots = [];
        List<DateTimeOffset> timestamps = [];
        var samples = 0;
        source.SnapshotReceived += snapshot =>
        {
            if (snapshot.Competitors.Count > MaximumCompetitors)
                throw new InvalidDataException($"The {series} dump exceeds the 500-competitor limit.");
            if (snapshots.Count >= MaximumFrames) throw new InvalidDataException($"The {series} dump exceeds the 250,000-frame limit.");
            samples += snapshot.Competitors.Count;
            if (samples > MaximumStandingSamples)
                throw new InvalidDataException($"The {series} dump exceeds the 5,000,000-standing sample limit.");
            // The recording was received over a live connection; the parser here never opens one.
            snapshots.Add(snapshot with { ConnectionState = ConnectionState.Connected });
            timestamps.Add(recorded.UtcNow);
        };
        var records = 0;
        foreach (var entry in FeedDumpReplayLoader.Read(path, token))
        {
            if (++records > MaximumRecords) throw new InvalidDataException($"The {series} dump exceeds the 1,000,000-record limit.");
            if (string.IsNullOrWhiteSpace(entry.Payload)) continue;
            recorded.UtcNow = entry.Timestamp;
            try { source.ProcessMessage(entry.Payload); }
            catch (JsonException ex) { throw new InvalidDataException($"The {series} dump contains malformed payload JSON.", ex); }
        }
        if (snapshots.Count == 0) throw new InvalidDataException($"The {series} dump contains no timing lines.");
        var last = snapshots[^1];
        return new(path, $"{series} · {last.Meeting} · {last.Session}", clock, snapshots, timestamps, series);
    }

    /// <summary>The series the hub was joined for, as its initial data names it (<c>F2</c>, <c>F3</c>, <c>F1 Academy</c>).</summary>
    private static string SeriesOf(string path, CancellationToken token)
    {
        foreach (var entry in FeedDumpReplayLoader.Read(path, token).Take(1_000))
        {
            JsonNode? root;
            try { root = JsonNode.Parse(entry.Payload); }
            catch (JsonException) { continue; }
            var named = Normalize(JsonSupport.String((root?["R"]?["data"] as JsonArray)?.ElementAtOrDefault(1)?["Series"]));
            if (named is not null) return named;
        }
        return Normalize(Path.GetFileNameWithoutExtension(path)) ??
            throw new InvalidDataException("The FIA dump does not say whether it is Formula 2, Formula 3, or F1 Academy.");
    }

    private static string? Normalize(string? value) =>
        value?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant() switch
        {
            "F2" => "F2",
            "F3" => "F3",
            "F1ACADEMY" => "F1 Academy",
            _ => null
        };

    private sealed class RecordedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
