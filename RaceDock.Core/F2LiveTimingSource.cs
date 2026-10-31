// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RaceDock.Core;

/// <summary>Streams the official FIA Formula 2 or Formula 3 live-timing SignalR hub.</summary>
public sealed class F2LiveTimingSource(IClock clock, IAlertArbiter alerts, string series = "F2", string? streamBase = null) : ILiveTimingSource
{
    private const string ConnectionData = "[{\"name\":\"streaming\"}]";
    private static readonly string[] JoinedFeeds = ["data", "stats", "weather", "status", "time", "racedetails"];
    private static readonly string[] InitialFeeds = ["data", "statsfeed", "weatherfeed", "sessionfeed", "trackfeed", "timefeed", "racedetailsfeed"];
    private static readonly IReadOnlyDictionary<string, string> F2Teams2026 = new Dictionary<string, string>
    {
        ["1"] = "Invicta Racing", ["2"] = "Invicta Racing",
        ["3"] = "Hitech", ["4"] = "Hitech",
        ["5"] = "Campos Racing", ["6"] = "Campos Racing",
        ["7"] = "DAMS Lucas Oil", ["8"] = "DAMS Lucas Oil",
        ["9"] = "MP Motorsport", ["10"] = "MP Motorsport",
        ["11"] = "PREMA Racing", ["12"] = "PREMA Racing",
        ["14"] = "Rodin Motorsport", ["15"] = "Rodin Motorsport",
        ["16"] = "ART Grand Prix", ["17"] = "ART Grand Prix",
        ["20"] = "AIX Racing", ["21"] = "AIX Racing",
        ["22"] = "Van Amersfoort Racing", ["23"] = "Van Amersfoort Racing",
        ["24"] = "TRIDENT", ["25"] = "TRIDENT"
    };
    private static readonly IReadOnlyDictionary<string, string> F3Teams2026 = new Dictionary<string, string>
    {
        ["1"] = "Campos Racing", ["2"] = "Campos Racing", ["3"] = "Campos Racing",
        ["4"] = "TRIDENT", ["5"] = "TRIDENT", ["6"] = "TRIDENT",
        ["7"] = "MP Motorsport", ["8"] = "MP Motorsport", ["9"] = "MP Motorsport",
        ["10"] = "ART Grand Prix", ["11"] = "ART Grand Prix", ["12"] = "ART Grand Prix",
        ["14"] = "Van Amersfoort Racing", ["15"] = "Van Amersfoort Racing", ["16"] = "Van Amersfoort Racing",
        ["17"] = "Rodin Motorsport", ["18"] = "Rodin Motorsport", ["19"] = "Rodin Motorsport",
        ["20"] = "PREMA Racing", ["21"] = "PREMA Racing", ["22"] = "PREMA Racing",
        ["23"] = "Hitech", ["24"] = "Hitech", ["25"] = "Hitech",
        ["26"] = "AIX Racing", ["27"] = "AIX Racing", ["28"] = "AIX Racing",
        ["29"] = "DAMS Lucas Oil", ["30"] = "DAMS Lucas Oil", ["31"] = "DAMS Lucas Oil"
    };
    // From the series' published 2026 teams-and-drivers list (car 45 is the wildcard entry).
    private static readonly IReadOnlyDictionary<string, string> F1AcademyTeams2026 = new Dictionary<string, string>
    {
        ["8"] = "PREMA Racing", ["9"] = "PREMA Racing", ["19"] = "PREMA Racing",
        ["4"] = "Campos Racing", ["18"] = "Campos Racing", ["21"] = "Campos Racing",
        ["3"] = "MP Motorsport", ["12"] = "MP Motorsport", ["32"] = "MP Motorsport",
        ["5"] = "Rodin Motorsport", ["20"] = "Rodin Motorsport", ["28"] = "Rodin Motorsport",
        ["14"] = "ART Grand Prix", ["91"] = "ART Grand Prix", ["95"] = "ART Grand Prix",
        ["45"] = "Hitech", ["55"] = "Hitech", ["56"] = "Hitech"
    };
    // Each F1 Academy driver races in the livery of an F1 team or partner, and that mark, not the entrant team's,
    // is what the series' own timing graphics show beside her name.
    private static readonly IReadOnlyDictionary<string, string> F1AcademySupport2026 = new Dictionary<string, string>
    {
        ["8"] = "Aston Martin", ["9"] = "Mercedes", ["19"] = "Sephora",
        ["4"] = "TAG Heuer", ["18"] = "Racing Bulls", ["21"] = "Red Bull Racing",
        ["3"] = "Alpine", ["12"] = "Ferrari", ["32"] = "LEGO",
        ["5"] = "Audi", ["20"] = "McLaren", ["28"] = "McLaren Oxagon",
        ["14"] = "Gatorade", ["91"] = "Haas", ["95"] = "Williams",
        ["45"] = "TeamViewer", ["55"] = "American Express", ["56"] = "PUMA"
    };

