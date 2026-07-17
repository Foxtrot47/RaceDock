// Copyright (c) 2026 MotorsportTaskbar contributors
// SignalR handshake adapted from mbot; SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MotorsportTaskbar.Core;

public sealed class F1LiveTimingSource(TimingStateProcessor processor, IClock clock, string? logDirectory = null) : ILiveTimingSource
{
    private const string WsUrl = "wss://livetiming.formula1.com/signalrcore";
    private const char RecordSeparator = '\u001e';
    private static readonly string[] Topics = ["TimingData", "TimingAppData", "DriverList", "RaceControlMessages", "SessionInfo", "TrackStatus", "LapCount", "SessionData", "SessionStatus", "ExtrapolatedClock"];
    private CancellationTokenSource? _runCts;
    private ClientWebSocket? _socket;
    private TimeSpan? _remainingAtUpdate;
    private DateTimeOffset _clockUpdatedAt;
    private bool _clockExtrapolating;
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
        _socket = new ClientWebSocket();
        _socket.Options.SetRequestHeader("Origin", "https://www.formula1.com");
        _socket.Options.SetRequestHeader("User-Agent", "MotorsportTaskbar/1.0");
        await _socket.ConnectAsync(new Uri(WsUrl), ct);
        await SendAsync(JsonSerializer.Serialize(new { protocol = "json", version = 1 }) + RecordSeparator, ct);
        var handshake = await ReceiveTextAsync(ct) ?? throw new WebSocketException("F1 live-timing handshake closed.");
        var handshakeMessage = handshake.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(handshakeMessage) && handshakeMessage != "{}")
            throw new InvalidOperationException($"F1 live-timing handshake failed: {handshakeMessage}");
        await SendAsync(JsonSerializer.Serialize(new { type = 1, target = "subscribe", arguments = new object[] { Topics }, invocationId = "1" }) + RecordSeparator, ct);
        ChangeConnection(ConnectionState.Connected);
        await ReceiveLoopAsync(ct);
        if (!ct.IsCancellationRequested) throw new WebSocketException("F1 live-timing connection closed.");
    }

    private async Task SendAsync(string text, CancellationToken ct) =>
        await _socket!.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);

    private async Task<string?> ReceiveTextAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await _socket!.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _socket?.State == WebSocketState.Open)
        {
            var text = await ReceiveTextAsync(ct);
            if (text is null) return;
            if (string.IsNullOrWhiteSpace(text)) continue;
            await RecordFrameAsync(text, ct);
            ProcessMessage(text);
        }
    }

    internal void ProcessMessage(string text)
    {
        foreach (var part in text.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            using var document = JsonDocument.Parse(part);
            var root = document.RootElement;
            if (root.TryGetProperty("type", out var type) && type.GetInt32() == 3 &&
                root.TryGetProperty("invocationId", out var invocationId) && invocationId.GetString() == "1" &&
                root.TryGetProperty("result", out var initial) && initial.ValueKind == JsonValueKind.Object)
            {
                var state = (JsonObject)JsonNode.Parse(initial.GetRawText())!;
                UpdateClock(state["ExtrapolatedClock"]);
                processor.ProcessInitial(state);
                continue;
            }
            if (!root.TryGetProperty("type", out type) || type.GetInt32() != 1 ||
                !root.TryGetProperty("target", out var target) || !target.GetString()!.Equals("feed", StringComparison.OrdinalIgnoreCase) ||
                !root.TryGetProperty("arguments", out var args) || args.GetArrayLength() < 2) continue;
            var topic = args[0].GetString() ?? "";
            var node = JsonNode.Parse(args[1].GetRawText());
            if (node is null) continue;
            if (topic == "ExtrapolatedClock") UpdateClock(node);
            var delta = new TimingDelta(topic, node, clock.UtcNow);
            DeltaReceived?.Invoke(delta);
            processor.ProcessDelta(topic, node, delta.Timestamp);
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

    private void ForwardSnapshot(TimingSnapshot value) => SnapshotReceived?.Invoke(value with { TimeRemaining = CurrentTimeRemaining() });
    private void UpdateClock(JsonNode? value)
    {
        _remainingAtUpdate = TimeSpan.TryParse(JsonSupport.String(value?["Remaining"]), out var remaining) ? remaining : null;
        _clockUpdatedAt = clock.UtcNow;
        _clockExtrapolating = JsonSupport.Bool(value?["Extrapolating"]);
    }

    private string? CurrentTimeRemaining()
    {
        if (!_remainingAtUpdate.HasValue) return null;
        var remaining = _remainingAtUpdate.Value - (_clockExtrapolating ? clock.UtcNow - _clockUpdatedAt : TimeSpan.Zero);
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        return $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }
    private void ChangeConnection(ConnectionState value) { processor.SetConnection(value); ConnectionChanged?.Invoke(value); }
    private async Task CloseConnectionAsync()
    {
        if (_socket?.State == WebSocketState.Open) try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None); } catch { }
        _socket?.Dispose(); _socket = null;
    }
    public async ValueTask DisposeAsync() => await StopAsync();
}
