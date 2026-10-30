// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Nodes;
using RaceDock.FeedServer.Recordings;
using RaceDock.FeedServer.Streams;

namespace RaceDock.FeedServer.Protocols;

/// <summary>Emulates Griiip Insights' session discovery and live bootstrap endpoints for WEC.</summary>
internal sealed class GriiipFeedEndpoint : IFeedEndpoint
{
    private readonly IReadOnlyList<GriiipFrame> _frames;
    private readonly PlaybackCursor _cursor;

    public GriiipFeedEndpoint(string dumpPath, IReadOnlyList<RecordedEntry> entries, double speed, bool loop)
    {
        DumpPath = dumpPath;
        _frames = Index(entries);
        if (!_frames.Any(frame => frame.Bootstrap))
            throw new InvalidDataException($"'{Path.GetFileName(dumpPath)}' has no WEC bootstrap frames.");
        _cursor = new(entries, speed, loop);
    }

    public string Id => EmulatedFeed.Wec;
    public string Championship => EmulatedFeed.Championships[Id];
    public string DumpPath { get; }
    public int FrameCount => _frames.Count;

    public void Map(WebApplication app)
    {
        app.MapGet("/meta/sessions", () => Body(bootstrap: false));
        app.MapGet("/api/v2/public/live/session/{sessionId}/bootstrap", (string sessionId) => Body(bootstrap: true));
    }

    public void Start(CancellationToken cancellationToken) => _cursor.Start();

    private IResult Body(bool bootstrap)
    {
        // A looped recording can restart between a discovery poll and the bootstrap that follows it, so the
        // newest recorded body stands in until the new cycle reaches one again.
        var frame = NewestAt(bootstrap, _cursor.Position) ?? NewestAt(bootstrap, int.MaxValue);
        if (frame is not null) return Results.Text(frame.Payload, "application/json");
        // Discovery without a catalogue is an empty list; a bootstrap without a live session is not found.
        return bootstrap ? Results.NotFound() : Results.Text("[]", "application/json");
    }

    private GriiipFrame? NewestAt(bool bootstrap, int cursor)
    {
        for (var index = _frames.Count - 1; index >= 0; index--)
        {
            var frame = _frames[index];
            if (frame.Index <= cursor && frame.Bootstrap == bootstrap) return frame;
        }
        return null;
    }

    private static IReadOnlyList<GriiipFrame> Index(IReadOnlyList<RecordedEntry> entries)
    {
        var frames = new List<GriiipFrame>();
        for (var index = 0; index < entries.Count; index++)
        {
            JsonNode? node;
            try { node = JsonNode.Parse(entries[index].Payload); }
            catch (JsonException) { continue; }
            var bootstrap = node switch
            {
                JsonArray => false,
                JsonObject => true,
                _ => (bool?)null
            };
            if (bootstrap is null) continue;
            frames.Add(new(index, bootstrap.Value, entries[index].Payload));
        }
        return frames;
    }

    private sealed record GriiipFrame(int Index, bool Bootstrap, string Payload);
}
