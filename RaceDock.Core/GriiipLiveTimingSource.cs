// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed record GriiipSeries(
    Championship Championship,
    int ProviderId,
    string ProviderName,
    string Label,
    string FeedKey,
    string FallbackEvent)
{
    public static GriiipSeries Dtm { get; } = new(
        Championship.Dtm, 14, "DTM", "DTM", "dtm", "DTM");

    public static GriiipSeries Wec { get; } = new(
        Championship.WorldEnduranceChampionship, 10, "FIA World Endurance Championship", "WEC", "wec", "WEC");

    public static GriiipSeries Elms { get; } = new(
        Championship.EuropeanLeMansSeries, 409, "European Le Mans Series", "ELMS", "elms", "ELMS");

    public static GriiipSeries MichelinLeMansCup { get; } = new(
        Championship.MichelinLeMansCup, 410, "Michelin Le Mans Cup", "Le Mans Cup", "mlmc", "Le Mans Cup");
}

internal sealed record GriiipPollingOptions(
    TimeSpan PollInterval,
    TimeSpan NearSessionInterval,
    TimeSpan ScheduleRefreshInterval,
    TimeSpan PreSessionLead,
    TimeSpan FailureInterval)
{
    public static GriiipPollingOptions Default { get; } = new(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromHours(6),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(1));
}

internal interface IGriiipApiClient : IDisposable
{
    Task<JsonArray> GetSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<JsonObject> GetBootstrapAsync(int sessionId, CancellationToken cancellationToken);
}

internal sealed class GriiipApiClient(GriiipSeries series) : IGriiipApiClient
{
    private readonly HttpClient _http = CreateHttpClient();

    public async Task<JsonArray> GetSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var date = Uri.EscapeDataString(now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        var text = await _http.GetStringAsync(
            $"meta/sessions?dateTime={date}&forward=true&seriesIds={series.ProviderId}", cancellationToken);
        FeedDump.Record(series.FeedKey, text);
        return JsonNode.Parse(text) as JsonArray ??
            throw new InvalidDataException($"{series.Label} session discovery returned invalid JSON.");
    }

