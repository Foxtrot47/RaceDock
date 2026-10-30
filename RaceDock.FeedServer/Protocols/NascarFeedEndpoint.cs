// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using RaceDock.FeedServer.Recordings;
using RaceDock.FeedServer.Streams;

namespace RaceDock.FeedServer.Protocols;

/// <summary>
/// Emulates the unofficial NASCAR live feed and the published race list. The race list is synthesized so the
/// feed's window check sees a race starting now; without it the scheduler would sleep until the real calendar's
/// next round even though a recording is ready to play.
/// </summary>
internal sealed class NascarFeedEndpoint : IFeedEndpoint
{
    private readonly IReadOnlyList<NascarFrame> _frames;
    private readonly PlaybackCursor _cursor;
    private readonly bool _retime;
    private readonly HashSet<int> _seriesIds;
    private readonly string _track;
    private readonly Lock _gate = new();
    private int _retimedPosition = -1;
    private string? _retimedPayload;

    public NascarFeedEndpoint(string dumpPath, IReadOnlyList<RecordedEntry> entries, double speed, bool loop, bool retime)
    {
        DumpPath = dumpPath;
        _retime = retime;
        _frames = Index(entries);
        if (_frames.Count == 0)
            throw new InvalidDataException($"'{Path.GetFileName(dumpPath)}' has no NASCAR live-feed frames.");
        _seriesIds = _frames.Select(frame => frame.SeriesId).Where(id => id > 0).ToHashSet();
        _track = _frames.FirstOrDefault(frame => !string.IsNullOrWhiteSpace(frame.Track))?.Track ?? "Recorded race";
        _cursor = new(entries, speed, loop);
    }

    public string Id => EmulatedFeed.Nascar;
    public string Championship => EmulatedFeed.Championships[Id];
    public string DumpPath { get; }
    public int FrameCount => _frames.Count;

    public void Map(WebApplication app)
    {
        app.MapGet("/live/feeds/live-feed.json", LiveFeed);
        app.MapGet("/cacher/{year:int}/{seriesId:int}/race_list_basic.json", RaceList);
        app.MapGet("/cacher/{year:int}/{seriesId:int}/{raceId:int}/weekend-feed.json", () => Results.NotFound());
    }

    public void Start(CancellationToken cancellationToken) => _cursor.Start();

    private IResult LiveFeed()
    {
        var position = _cursor.Position;
        var payload = Latest(position);
        if (payload is null) return Results.NotFound();
        if (!_retime) return Results.Text(payload, "application/json");
        lock (_gate)
        {
            if (position == _retimedPosition && _retimedPayload is not null)
                return Results.Text(_retimedPayload, "application/json");
            // The endpoint stamps one assembly per revision, so the stamp is derived from the playback position
            // rather than from each request; otherwise every poll would look like a new assembly.
            var node = JsonNode.Parse(payload) as JsonObject;
            if (node is null) return Results.Text(payload, "application/json");
            node["time_of_day_os"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            _retimedPosition = position;
            _retimedPayload = node.ToJsonString();
            return Results.Text(_retimedPayload, "application/json");
        }
    }

    private IResult RaceList(int seriesId)
    {
        // A recorded feed only ever carries one series, so any other series stays unpublished.
        if (!_seriesIds.Contains(seriesId)) return Results.NotFound();
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var races = new JsonArray
        {
            new JsonObject
            {
                ["race_id"] = 900_001,
                ["race_name"] = $"{_track} (feed server)",
                ["track_name"] = _track,
                ["series_id"] = seriesId,
                ["schedule"] = new JsonArray
                {
                    new JsonObject { ["run_type"] = 3, ["start_time_utc"] = now }
                }
            }
        };
        return Results.Text(races.ToJsonString(), "application/json");
    }

    private string? Latest(int cursor)
    {
        for (var index = _frames.Count - 1; index >= 0; index--)
            if (_frames[index].Index <= cursor)
                return _frames[index].Payload;
        // The endpoint keeps answering after a session with the field it last assembled, and a looped
        // recording has the same gap at the start of each cycle.
        return _frames.Count == 0 ? null : _frames[^1].Payload;
    }

    private static IReadOnlyList<NascarFrame> Index(IReadOnlyList<RecordedEntry> entries)
    {
        var frames = new List<NascarFrame>();
        for (var index = 0; index < entries.Count; index++)
        {
            JsonObject? payload;
            try { payload = JsonNode.Parse(entries[index].Payload) as JsonObject; }
            catch (JsonException) { continue; }
            if (payload?["vehicles"] is not JsonArray { Count: > 0 }) continue;
            frames.Add(new(index, Number(payload["series_id"]), Text(payload["track_name"]), entries[index].Payload));
        }
        return frames;
    }

    private static int Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private sealed record NascarFrame(int Index, int SeriesId, string? Track, string Payload);
}
