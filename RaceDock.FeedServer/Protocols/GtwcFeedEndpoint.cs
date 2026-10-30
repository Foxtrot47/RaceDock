// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Nodes;
using RaceDock.FeedServer.Recordings;
using RaceDock.FeedServer.Streams;

namespace RaceDock.FeedServer.Protocols;

internal enum GtwcBodyKind { Unknown, Seasons, Season, Schedule, Timing, Detail }

/// <summary>
/// Emulates the Swiss Timing cache GTWC reads. Each request returns the newest recorded body of its own kind,
/// which is how the real cache behaves: seasons, schedule, timing, and detail have independent lifetimes.
/// </summary>
internal sealed class GtwcFeedEndpoint : IFeedEndpoint
{
    private readonly IReadOnlyList<GtwcFrame> _frames;
    private readonly PlaybackCursor _cursor;

    public GtwcFeedEndpoint(string dumpPath, IReadOnlyList<RecordedEntry> entries, double speed, bool loop)
    {
        DumpPath = dumpPath;
        _frames = Index(entries);
        if (!_frames.Any(frame => frame.Kind == GtwcBodyKind.Timing))
            throw new InvalidDataException($"'{Path.GetFileName(dumpPath)}' has no GTWC timing frames.");
        _cursor = new(entries, speed, loop);
    }

    public string Id => EmulatedFeed.Gtwc;
    public string Championship => EmulatedFeed.Championships[Id];
    public string DumpPath { get; }
    public int FrameCount => _frames.Count;

    public void Map(WebApplication app) => app.MapGet("/node/db/RAC_PROD/{key}", Serve);

    public void Start(CancellationToken cancellationToken) => _cursor.Start();

    private IResult Serve(string key)
    {
        var bare = key.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? key[..^".json".Length] : key;
        var kind = KindFor(bare);
        if (kind == GtwcBodyKind.Unknown) return Results.NotFound();
        var payload = PayloadFor(kind);
        return payload is null ? Results.NotFound() : Results.Text(payload, "application/json");
    }

    private static GtwcBodyKind KindFor(string key) =>
        key.Equals("SRO_SEASONS_JSON", StringComparison.OrdinalIgnoreCase) ? GtwcBodyKind.Seasons
        : key.Contains("_COMP_DETAIL_", StringComparison.OrdinalIgnoreCase) ? GtwcBodyKind.Detail
        : key.Contains("_TIMING_", StringComparison.OrdinalIgnoreCase) ? GtwcBodyKind.Timing
        : key.Contains("_SCHEDULE_", StringComparison.OrdinalIgnoreCase) ? GtwcBodyKind.Schedule
        : key.Contains("_SEASON_JSON", StringComparison.OrdinalIgnoreCase) ? GtwcBodyKind.Season
        : GtwcBodyKind.Unknown;

    /// <summary>
    /// Detail is answered for the session the newest timing frame belongs to, so a recording that crosses a
    /// session boundary never pairs a new classification with the previous session's competitor list.
    /// </summary>
    /// <remarks>
    /// When a looped recording has only just restarted, nothing of the requested kind is at the cursor yet.
    /// The real caches keep serving their last entry, so the newest recorded body stands in rather than letting
    /// a poll fail and send the adapter into a long backoff.
    /// </remarks>
    private string? PayloadFor(GtwcBodyKind kind)
    {
        var cursor = _cursor.Position;
        var unitId = kind == GtwcBodyKind.Detail ? LatestUnitId(cursor) : null;
        var frame = NewestAt(kind, unitId, cursor)
            ?? NewestAt(kind, kind == GtwcBodyKind.Detail ? LatestUnitId(int.MaxValue) : null, int.MaxValue);
        return frame?.Payload;
    }

    private GtwcFrame? NewestAt(GtwcBodyKind kind, string? unitId, int cursor)
    {
        for (var index = _frames.Count - 1; index >= 0; index--)
        {
            var frame = _frames[index];
            if (frame.Index > cursor || frame.Kind != kind) continue;
            if (unitId is not null && !string.Equals(frame.UnitId, unitId, StringComparison.OrdinalIgnoreCase)) continue;
            return frame;
        }
        return null;
    }

    private string? LatestUnitId(int cursor)
    {
        for (var index = _frames.Count - 1; index >= 0; index--)
            if (_frames[index].Index <= cursor && _frames[index].Kind == GtwcBodyKind.Timing)
                return _frames[index].UnitId;
        return null;
    }

    private static IReadOnlyList<GtwcFrame> Index(IReadOnlyList<RecordedEntry> entries)
    {
        var frames = new List<GtwcFrame>();
        for (var index = 0; index < entries.Count; index++)
        {
            JsonObject? full;
            try { full = (JsonNode.Parse(entries[index].Payload) as JsonObject)?["content"]?["full"] as JsonObject; }
            catch (JsonException) { continue; }
            if (full is null) continue;
            var kind = KindOf(full);
            if (kind == GtwcBodyKind.Unknown) continue;
            frames.Add(new(index, kind, Text(full["UnitId"]), entries[index].Payload));
        }
        return frames;
    }

    private static GtwcBodyKind KindOf(JsonObject full) =>
        full.ContainsKey("Results") ? GtwcBodyKind.Timing
        : full.ContainsKey("Competitors") ? GtwcBodyKind.Detail
        : full.ContainsKey("Units") ? GtwcBodyKind.Schedule
        : full.ContainsKey("Meetings") ? GtwcBodyKind.Season
        : full.ContainsKey("AllSeasons") || full.ContainsKey("CurrentSeason") ? GtwcBodyKind.Seasons
        : GtwcBodyKind.Unknown;

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private sealed record GtwcFrame(int Index, GtwcBodyKind Kind, string? UnitId, string Payload);
}