    private readonly JsonObject _lines = [];
    private readonly string _baseUrl = streamBase ?? BaseUrlFor(series);
    private readonly Championship _championship = series switch
    {
        "F3" => Championship.Formula3,
        "F1 Academy" => Championship.F1Academy,
        _ => Championship.Formula2
    };
    private CancellationTokenSource? _runCts;
    private Task? _run;
    private HttpClient? _http;
    private ClientWebSocket? _socket;
    private string _meeting = series == "F1 Academy" ? "F1 Academy" : $"Formula {series}";
    private string _season = "";
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
        var origin = OriginFor(series);
        _http.DefaultRequestHeaders.Add("Origin", origin);
        var encodedData = Uri.EscapeDataString(ConnectionData);
        var negotiation = await _http.GetFromJsonAsync<NegotiateResponse>($"{_baseUrl}/negotiate?clientProtocol=2.1", ct)
            ?? throw new InvalidOperationException($"{series} negotiation returned no connection token.");
        var token = Uri.EscapeDataString(negotiation.ConnectionToken);
        var query = $"transport=webSockets&clientProtocol=1.5&connectionToken={token}&connectionData={encodedData}";

        _socket = new ClientWebSocket();
        _socket.Options.Cookies = cookies;
        _socket.Options.SetRequestHeader("Origin", origin);
        _socket.Options.SetRequestHeader("User-Agent", BrowserUserAgent.Value);
        var webSocketBaseUrl = WebSocketBase(_baseUrl);
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
            var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            // "F1 Academy" is dumped as f1academy.ndjson so the file name stays free of spaces.
            FeedDump.Record(series.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant(), text);
            ProcessMessage(text);
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
            if (timing.ElementAtOrDefault(1) is JsonNode metadata) ApplyData(metadata);
            if (timing.ElementAtOrDefault(2) is JsonNode lines)
            {
                // The initial data is the whole field. After a reconnect or a new session, lines kept from before
                // would otherwise linger beside it with positions that no longer exist.
                _lines.Clear();
                MergeLines(lines);
            }
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
            if (data is not null) ApplyData(data);
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

    private void ApplyData(JsonNode data)
    {
        if (data is JsonArray lineUpdates)
        {
            MergeLines(lineUpdates);
            return;
        }
        if (data is not JsonObject feed) return;
        var sessionType = JsonSupport.String(feed["Session"]);
        if (!string.IsNullOrWhiteSpace(sessionType)) _timingSessionType = sessionType;
        _session = sessionType switch
        {
            "Practice" => "Free Practice",
            "F1Qualifing" or "GPQualifying" or "Qualifying" => "Qualifying",
            { Length: > 0 } value => value,
            _ => _session
        };
        if (feed["lines"] is JsonNode feedLines) MergeLines(feedLines);
        else if (LooksLikeLineMap(feed)) MergeLines(feed);
    }

    private void MergeLines(JsonNode updates)
    {
        if (updates is JsonObject map)
        {
            if (LooksLikeLine(map))
            {
                MergeLine(LineKey(map), map);
                return;
            }
            foreach (var pair in map)
                if (pair.Value is JsonObject update) MergeLine(pair.Key, update);
            return;
        }
        if (updates is not JsonArray array) return;
        foreach (var item in array)
        {
            if (item is JsonArray pair && pair.Count >= 2 && pair[1] is JsonObject pairValue)
            {
                MergeLine(JsonSupport.String(pair[0]), pairValue);
            }
            else if (item is JsonObject line)
            {
                if (line["line"] is JsonObject nested) line = nested;
                if (LooksLikeLine(line)) MergeLine(LineKey(line), line);
                else if (LooksLikeLineMap(line)) MergeLines(line);
            }
        }
    }

