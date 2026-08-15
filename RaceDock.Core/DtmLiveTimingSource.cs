// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed record DtmPollingOptions(TimeSpan PollInterval, TimeSpan IdleInterval, TimeSpan FailureInterval)
{
    public static DtmPollingOptions Default { get; } = new(
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5));
}

internal interface IDtmApiClient : IDisposable
{
    Task<JsonArray> GetSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<JsonObject> GetBootstrapAsync(int sessionId, CancellationToken cancellationToken);
}

internal sealed class DtmApiClient : IDtmApiClient
{
    private readonly HttpClient _http;

    public DtmApiClient()
    {
        _http = new HttpClient { BaseAddress = new Uri("https://insights.griiip.com/"), Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RaceDock", "1.0"));
    }

    public async Task<JsonArray> GetSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var date = Uri.EscapeDataString(now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        var text = await _http.GetStringAsync($"meta/sessions?dateTime={date}&forward=true&seriesIds=14", cancellationToken);
        return JsonNode.Parse(text) as JsonArray ?? throw new InvalidDataException("DTM session discovery returned invalid JSON.");
    }

    public async Task<JsonObject> GetBootstrapAsync(int sessionId, CancellationToken cancellationToken)
    {
        var text = await _http.GetStringAsync(
            $"api/v2/public/live/session/{sessionId}/bootstrap?includeViewers=false&includeUnclassifiedRanks=false",
            cancellationToken);
        return JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException("DTM live timing returned invalid JSON.");
    }

    public void Dispose() => _http.Dispose();
}

public sealed class DtmLiveTimingSource : ILiveTimingSource
{
    private readonly IDtmApiClient _api;
    private readonly IClock _clock;
    private readonly IAsyncDelay _delay;
    private readonly IAlertArbiter _alerts;
    private readonly DtmPollingOptions _options;
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

    public DtmLiveTimingSource(IClock clock, IAlertArbiter alerts)
        : this(new DtmApiClient(), clock, new SystemAsyncDelay(), alerts, DtmPollingOptions.Default) { }

    internal DtmLiveTimingSource(
        IDtmApiClient api,
        IClock clock,
        IAsyncDelay delay,
        IAlertArbiter alerts,
        DtmPollingOptions options)
    {
        _api = api;
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
                if (_connection is null or ConnectionState.Disconnected or ConnectionState.Faulted)
                    SetConnection(ConnectionState.Connecting);
                var sessions = await _api.GetSessionsAsync(_clock.UtcNow, cancellationToken);
                var session = ActiveSession(sessions);
                if (session is null || JsonSupport.Int(session["id"]) is not { } sessionId)
                {
                    ResetSession();
                    SetConnection(ConnectionState.Disconnected);
                    PublishHidden(ConnectionState.Disconnected);
                    await _delay.DelayAsync(_options.IdleInterval, cancellationToken);
                    continue;
                }

                if (_sessionId != sessionId)
                {
                    ResetSession();
                    _sessionId = sessionId;
                }
                var bootstrap = await _api.GetBootstrapAsync(sessionId, cancellationToken);
                var snapshot = DtmTiming.BuildSnapshot(session, bootstrap, _clock.UtcNow, ConnectionState.Connected);
                SetConnection(ConnectionState.Connected);
                ProcessAlerts(snapshot);
                var fingerprint = bootstrap.ToJsonString();
                if (fingerprint != _lastFingerprint)
                {
                    _lastFingerprint = fingerprint;
                    DeltaReceived?.Invoke(new("DtmTiming", bootstrap.DeepClone(), _clock.UtcNow));
                }
                SnapshotReceived?.Invoke(snapshot);
                await _delay.DelayAsync(snapshot.Lifecycle == SessionLifecycle.Live
                    ? _options.PollInterval
                    : _options.IdleInterval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                ResetSession();
                SetConnection(ConnectionState.Faulted);
                PublishHidden(ConnectionState.Faulted);
                Failed?.Invoke(new($"DTM live timing failed: {ex.Message}", ex, _clock.UtcNow));
                try { await _delay.DelayAsync(_options.FailureInterval, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            }
        }
    }

    internal static JsonObject? ActiveSession(JsonArray sessions) => sessions
        .OfType<JsonObject>()
        .Where(session => !JsonSupport.Bool(session["hideFromUsers"]) &&
            string.Equals(JsonSupport.String(session["series"]?["name"]), "DTM", StringComparison.OrdinalIgnoreCase))
        .Where(session => JsonSupport.Bool(session["isRunning"]))
        .OrderByDescending(session => ParseDate(session["startTime"]))
        .FirstOrDefault();

    private void ProcessAlerts(TimingSnapshot snapshot)
    {
        if (!_sessionStarted && snapshot.Lifecycle == SessionLifecycle.Live)
        {
            _sessionStarted = true;
            _alerts.Accept(new(AlertKind.SessionStart, 10, false, "DTM SESSION STARTED", snapshot.Session,
                snapshot.CurrentLap, _clock.UtcNow, $"dtm:{_sessionId}:start", TimeSpan.FromSeconds(4)));
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
                snapshot.CurrentLap, _clock.UtcNow, $"dtm:{_sessionId}:chequered", TimeSpan.FromSeconds(6)));
        _observedEnded = ended;
    }

    private void ResetSession()
    {
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
        SessionLifecycle.OffSession, connection, Championship: Championship.Dtm));

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
