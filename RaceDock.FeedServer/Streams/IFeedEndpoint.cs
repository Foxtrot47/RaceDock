// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.FeedServer.Streams;

/// <summary>One emulated provider: the routes it answers and the recording behind them.</summary>
internal interface IFeedEndpoint
{
    string Id { get; }
    string Championship { get; }
    string DumpPath { get; }
    int FrameCount { get; }

    /// <summary>Registers every route the provider's real host exposes for this stream.</summary>
    void Map(WebApplication app);

    /// <summary>Starts playback for streams that answer polls rather than pushing frames.</summary>
    void Start(CancellationToken cancellationToken) { }
}

/// <summary>What <c>GET /streams</c> reports, so the application enables exactly what is served.</summary>
public sealed record FeedStreamInfo(string Id, string Championship, string Dump, int Frames, bool Loop);
