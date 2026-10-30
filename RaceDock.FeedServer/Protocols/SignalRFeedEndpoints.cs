// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.WebSockets;
using System.Text;
using RaceDock.FeedServer.Recordings;
using RaceDock.FeedServer.Streams;

namespace RaceDock.FeedServer.Protocols;

/// <summary>Push transports shared by the Formula 1 and FIA SignalR hubs.</summary>
internal static class WebSocketPush
{
    public const char RecordSeparator = '\u001e';

    /// <summary>
    /// Streams the recorded frames on their recorded cadence. The handshake reply is sent first for Formula 1,
    /// whose client waits for <c>{}</c> before it subscribes.
    /// </summary>
    public static async Task PlayAsync(WebSocket socket, IReadOnlyList<RecordedEntry> frames, double speed,
        bool loop, bool handshake, CancellationToken cancellationToken)
    {
        try
        {
            if (handshake) await SendAsync(socket, "{}" + RecordSeparator, cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                for (var index = 0; index < frames.Count; index++)
                {
                    if (index > 0)
                        await Task.Delay(
                            RecordedDelay.Between(frames[index - 1].Timestamp, frames[index].Timestamp, speed),
                            cancellationToken);
                    await SendAsync(socket, frames[index].Payload, cancellationToken);
                }
                if (!loop) return;
                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (ObjectDisposedException) { }
    }

    private static Task SendAsync(WebSocket socket, string text, CancellationToken cancellationToken) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, cancellationToken);

    /// <summary>Consumes the client's handshake and subscriptions so its send buffer never fills.</summary>
    public static async Task DrainAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return;
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (ObjectDisposedException) { }
    }
}

/// <summary>Emulates the Formula 1 SignalR endpoint, replaying the recorded initial state and delta feed.</summary>
internal sealed class F1FeedEndpoint : IFeedEndpoint
{
    private readonly IReadOnlyList<RecordedEntry> _frames;
    private readonly double _speed;
    private readonly bool _loop;

    public F1FeedEndpoint(string dumpPath, IReadOnlyList<RecordedEntry> entries, double speed, bool loop)
    {
        DumpPath = dumpPath;
        _speed = speed;
        _loop = loop;
        var start = -1;
        for (var index = 0; index < entries.Count; index++)
        {
            if (!entries[index].Payload.Contains("\"type\":3", StringComparison.Ordinal)) continue;
            start = index;
            break;
        }
        if (start < 0)
            throw new InvalidDataException(
                $"'{Path.GetFileName(dumpPath)}' has no Formula 1 initial state, so it cannot start a connection.");
        // The initial state has to be first: recorded deltas before it assume a state the client never had.
        _frames = entries.Skip(start).ToList();
    }

    public string Id => EmulatedFeed.Formula1;
    public string Championship => EmulatedFeed.Championships[Id];
    public string DumpPath { get; }
    public int FrameCount => _frames.Count;

    public void Map(WebApplication app) => app.Map("/f1/signalrcore", HandleAsync);

    private async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var cancellationToken = context.RequestAborted;
        _ = WebSocketPush.DrainAsync(socket, cancellationToken);
        await WebSocketPush.PlayAsync(socket, _frames, _speed, _loop, handshake: true, cancellationToken);
    }
}

/// <summary>Emulates an FIA Formula 2, Formula 3, or F1 Academy SignalR hub.</summary>
internal sealed class FiaFeedEndpoint : IFeedEndpoint
{
    private readonly IReadOnlyList<RecordedEntry> _frames;
    private readonly double _speed;
    private readonly bool _loop;

    public FiaFeedEndpoint(string id, string dumpPath, IReadOnlyList<RecordedEntry> entries, double speed, bool loop)
    {
        Id = id;
        DumpPath = dumpPath;
        _speed = speed;
        _loop = loop;
        // Entry 0 is the hub's own "{}" handshake, which is exactly what a connecting client expects first.
        _frames = entries;
    }

    public string Id { get; }
    public string Championship => EmulatedFeed.Championships[Id];
    public string DumpPath { get; }
    public int FrameCount => _frames.Count;

    public void Map(WebApplication app)
    {
        app.MapGet($"/{Id}/streaming/negotiate", () => Results.Json(new
        {
            ConnectionToken = "feed-server",
            ConnectionId = Guid.NewGuid().ToString("N"),
            KeepAliveTimeout = 20.0,
            DisconnectTimeout = 30.0,
            TryWebSockets = true,
            ProtocolVersion = "1.5",
            TransportConnectTimeout = 5.0,
            LongPollingTimeout = 20.0
        }));
        app.Map($"/{Id}/streaming/connect", HandleAsync);
    }

    private async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var cancellationToken = context.RequestAborted;
        _ = WebSocketPush.DrainAsync(socket, cancellationToken);
        await WebSocketPush.PlayAsync(socket, _frames, _speed, _loop, handshake: false, cancellationToken);
    }
}