    private void MergeLine(string? key, JsonObject update)
    {
        // One car, one standing: the payload key is only the fallback, because F1 Academy keys its lines by
        // display slot while deltas name the car, and Publish emits one standing per stored line.
        var car = LineKey(update);
        key = car ?? key;
        if (string.IsNullOrWhiteSpace(key)) return;
        // A line stored under its payload key before this path named the car is adopted rather than duplicated.
        if (car is not null && _lines[car] is null)
        {
            var adopted = _lines.FirstOrDefault(pair => pair.Value is JsonObject stored && LineKey(stored) == car);
            if (adopted.Value is JsonObject candidate)
            {
                _lines.Remove(adopted.Key);
                _lines[car] = candidate;
            }
        }
        if (_lines[key] is not JsonObject line) _lines[key] = line = [];
        // A delta sends only the sectors that changed, each naming its Id, so they are kept keyed by Id rather
        // than as the array the feed sends, which a merge would otherwise replace whole.
        if (update["sectors"] is JsonArray sectors)
        {
            update = (JsonObject)update.DeepClone();
            var keyed = new JsonObject();
            foreach (var sector in sectors.OfType<JsonObject>())
                if (JsonSupport.String(sector["Id"]) is { Length: > 0 } id) keyed[id] = sector.DeepClone();
            update["sectors"] = keyed;
        }
        JsonSupport.DeepMerge(line, update);
    }

    private static bool LooksLikeLineMap(JsonObject value) =>
        value.Any(pair => int.TryParse(pair.Key, out _) && pair.Value is JsonObject);

    private static bool LooksLikeLine(JsonObject value) =>
        value["driver"] is JsonObject || value["position"] is not null || value["RacingNumber"] is not null;

    /// <summary>
    /// The car a line belongs to. <c>Number</c> is the line's display slot, which F1 Academy updates carry beside
    /// the car (slot 1 is whoever leads), so it never identifies a car.
    /// </summary>
    private static string? LineKey(JsonObject line) =>
        JsonSupport.String((line["driver"] as JsonObject)?["RacingNumber"]) ??
        JsonSupport.String(line["RacingNumber"]);

    private void ApplyRaceDetails(JsonObject value)
    {
        _season = JsonSupport.String(value["Season"]) ?? _season;
        _meeting = MeetingName(value) ?? _meeting;
        _circuit = JsonSupport.String(value["Circuit"]) ?? _circuit;
        _session = JsonSupport.String(value["Session"]) ?? _session;
        if (_session.Contains("race", StringComparison.OrdinalIgnoreCase)) _timingSessionType = "Race";
    }