    public async Task<JsonObject> GetBootstrapAsync(int sessionId, CancellationToken cancellationToken)
    {
        var text = await _http.GetStringAsync(
            $"api/v2/public/live/session/{sessionId}/bootstrap?includeViewers=false&includeUnclassifiedRanks=false",
            cancellationToken);
        FeedDump.Record(series.FeedKey, text);
        return JsonNode.Parse(text) as JsonObject ??
            throw new InvalidDataException($"{series.Label} live timing returned invalid JSON.");
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://insights.griiip.com/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RaceDock", "1.0"));
        return client;
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>Polls a public Griiip timing series and maps it into RaceDock snapshots.</summary>
public abstract class GriiipLiveTimingSource : ILiveTimingSource
{
    private readonly IGriiipApiClient _api;
    private readonly GriiipSeries _series;
    private readonly IClock _clock;
    private readonly IAsyncDelay _delay;
    private readonly IAlertArbiter _alerts;
    private readonly GriiipPollingOptions _options;
    private readonly GriiipSectorTiming _sectorTiming = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private CancellationTokenSource? _runCts;
    private Task? _worker;
    private ConnectionState? _connection;
    private int? _sessionId;
    private bool _sessionStarted;
    private bool? _observedEnded;
    private TrackCondition _trackCondition = TrackCondition.Unknown;
    private string? _lastFingerprint;
    private bool _disposed;

    internal GriiipLiveTimingSource(
        IGriiipApiClient api,
        GriiipSeries series,
        IClock clock,
        IAsyncDelay delay,
        IAlertArbiter alerts,
        GriiipPollingOptions options)
    {
        _api = api;
        _series = series;
        _clock = clock;
        _delay = delay;
        _alerts = alerts;
        _options = options;
    }

    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived;
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_worker is not null) return;
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _worker = Task.Run(() => RunAsync(_runCts.Token), _runCts.Token);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception failure)
            {
                ResetSession();
                SetConnection(ConnectionState.Faulted);
                PublishHidden(ConnectionState.Faulted);
                Failed?.Invoke(new($"{_series.Label} live timing failed: {failure.Message}", failure, _clock.UtcNow));
                try { await _delay.DelayAsync(_options.FailureInterval, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        if (_connection is null or ConnectionState.Disconnected or ConnectionState.Faulted)
            SetConnection(ConnectionState.Connecting);
        var sessions = await _api.GetSessionsAsync(_clock.UtcNow, cancellationToken);
        var session = ActiveSession(sessions, _series);
        if (session is null || JsonSupport.Int(session["id"]) is not { } sessionId)
        {
            ResetSession();
            SetConnection(ConnectionState.Disconnected);
            PublishHidden(ConnectionState.Disconnected);
            await _delay.DelayAsync(NextIdleDelay(sessions, _series, _clock.UtcNow, _options), cancellationToken);
            return;
        }

        if (_sessionId != sessionId)
        {
            ResetSession();
            _sessionId = sessionId;
        }
        var bootstrap = await _api.GetBootstrapAsync(sessionId, cancellationToken);
        var snapshot = GriiipTiming.BuildSnapshot(session, bootstrap, _clock.UtcNow, ConnectionState.Connected, _series, _sectorTiming);
        SetConnection(ConnectionState.Connected);
        ProcessAlerts(snapshot);
        PublishDelta(bootstrap);
        SnapshotReceived?.Invoke(snapshot);
        await _delay.DelayAsync(snapshot.Lifecycle == SessionLifecycle.Live
            ? _options.PollInterval
            : _options.NearSessionInterval, cancellationToken);
    }

    private void PublishDelta(JsonObject bootstrap)
    {
        var fingerprint = bootstrap.ToJsonString();
        if (fingerprint == _lastFingerprint) return;
        _lastFingerprint = fingerprint;
        DeltaReceived?.Invoke(new($"{_series.Label}Timing", bootstrap.DeepClone(), _clock.UtcNow));
    }

    internal static TimeSpan NextIdleDelay(JsonArray sessions, GriiipSeries series, DateTimeOffset now,
        GriiipPollingOptions options)
    {
        var windows = sessions.OfType<JsonObject>()
            .Where(session => !JsonSupport.Bool(session["hideFromUsers"]) &&
                string.Equals(JsonSupport.String(session["series"]?["name"]), series.ProviderName, StringComparison.OrdinalIgnoreCase))
            .Select(session => (Start: ParseDate(session["startTime"]), End: ParseDate(session["endTime"])))
            .Where(window => window.Start != default)
            .ToList();
        // isRunning can lag behind the scheduled start. Keep discovering throughout the published window rather
        // than sleeping until tomorrow's session; without an end time, use the lead interval as a bounded grace.
        if (windows.Any(window => window.Start <= now &&
            now < (window.End > window.Start ? window.End : window.Start + options.PreSessionLead)))
            return options.NearSessionInterval;
        var next = windows.Select(window => window.Start).Where(start => start > now).OrderBy(start => start).FirstOrDefault();
        if (next == default) return options.ScheduleRefreshInterval;
        var untilLead = next - now - options.PreSessionLead;
        if (untilLead <= TimeSpan.Zero) return options.NearSessionInterval;
        return untilLead <= options.ScheduleRefreshInterval ? untilLead : options.ScheduleRefreshInterval;
    }

    internal static JsonObject? ActiveSession(JsonArray sessions, GriiipSeries series) => sessions
        .OfType<JsonObject>()
        .Where(session => !JsonSupport.Bool(session["hideFromUsers"]) &&
            string.Equals(JsonSupport.String(session["series"]?["name"]), series.ProviderName, StringComparison.OrdinalIgnoreCase))
        .Where(session => JsonSupport.Bool(session["isRunning"]))
        .OrderByDescending(session => ParseDate(session["startTime"]))
        .FirstOrDefault();

    private void ProcessAlerts(TimingSnapshot snapshot)
    {
        if (!_sessionStarted && snapshot.Lifecycle == SessionLifecycle.Live)
        {
            _sessionStarted = true;
            _alerts.Accept(new(AlertKind.SessionStart, 10, false, $"{_series.Label} SESSION STARTED", snapshot.Session,
                snapshot.CurrentLap, _clock.UtcNow, $"{_series.FeedKey}:{_sessionId}:start", TimeSpan.FromSeconds(4)));
        }
        if (snapshot.TrackCondition != _trackCondition)
        {
            _trackCondition = snapshot.TrackCondition;
            if (_trackCondition != TrackCondition.Unknown)
                _alerts.ApplyTrackCondition(_trackCondition, null, snapshot.CurrentLap, _clock.UtcNow);
        }
        var ended = snapshot.Lifecycle == SessionLifecycle.Ended;
        if (_observedEnded == false && ended)
            _alerts.Accept(new(AlertKind.Chequered, 90, false, "CHEQUERED FLAG", snapshot.Session,
                snapshot.CurrentLap, _clock.UtcNow, $"{_series.FeedKey}:{_sessionId}:chequered", TimeSpan.FromSeconds(6)));
        _observedEnded = ended;
    }

    private void ResetSession()
    {
        _sectorTiming.Reset();
        _sessionId = null;
        _sessionStarted = false;
        _observedEnded = null;
        _trackCondition = TrackCondition.Unknown;
        _lastFingerprint = null;
    }

    private static DateTimeOffset ParseDate(JsonNode? value) =>
        DateTimeOffset.TryParse(JsonSupport.String(value), out var parsed) ? parsed : DateTimeOffset.MinValue;

    private void PublishHidden(ConnectionState connection) => SnapshotReceived?.Invoke(new(
        "", "", "", 0, null, TrackCondition.Unknown, [], _clock.UtcNow,
        SessionLifecycle.OffSession, connection, Championship: _series.Championship));

    private void SetConnection(ConnectionState state)
    {
        if (_connection == state) return;
        _connection = state;
        ConnectionChanged?.Invoke(state);
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (_worker is null) return;
            if (_runCts is not null) await _runCts.CancelAsync();
            try { await _worker; }
            catch (OperationCanceledException) { }
            _worker = null;
            _runCts?.Dispose();
            _runCts = null;
            ResetSession();
            SetConnection(ConnectionState.Disconnected);
        }
        finally { _lifecycle.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            if (_worker is not null)
            {
                if (_runCts is not null) await _runCts.CancelAsync();
                try { await _worker; }
                catch (OperationCanceledException) { }
                _worker = null;
            }
            _runCts?.Dispose();
            _runCts = null;
            _api.Dispose();
            ResetSession();
            SetConnection(ConnectionState.Disconnected);
        }
        finally { _lifecycle.Release(); }
    }
}
