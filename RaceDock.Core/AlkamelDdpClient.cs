// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal interface IAlkamelDdpClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task<JsonObject?> ReceiveAsync(CancellationToken cancellationToken);
    Task SendAsync(JsonObject message, CancellationToken cancellationToken);
}

/// <summary>Minimal SockJS and Meteor DDP transport for Al Kamel timing streams.</summary>
internal sealed class AlkamelDdpClient(Uri socketBase, string source) : IAlkamelDdpClient
{
    private const int MaximumMessageBytes = 2 * 1024 * 1024;
    private readonly Queue<JsonObject> _pending = [];
    private ClientWebSocket? _socket;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_socket is not null) throw new InvalidOperationException("The Al Kamel connection is already open.");
        _socket = new ClientWebSocket();
        _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        _socket.Options.SetRequestHeader("User-Agent", "RaceDock/1.0");
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(10));
        await _socket.ConnectAsync(CreateEndpoint(socketBase), connectCts.Token);

        while (!connectCts.IsCancellationRequested)
        {
            var frame = await ReceiveTextAsync(connectCts.Token)
                ?? throw new WebSocketException("Al Kamel SockJS connection closed during startup.");
            var messages = DecodeFrame(frame, out var opened, out _);
            foreach (var message in messages) _pending.Enqueue(message);
            if (opened) return;
        }
    }

    public async Task<JsonObject?> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (_pending.TryDequeue(out var pending)) return pending;
        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = await ReceiveTextAsync(cancellationToken);
            if (frame is null) return null;
            var messages = DecodeFrame(frame, out _, out var heartbeat);
            foreach (var message in messages) _pending.Enqueue(message);
            if (_pending.TryDequeue(out pending)) return pending;
            if (heartbeat) return new JsonObject { ["msg"] = "_sockjsHeartbeat" };
        }
        return null;
    }

    public async Task SendAsync(JsonObject message, CancellationToken cancellationToken)
    {
        if (_socket?.State != WebSocketState.Open) throw new WebSocketException("Al Kamel SockJS connection is not open.");
        var bytes = Encoding.UTF8.GetBytes(EncodeMessage(message));
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    internal static Uri CreateEndpoint(Uri socketBase)
    {
        Span<char> server = stackalloc char[3];
        for (var index = 0; index < server.Length; index++) server[index] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        Span<char> session = stackalloc char[8];
        for (var index = 0; index < session.Length; index++) session[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new Uri(socketBase, $"{server}/{session}/websocket");
    }

    internal static string EncodeMessage(JsonObject message) =>
        JsonSerializer.Serialize(new[] { message.ToJsonString() });

    internal static IReadOnlyList<JsonObject> DecodeFrame(string frame, out bool opened, out bool heartbeat)
    {
        opened = frame == "o";
        heartbeat = frame == "h";
        if (opened || heartbeat || string.IsNullOrWhiteSpace(frame)) return [];
        if (!frame.StartsWith('a')) throw new InvalidDataException("Al Kamel SockJS returned an unexpected frame.");
        var values = JsonNode.Parse(frame[1..]) as JsonArray
            ?? throw new InvalidDataException("Al Kamel SockJS returned an invalid message array.");
        List<JsonObject> result = [];
        foreach (var value in values)
        {
            var text = value?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (JsonNode.Parse(text) is not JsonObject message)
                throw new InvalidDataException("Al Kamel SockJS returned a non-object DDP message.");
            result.Add(message);
        }
        return result;
    }

    private async Task<string?> ReceiveTextAsync(CancellationToken cancellationToken)
    {
        if (_socket is null) throw new InvalidOperationException("The Al Kamel connection has not been opened.");
        var buffer = new byte[65536];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text)
                throw new InvalidDataException("Al Kamel SockJS returned a non-text frame.");
            message.Write(buffer, 0, result.Count);
            if (message.Length > MaximumMessageBytes)
                throw new InvalidDataException("Al Kamel SockJS message exceeded the supported size.");
        } while (!result.EndOfMessage);
        var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
        FeedDump.Record(source, text);
        return text;
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket is null) return;
        if (_socket.State == WebSocketState.Open)
        {
            try
            {
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", closeCts.Token);
            }
            catch { _socket.Abort(); }
        }
        _socket.Dispose();
        _socket = null;
        _pending.Clear();
    }
}