    /// <summary>
    /// The round's name. F1 Academy publishes the circuit, so the round carries the grand prix it belongs to; every
    /// other series names the event itself.
    /// </summary>
    private string? MeetingName(JsonObject details)
    {
        var race = JsonSupport.String(details["Race"]);
        if (_championship != Championship.F1Academy) return race;
        return ChampionshipCalendars.F1AcademyGrandPrixName(race) ?? race;
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
            var racingNumber = JsonSupport.String(driver?["RacingNumber"]) ?? pair.Key;
            var gap = JsonSupport.TimingValue(line?[_timingSessionType == "Race" ? "gap" : "gapP"]);
            var interval = JsonSupport.TimingValue(line?[_timingSessionType == "Race" ? "interval" : "intervalP"]);
            return new CompetitorStanding(
                racingNumber, position,
                JsonSupport.String(driver?["TLA"]) ?? pair.Key, DriverName(JsonSupport.String(driver?["FullName"])), TeamFor(racingNumber),
                position == 1 ? "LEAD" : gap, interval, null, JsonSupport.Int(line?["laps"]?["Value"]) ?? 0,
                JsonSupport.Bool(status?["InPit"]), JsonSupport.Bool(status?["Retired"]), JsonSupport.Bool(status?["Stopped"]),
                bestLap.HasValue && lapTime == bestLap,
                ResultTime: JsonSupport.TimingValue(line?["best"]),
                LastLapTime: JsonSupport.TimingValue(line?["last"]),
                CurrentSectors: Sectors(line?["sectors"] as JsonObject),
                Manufacturer: SupportFor(racingNumber));
        }).Where(x => x.Position != int.MaxValue).OrderBy(x => x.Position).ToList();
        if (standings.Count == 0) return;
        var lifecycle = SessionLifecycleFor(_sessionStatus);
        SnapshotReceived?.Invoke(new(_meeting, _session, _circuit, standings.Max(x => x.Lap), null,
            _trackCondition, standings, clock.UtcNow, lifecycle, _connection, _timeRemaining, _championship));
    }
    /// <summary>The three sectors of the lap in progress; a blank sector has not been run yet on this lap.</summary>
    private static SectorTimingSet? Sectors(JsonObject? sectors)
    {
        if (sectors is null) return null;
        var set = new SectorTimingSet(Sector(sectors["1"]), Sector(sectors["2"]), Sector(sectors["3"]));
        return set.Sector1 is null && set.Sector2 is null && set.Sector3 is null ? null : set;
    }

    private static SectorTiming? Sector(JsonNode? node)
    {
        if (node is not JsonObject sector || JsonSupport.TimingValue(sector) is not { } value) return null;
        var state = JsonSupport.Bool(sector["OverallFastest"]) ? SectorTimingState.OverallBest
            : JsonSupport.Bool(sector["PersonalFastest"]) ? SectorTimingState.PersonalBest
            : SectorTimingState.Slower;
        return new(value, state);
    }

    /// <summary>
    /// The feed tags a wild-card entry's name with "(WCD)"; broadcast graphics show the name alone, so the tag is dropped.
    /// </summary>
    private static string DriverName(string? name)
    {
        var value = (name ?? "").Trim();
        const string wildCard = "(WCD)";
        return value.EndsWith(wildCard, StringComparison.OrdinalIgnoreCase) ? value[..^wildCard.Length].TrimEnd() : value;
    }

    internal static SessionLifecycle SessionLifecycleFor(string? status) => status switch
    {
        "Finished" or "Finalised" => SessionLifecycle.Ended,
        "Started" or "Running" or "Aborted" or "Suspended" => SessionLifecycle.Live,
        _ => SessionLifecycle.PreSession
    };
    private string TeamFor(string racingNumber) => (_championship, _season) switch
    {
        (Championship.Formula2, "Formula 2 2026") => F2Teams2026.GetValueOrDefault(racingNumber) ?? "",
        (Championship.Formula3, "Formula 3 2026") => F3Teams2026.GetValueOrDefault(racingNumber) ?? "",
        (Championship.F1Academy, "2026 F1 Academy") => F1AcademyTeams2026.GetValueOrDefault(racingNumber) ?? "",
        _ => ""
    };
    private string? SupportFor(string racingNumber) => (_championship, _season) switch
    {
        (Championship.F1Academy, "2026 F1 Academy") => F1AcademySupport2026.GetValueOrDefault(racingNumber),
        _ => null
    };
    internal static string BaseUrlFor(string value) => value switch
    {
        "F2" => "https://ltss.fiaformula2.com/streaming",
        "F3" => "https://ltss.fiaformula3.com/streaming",
        "F1 Academy" => "https://f2f3-prod-livetiming.azurewebsites.net/streaming",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Only F2, F3, and F1 Academy are supported.")
    };

    internal static string OriginFor(string value) => value switch
    {
        "F2" => "https://www.fiaformula2.com",
        "F3" => "https://www.fiaformula3.com",
        "F1 Academy" => "https://www.f1academy.com",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Only F2, F3, and F1 Academy are supported.")
    };

    /// <summary>The same hub base over the WebSocket scheme its <c>/connect</c> call needs.</summary>
    internal static string WebSocketBase(string value) =>
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "wss://" + value["https://".Length..]
        : value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "ws://" + value["http://".Length..]
        : value;

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
