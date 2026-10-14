// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>Polls the official MotoGP live-timing gateway for the currently active MotoGP, Moto2, or Moto3 session.</summary>
public sealed class MotoGpLiveTimingSource : ILiveTimingSource
{
    internal const string Endpoint = "https://api.pulselive.motogp.com/motogp/v1/timing-gateway/livetiming-lite";

    private readonly HttpClient _http;
    private readonly IClock _clock;
    private readonly IAsyncDelay _delay;
    private readonly IAlertArbiter _alerts;
    private readonly TimeSpan _pollInterval;
    private readonly bool _ownsHttpClient;
    private readonly MotoGpScheduleClient? _schedule;
    private readonly Championship? _targetChampionship;
    private CancellationTokenSource? _runCts;
    private Task? _run;
    private ConnectionState _connection = ConnectionState.Disconnected;
    private SessionLifecycle? _lastLifecycle;
    private string? _lastSessionKey;

    public MotoGpLiveTimingSource(IClock clock, IAlertArbiter alerts)
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }, clock, new SystemAsyncDelay(), alerts,
            TimeSpan.FromSeconds(5), true, null, new MotoGpScheduleClient()) { }

    public MotoGpLiveTimingSource(IClock clock, IAlertArbiter alerts, Championship championship)
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }, clock, new SystemAsyncDelay(), alerts,
            TimeSpan.FromSeconds(5), true, championship, new MotoGpScheduleClient()) { }

    internal MotoGpLiveTimingSource(HttpClient http, IClock clock, IAsyncDelay delay, IAlertArbiter alerts,
        TimeSpan pollInterval, bool ownsHttpClient = false, Championship? targetChampionship = null,
        MotoGpScheduleClient? schedule = null)
    {
        _http = http;
        _clock = clock;
        _delay = delay;
        _alerts = alerts;
        _pollInterval = pollInterval;
        _ownsHttpClient = ownsHttpClient;
        _targetChampionship = targetChampionship;
        _schedule = schedule;
    }

    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived;
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_run is not null) return Task.CompletedTask;
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _run = Task.Run(() => RunAsync(_runCts.Token), _runCts.Token);
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_connection != ConnectionState.Connected) ChangeConnection(ConnectionState.Connecting);
                var cacheBuster = _clock.UtcNow.ToUnixTimeMilliseconds();
                var text = await _http.GetStringAsync($"{Endpoint}?{cacheBuster}", ct);
                FeedDump.Record((_targetChampionship ?? Championship.MotoGp).ToString().ToLowerInvariant(), text);
                var payload = JsonNode.Parse(text) as JsonObject
                    ?? throw new InvalidOperationException("MotoGP live timing returned no payload.");
                ChangeConnection(ConnectionState.Connected);
                var live = ProcessPayload(payload);
                await _delay.DelayAsync(await NextPollDelayAsync(live, ct), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Failed?.Invoke(new($"MotoGP feed failed: {ex.Message}", ex, _clock.UtcNow));
                ChangeConnection(ConnectionState.Faulted);
                try { await _delay.DelayAsync(TimeSpan.FromSeconds(10), ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task<TimeSpan> NextPollDelayAsync(bool live, CancellationToken cancellationToken)
    {
        if (live || _schedule is null) return _pollInterval;
        try
        {
            var sessions = ChampionshipCalendars.NextMotorcycleGrandPrix(
                await _schedule.GetEventsAsync(_clock.UtcNow, cancellationToken), _clock.UtcNow);
            return IdleDelay(sessions, _targetChampionship, _clock.UtcNow, _pollInterval);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception failure)
        {
            Failed?.Invoke(new($"MotoGP schedule failed: {failure.Message}", failure, _clock.UtcNow));
            return TimeSpan.FromMinutes(5);
        }
    }

    internal static TimeSpan IdleDelay(IReadOnlyList<UpcomingSession> sessions, Championship? target,
        DateTimeOffset now, TimeSpan pollInterval)
    {
        var next = sessions.FirstOrDefault(session => session.StartUtc > now &&
            (target is null || session.Championship == target));
        if (next is null) return TimeSpan.FromHours(6);
        var untilLead = next.StartUtc - now - TimeSpan.FromMinutes(30);
        return untilLead > TimeSpan.Zero ? Min(untilLead, TimeSpan.FromHours(6)) : pollInterval;
    }

    internal bool ProcessPayload(JsonObject payload)
    {
        var snapshot = MotoGpTiming.BuildSnapshot(payload, _clock.UtcNow, _connection);
        if (snapshot is null || snapshot.Championship == Championship.Unknown ||
            (_targetChampionship is not null && snapshot.Championship != _targetChampionship))
        {
            _lastLifecycle = null;
            _lastSessionKey = null;
            SnapshotReceived?.Invoke(TimingSnapshot.Hidden(_clock.UtcNow));
            return false;
        }
        var key = $"{snapshot.Meeting}:{snapshot.Session}";
        if (snapshot.Lifecycle == SessionLifecycle.Live && (_lastLifecycle != SessionLifecycle.Live || _lastSessionKey != key))
            _alerts.Accept(new(AlertKind.SessionStart, 10, false, "MOTOGP SESSION STARTED", snapshot.Session, null,
                _clock.UtcNow, $"motogp:start:{key}", TimeSpan.FromSeconds(4)));
        if (snapshot.Lifecycle == SessionLifecycle.Ended && _lastLifecycle != SessionLifecycle.Ended)
            _alerts.Accept(new(AlertKind.Chequered, 90, false, "CHEQUERED FLAG", snapshot.Session, null,
                _clock.UtcNow, $"motogp:end:{key}", TimeSpan.FromSeconds(6)));
        _lastLifecycle = snapshot.Lifecycle;
        _lastSessionKey = key;
        DeltaReceived?.Invoke(new("MotoGpLite", payload, _clock.UtcNow));
        SnapshotReceived?.Invoke(snapshot);
        return snapshot.Lifecycle == SessionLifecycle.Live;
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;

    private void ChangeConnection(ConnectionState value)
    {
        if (_connection == value) return;
        _connection = value;
        ConnectionChanged?.Invoke(value);
    }

    public async Task StopAsync()
    {
        if (_runCts is null) return;
        await _runCts.CancelAsync();
        if (_run is not null) try { await _run; } catch (OperationCanceledException) { }
        _run = null;
        _runCts.Dispose();
        _runCts = null;
        ChangeConnection(ConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_ownsHttpClient) _http.Dispose();
        _schedule?.Dispose();
    }
}

internal static class MotoGpTiming
{
    internal static TimingSnapshot? BuildSnapshot(JsonObject payload, DateTimeOffset timestamp, ConnectionState connection)
    {
        var head = payload["head"] as JsonObject;
        var riders = payload["rider"] as JsonObject;
        if (head is null)
        {
            FeedDrift.Record("MotoGP", "head", payload);
            return null;
        }
        if (riders is null || riders.Count == 0)
        {
            FeedDrift.Record("MotoGP", "rider", payload);
            return null;
        }

        var standings = riders.Select(pair => BuildStanding(pair.Key, pair.Value as JsonObject))
            .Where(standing => standing is not null)
            .Cast<(CompetitorStanding Standing, int Order, double? LapTime)>()
            .OrderBy(value => value.Standing.Position)
            .ThenBy(value => value.Order)
            .ToList();
        if (standings.Count == 0)
        {
            FeedDrift.Record("MotoGP", "classified riders", payload);
            return null;
        }

        var fastest = standings.Where(value => value.LapTime.HasValue).Select(value => value.LapTime!.Value).DefaultIfEmpty(double.NaN).Min();
        var fastestIndex = double.IsNaN(fastest) ? -1 : standings.FindIndex(value => value.LapTime == fastest);
        var competitors = standings.Select((value, index) => value.Standing with
        {
            IsOverallFastest = index == fastestIndex
        }).ToList();
        var category = JsonSupport.String(head["category"]);
        var sessionName = JsonSupport.String(head["session_name"]);
        var session = Join(category, sessionName);
        var meeting = JsonSupport.String(head["event_tv_name"]) ?? JsonSupport.String(head["event_shortname"]) ?? "MotoGP";
        var totalLaps = JsonSupport.Int(head["num_laps"]);
        if (totalLaps <= 0) totalLaps = null;

        return new TimingSnapshot(
            meeting,
            string.IsNullOrWhiteSpace(session) ? "Live session" : session,
            JsonSupport.String(head["circuit_name"]) ?? "",
            competitors.Max(standing => standing.Lap),
            totalLaps,
            TrackCondition.Unknown,
            competitors,
            timestamp,
            SessionLifecycleFor(JsonSupport.String(head["session_status_id"]) ?? JsonSupport.String(head["session_status_name"])),
            connection,
            FormatRemaining(JsonSupport.Int(head["remaining"])),
            ChampionshipForCategory(category))
        {
            SessionShortName = JsonSupport.String(head["session_shortname"])
        };
    }

    internal static Championship ChampionshipForCategory(string? category) => category?.Trim().ToUpperInvariant() switch
    {
        "MOTOGP" or "MOTO GP" => Championship.MotoGp,
        "MOTO2" or "MOTO 2" => Championship.Moto2,
        "MOTO3" or "MOTO 3" => Championship.Moto3,
        _ => Championship.Unknown
    };

    internal static SessionLifecycle SessionLifecycleFor(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "S" or "R" or "RUNNING" or "STARTED" or "LIVE" => SessionLifecycle.Live,
        "F" or "E" or "C" or "FINISHED" or "FINALISED" or "CLOSED" or "ENDED" => SessionLifecycle.Ended,
        _ => SessionLifecycle.PreSession
    };

    private static (CompetitorStanding Standing, int Order, double? LapTime)? BuildStanding(string key, JsonObject? rider)
    {
        if (rider is null) return null;
        var order = JsonSupport.Int(rider["order"]) ?? int.MaxValue;
        var position = JsonSupport.Int(rider["pos"]);
        var status = JsonSupport.String(rider["status_name"]);
        var classified = position is > 0;
        position = classified ? position : order;
        if (position is null or int.MaxValue) return null;
        var lapTime = JsonSupport.String(rider["lap_time"]);
        var statusUpper = status?.Trim().ToUpperInvariant();
        var gap = FormatGap(JsonSupport.String(rider["gap_first"]));
        var team = JsonSupport.String(rider["team_name"]) ?? "";

        return (new CompetitorStanding(
            JsonSupport.String(rider["rider_id"]) ?? key,
            position.Value,
            ShortCode(JsonSupport.String(rider["rider_shortname"])) ?? JsonSupport.String(rider["rider_number"]) ?? key,
            Join(JsonSupport.String(rider["rider_name"]), JsonSupport.String(rider["rider_surname"])),
            team,
            position == 1 ? "LEAD" : gap,
            FormatGap(JsonSupport.String(rider["gap_prev"])),
            null,
            JsonSupport.Int(rider["num_lap"]) ?? 0,
            JsonSupport.Bool(rider["on_pit"]),
            statusUpper is "RET" or "DNF" or "DSQ",
            false,
            false,
            lapTime,
            status,
            classified ? null : status,
            null,
            ManufacturerFor(JsonSupport.String(rider["bike_name"]), team)), order, LapSeconds(lapTime));
    }

    internal static string? ManufacturerFor(string? bikeName, string? teamName)
    {
        var manufacturers = new[] { "KTM", "Honda", "Yamaha", "Ducati", "Aprilia", "Kalex", "Boscoscuro" };
        foreach (var manufacturer in manufacturers)
            if (bikeName?.Contains(manufacturer, StringComparison.OrdinalIgnoreCase) == true) return manufacturer;
        foreach (var manufacturer in manufacturers)
            if (teamName?.Contains(manufacturer, StringComparison.OrdinalIgnoreCase) == true) return manufacturer;
        if (teamName?.Contains("Gresini", StringComparison.OrdinalIgnoreCase) == true ||
            teamName?.Contains("VR46", StringComparison.OrdinalIgnoreCase) == true) return "Ducati";
        if (teamName?.Contains("Trackhouse", StringComparison.OrdinalIgnoreCase) == true) return "Aprilia";
        return string.IsNullOrWhiteSpace(bikeName) ? null : bikeName;
    }

    private static string? FormatGap(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "0.000") return null;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? $"+{seconds:0.000}" : value;
    }

    private static string? FormatRemaining(int? seconds)
    {
        if (seconds is null || seconds < 0) return null;
        var time = TimeSpan.FromSeconds(seconds.Value);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}" : $"{time.Minutes}:{time.Seconds:D2}";
    }

    private static double? LapSeconds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split('\'', 2);
        return parts.Length == 2 && int.TryParse(parts[0], out var minutes) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? minutes * 60 + seconds : null;
    }

    private static string Join(string? first, string? second) =>
        string.IsNullOrWhiteSpace(first) ? second ?? "" : string.IsNullOrWhiteSpace(second) ? first : $"{first} {second}";

    private static string? ShortCode(string? shortName)
    {
        if (string.IsNullOrWhiteSpace(shortName)) return null;
        var code = new string(shortName.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(code) ? null : code;
    }
}
