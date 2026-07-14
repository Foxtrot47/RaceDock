// Copyright (c) 2026 MotorsportTaskbar contributors
// SignalR handshake adapted from mbot; SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MotorsportTaskbar.Core;

public sealed class F1LiveTimingSource(TimingStateProcessor processor, IClock clock, string? logDirectory = null) : ILiveTimingSource
{
    private const string HttpBase = "https://livetiming.formula1.com/signalr";
    private const string WsBase = "wss://livetiming.formula1.com/signalr";
    private const string ConnectionData = "[{\"name\":\"Streaming\"}]";
    private static readonly string[] Topics = ["TimingData", "TimingAppData", "DriverList", "RaceControlMessages", "SessionInfo", "TrackStatus", "LapCount", "SessionData"];
    private CancellationTokenSource? _runCts;
    private ClientWebSocket? _socket;
    private HttpClient? _http;
    public bool DiagnosticRecordingEnabled { get; set; }
    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived;
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_runCts is not null) return Task.CompletedTask;
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        processor.SnapshotChanged += ForwardSnapshot;
        _ = Task.Run(() => RunReconnectLoopAsync(_runCts.Token), _runCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_runCts is null) return;
        await _runCts.CancelAsync();
        await CloseConnectionAsync();
        _runCts.Dispose(); _runCts = null;
        processor.SnapshotChanged -= ForwardSnapshot;
        ChangeConnection(ConnectionState.Disconnected);
    }

    private async Task RunReconnectLoopAsync(CancellationToken ct)
    {
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                ChangeConnection(ConnectionState.Connecting);
                await ConnectAndReceiveAsync(ct);
                attempt = 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Failed?.Invoke(new(ex.Message, ex, clock.UtcNow));
                ChangeConnection(ConnectionState.Faulted);
                var delay = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Min(attempt++, 5))));
                try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { break; }
            }
            finally { await CloseConnectionAsync(); }
        }
    }

    private async Task ConnectAndReceiveAsync(CancellationToken ct)
    {
        var cookies = new CookieContainer();
        _http = new HttpClient(new HttpClientHandler { CookieContainer = cookies, UseCookies = true });
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MotorsportTaskbar/1.0");
        _http.DefaultRequestHeaders.Add("Origin", "https://www.formula1.com");
        var negotiation = await _http.GetFromJsonAsync<NegotiateResponse>($"{HttpBase}/negotiate?clientProtocol=1.5&connectionData={Uri.EscapeDataString(ConnectionData)}", ct)
            ?? throw new InvalidOperationException("F1 negotiation returned no connection token.");
        _socket = new ClientWebSocket(); _socket.Options.Cookies = cookies;
        _socket.Options.SetRequestHeader("Origin", "https://www.formula1.com");
        _socket.Options.SetRequestHeader("User-Agent", "MotorsportTaskbar/1.0");
        var uri = $"{WsBase}/connect?transport=webSockets&clientProtocol=1.5&connectionToken={Uri.EscapeDataString(negotiation.ConnectionToken)}&connectionData={Uri.EscapeDataString(ConnectionData)}&tid={Random.Shared.Next(0, 11)}";
        await _socket.ConnectAsync(new Uri(uri), ct);
        var subscribe = JsonSerializer.Serialize(new { H = "Streaming", M = "Subscribe", A = new[] { Topics }, I = 1 });
        await _socket.SendAsync(Encoding.UTF8.GetBytes(subscribe), WebSocketMessageType.Text, true, ct);
        ChangeConnection(ConnectionState.Connected);
        await ReceiveLoopAsync(ct);
        if (!ct.IsCancellationRequested) throw new WebSocketException("F1 live-timing connection closed.");
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];
        while (!ct.IsCancellationRequested && _socket?.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) return;
                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            var text = Encoding.UTF8.GetString(message.ToArray());
            if (text is "" or "{}" or " ") continue;
            await RecordFrameAsync(text, ct);
            ProcessMessage(text);
        }
    }

    internal void ProcessMessage(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.TryGetProperty("R", out var initial) && initial.ValueKind == JsonValueKind.Object)
        {
            processor.ProcessInitial((JsonObject)JsonNode.Parse(initial.GetRawText())!); return;
        }
        if (!root.TryGetProperty("M", out var messages) || messages.ValueKind != JsonValueKind.Array) return;
        foreach (var message in messages.EnumerateArray())
        {
            if (message.GetProperty("M").GetString() != "feed" || !message.TryGetProperty("A", out var args) || args.GetArrayLength() < 2) continue;
            var topic = args[0].GetString() ?? ""; var node = JsonNode.Parse(args[1].GetRawText());
            if (node is null) continue;
            var delta = new TimingDelta(topic, node, clock.UtcNow);
            DeltaReceived?.Invoke(delta); processor.ProcessDelta(topic, node, delta.Timestamp);
        }
    }

    private async Task RecordFrameAsync(string text, CancellationToken ct)
    {
        if (!DiagnosticRecordingEnabled || logDirectory is null) return;
        Directory.CreateDirectory(logDirectory);
        var path = Path.Combine(logDirectory, "diagnostic-feed.ndjson");
        if (File.Exists(path) && new FileInfo(path).Length >= 2 * 1024 * 1024) return;
        await File.AppendAllTextAsync(path, JsonSerializer.Serialize(new { timestamp = clock.UtcNow, frame = text }) + Environment.NewLine, ct);
    }

    private void ForwardSnapshot(TimingSnapshot value) => SnapshotReceived?.Invoke(value);
    private void ChangeConnection(ConnectionState value) { processor.SetConnection(value); ConnectionChanged?.Invoke(value); }
    private async Task CloseConnectionAsync()
    {
        if (_socket?.State == WebSocketState.Open) try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None); } catch { }
        _socket?.Dispose(); _socket = null; _http?.Dispose(); _http = null;
    }
    public async ValueTask DisposeAsync() => await StopAsync();
    private sealed class NegotiateResponse { [JsonPropertyName("ConnectionToken")] public string ConnectionToken { get; init; } = ""; }
}
