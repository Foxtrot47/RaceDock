// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MotorsportTaskbar.Core;

/// <summary>Streams the official FIA Formula 2 or Formula 3 live-timing SignalR hub.</summary>
public sealed class F2LiveTimingSource(IClock clock, IAlertArbiter alerts, string series = "F2") : ILiveTimingSource
{
    private const string ConnectionData = "[{\"name\":\"streaming\"}]";
    private static readonly string[] JoinedFeeds = ["data", "stats", "weather", "status", "time", "racedetails"];
    private static readonly string[] InitialFeeds = ["data", "statsfeed", "weatherfeed", "sessionfeed", "trackfeed", "timefeed", "racedetailsfeed"];

    private readonly JsonObject _lines = [];
    private readonly string _baseUrl = BaseUrlFor(series);
    private readonly Championship _championship = series == "F3" ? Championship.Formula3 : Championship.Formula2;
    private CancellationTokenSource? _runCts;
    private Task? _run;
    private HttpClient? _http;
    private ClientWebSocket? _socket;
    private string _meeting = $"Formula {series}";
    private string _session = "Live Session";
    private string _timingSessionType = "";
    private string _circuit = "";
    private string _sessionStatus = "";
    private string? _timeRemaining;
    private TrackCondition _trackCondition = TrackCondition.Unknown;
    private ConnectionState _connection = ConnectionState.Disconnected;

    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived;
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_run is not null) return Task.CompletedTask;
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _run = Task.Run(() => RunReconnectLoopAsync(_runCts.Token), _runCts.Token);
        return Task.CompletedTask;
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
                if (!ct.IsCancellationRequested) throw new WebSocketException($"{series} live-timing connection closed.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Failed?.Invoke(new($"{series} feed failed: {ex.Message}", ex, clock.UtcNow));
                ChangeConnection(ConnectionState.Faulted);
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Min(attempt++, 5)))), ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            }
            finally { await CloseConnectionAsync(); }
        }
    }

    private async Task ConnectAndReceiveAsync(CancellationToken ct)
    {
        var cookies = new CookieContainer();
        _http = new HttpClient(new HttpClientHandler { CookieContainer = cookies, UseCookies = true });
        _http.DefaultRequestHeaders.Add("Origin", $"https://www.fiaformula{series}.com");
        var encodedData = Uri.EscapeDataString(ConnectionData);
        var negotiation = await _http.GetFromJsonAsync<NegotiateResponse>($"{_baseUrl}/negotiate?clientProtocol=2.1", ct)
            ?? throw new InvalidOperationException($"{series} negotiation returned no connection token.");
        var token = Uri.EscapeDataString(negotiation.ConnectionToken);
        var query = $"transport=webSockets&clientProtocol=1.5&connectionToken={token}&connectionData={encodedData}";

        _socket = new ClientWebSocket();
        _socket.Options.Cookies = cookies;
        _socket.Options.SetRequestHeader("Origin", $"https://www.fiaformula{series}.com");
        _socket.Options.SetRequestHeader("User-Agent", "MotorsportTaskbar/1.0");
        var webSocketBaseUrl = _baseUrl.Replace("https://", "wss://", StringComparison.Ordinal);
        await _socket.ConnectAsync(new Uri($"{webSocketBaseUrl}/connect?{query}&tid={Random.Shared.Next(0, 11)}"), ct);

        await SendInvocationAsync(0, "JoinFeeds", [series, JoinedFeeds], ct);
        await SendInvocationAsync(1, "GetData2", [series, InitialFeeds], ct);
        ChangeConnection(ConnectionState.Connected);
        await ReceiveLoopAsync(ct);
    }

    private async Task SendInvocationAsync(int id, string method, object[] arguments, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { H = "streaming", M = method, A = arguments, I = id });
        await _socket!.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
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
            if (message.Length == 0) continue;
            ProcessMessage(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
        }
    }

    internal void ProcessMessage(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.TryGetProperty("R", out var response) && response.ValueKind == JsonValueKind.Object)
        {
            ProcessInitial((JsonObject)JsonNode.Parse(response.GetRawText())!);
        }
        if (!root.TryGetProperty("M", out var messages) || messages.ValueKind != JsonValueKind.Array) return;
        foreach (var message in messages.EnumerateArray())
        {
            if (!message.TryGetProperty("M", out var methodNode) || !message.TryGetProperty("A", out var args) || args.ValueKind != JsonValueKind.Array) continue;
            ProcessFeed(methodNode.GetString() ?? "", args);
        }
    }

    private void ProcessInitial(JsonObject data)
    {
        if (data["data"] is JsonArray timing)
        {
            if (timing.ElementAtOrDefault(1) is JsonObject metadata) ApplyData(metadata);
            if (timing.ElementAtOrDefault(2) is JsonObject lines) MergeLines(lines);
        }
        ApplyInitialPair(data["racedetailsfeed"] as JsonArray, ApplyRaceDetails);
        ApplyInitialPair(data["sessionfeed"] as JsonArray, ApplySessionStatus);
        ApplyInitialPair(data["trackfeed"] as JsonArray, ApplyTrackStatus);
        ApplyTimeFeed(data["timefeed"] as JsonArray);
        Publish();
    }

    private static void ApplyInitialPair(JsonArray? pair, Action<JsonObject> apply)
    {
        if (pair?.ElementAtOrDefault(1) is JsonObject value) apply(value);
    }

    private void ProcessFeed(string method, JsonElement args)
    {
        var topic = method.ToLowerInvariant();
        JsonNode? data = null;
        if (topic == "timefeed" && args.GetArrayLength() >= 3)
        {
            data = JsonNode.Parse(args[2].GetRawText());
            _timeRemaining = JsonSupport.String(data);
        }
        else if (topic == "datafeed" && args.GetArrayLength() >= 3)
        {
            data = JsonNode.Parse(args[2].GetRawText());
            if (data is JsonObject feed) ApplyData(feed);
        }
        else if (args.GetArrayLength() >= 2)
        {
            data = JsonNode.Parse(args[1].GetRawText());
            if (data is JsonObject value)
            {
                if (topic == "racedetailsfeed") ApplyRaceDetails(value);
                else if (topic == "sessionfeed") ApplySessionStatus(value);
                else if (topic == "trackfeed") ApplyTrackStatus(value);
            }
        }
        if (data is null) return;
        DeltaReceived?.Invoke(new(topic, data, clock.UtcNow));
        Publish();
    }

    private void ApplyData(JsonObject feed)
    {
        var sessionType = JsonSupport.String(feed["Session"]);
        if (!string.IsNullOrWhiteSpace(sessionType)) _timingSessionType = sessionType;
        _session = sessionType switch
        {
            "Practice" => "Free Practice",
            "F1Qualifing" or "GPQualifying" or "Qualifying" => "Qualifying",
            { Length: > 0 } value => value,
            _ => _session
        };
        if (feed["lines"] is JsonObject lines) MergeLines(lines);
    }

    private void MergeLines(JsonObject updates)
    {
        foreach (var pair in updates)
        {
            if (pair.Value is not JsonObject update) continue;
            if (_lines[pair.Key] is not JsonObject line) _lines[pair.Key] = line = [];
            JsonSupport.DeepMerge(line, update);
        }
    }

    private void ApplyRaceDetails(JsonObject value)
    {
        _meeting = JsonSupport.String(value["Race"]) ?? _meeting;
        _circuit = JsonSupport.String(value["Circuit"]) ?? _circuit;
        _session = JsonSupport.String(value["Session"]) ?? _session;
    }

    private void ApplySessionStatus(JsonObject value)
    {
        var previous = _sessionStatus;
        _sessionStatus = JsonSupport.String(value["Value"]) ?? _sessionStatus;
        if (_sessionStatus == "Started" && previous != "Started")
            alerts.Accept(new(AlertKind.SessionStart, 10, false, $"{series} SESSION STARTED", _session, null, clock.UtcNow, $"{series.ToLowerInvariant()}:start:{_meeting}:{_session}", TimeSpan.FromSeconds(4)));
        if (_sessionStatus is "Finished" or "Finalised" && previous is not ("Finished" or "Finalised"))
            alerts.Accept(new(AlertKind.Chequered, 90, false, "CHEQUERED FLAG", _session, null, clock.UtcNow, $"{series.ToLowerInvariant()}:end:{_meeting}:{_session}", TimeSpan.FromSeconds(6)));
    }

    private void ApplyTrackStatus(JsonObject value)
    {
        _trackCondition = JsonSupport.String(value["Value"]) switch
        {
            "1" => TrackCondition.AllClear,
            "2" => TrackCondition.Yellow,
            "4" => TrackCondition.SafetyCar,
            "5" => TrackCondition.RedFlag,
            "6" => TrackCondition.VirtualSafetyCar,
            _ => TrackCondition.Unknown
        };
        alerts.ApplyTrackCondition(_trackCondition, JsonSupport.String(value["Message"]), null, clock.UtcNow);
    }

    private void Publish()
    {
        if (_lines.Count == 0) return;
        var bestLap = _lines.Select(x => LapSeconds(JsonSupport.TimingValue(x.Value?["best"]))).Where(x => x.HasValue).Min();
        var standings = _lines.Select(pair =>
        {
            var line = pair.Value as JsonObject;
            var driver = line?["driver"] as JsonObject;
            var status = line?["status"] as JsonObject;
            var lapTime = LapSeconds(JsonSupport.TimingValue(line?["best"]));
            var position = JsonSupport.Int(line?["position"]?["Value"]) ?? int.MaxValue;
            var gap = JsonSupport.TimingValue(line?[_timingSessionType == "Race" ? "gap" : "gapP"]);
            var interval = JsonSupport.TimingValue(line?[_timingSessionType == "Race" ? "interval" : "intervalP"]);
            return new CompetitorStanding(
                JsonSupport.String(driver?["RacingNumber"]) ?? pair.Key, position,
                JsonSupport.String(driver?["TLA"]) ?? pair.Key, JsonSupport.String(driver?["FullName"]) ?? "", "",
                position == 1 ? "LEAD" : gap, interval, null, JsonSupport.Int(line?["laps"]?["Value"]) ?? 0,
                JsonSupport.Bool(status?["InPit"]), JsonSupport.Bool(status?["Retired"]), JsonSupport.Bool(status?["Stopped"]),
                bestLap.HasValue && lapTime == bestLap);
        }).Where(x => x.Position != int.MaxValue).OrderBy(x => x.Position).ToList();
        if (standings.Count == 0) return;
        var lifecycle = SessionLifecycleFor(_sessionStatus);
        SnapshotReceived?.Invoke(new(_meeting, _session, _circuit, standings.Max(x => x.Lap), null,
            _trackCondition, standings, clock.UtcNow, lifecycle, _connection, _timeRemaining, _championship));
    }
    internal static SessionLifecycle SessionLifecycleFor(string? status) => status switch
    {
        "Finished" or "Finalised" => SessionLifecycle.Ended,
        "Started" or "Running" or "Aborted" or "Suspended" => SessionLifecycle.Live,
        _ => SessionLifecycle.PreSession
    };
    internal static string BaseUrlFor(string value) => value switch
    {
        "F2" => "https://ltss.fiaformula2.com/streaming",
        "F3" => "https://ltss.fiaformula3.com/streaming",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Only F2 and F3 are supported.")
    };

    private void ApplyTimeFeed(JsonArray? pair)
    {
        _timeRemaining = JsonSupport.String(pair?.ElementAtOrDefault(2));
    }

    private static double? LapSeconds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split(':');
        return parts.Length == 2 && double.TryParse(parts[0], out var minutes) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? minutes * 60 + seconds : null;
    }

    private void ChangeConnection(ConnectionState value)
    {
        _connection = value;
        ConnectionChanged?.Invoke(value);
        Publish();
    }

    private async Task CloseConnectionAsync()
    {
        if (_socket?.State == WebSocketState.Open)
            try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None); } catch { }
        _socket?.Dispose(); _socket = null;
        _http?.Dispose(); _http = null;
    }

    public async Task StopAsync()
    {
        if (_runCts is null) return;
        await _runCts.CancelAsync();
        await CloseConnectionAsync();
        if (_run is not null) try { await _run; } catch (OperationCanceledException) { }
        _run = null;
        _runCts.Dispose(); _runCts = null;
        ChangeConnection(ConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private sealed class NegotiateResponse
    {
        [JsonPropertyName("ConnectionToken")]
        public string ConnectionToken { get; init; } = "";
    }
}
