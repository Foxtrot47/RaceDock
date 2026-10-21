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
                _ => await F1FeedDumpReplaySource.CreateAsync(path, clock, cancellationToken)
            };
        }
        catch (JsonException ex) { throw new InvalidDataException("The feed dump contains malformed payload JSON.", ex); }
    }

    private enum FeedFormat { F1, Gtwc, Wec }

    private static FeedFormat Detect(string path, CancellationToken cancellationToken)
    {
        foreach (var entry in Read(path, cancellationToken).Take(32))
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
            if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object &&
                content.TryGetProperty("full", out var full) && full.ValueKind == JsonValueKind.Object)
                return FeedFormat.Gtwc;
            if (root.TryGetProperty("sessionInfo", out var info) && info.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("participants", out var participants) && participants.ValueKind == JsonValueKind.Array)
                return FeedFormat.Wec;
            // F1 uses record separators within its SignalR payload. Its own loader validates these.
            return FeedFormat.F1;
        }
        throw new InvalidDataException("No supported timing records were found in this feed dump.");
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
