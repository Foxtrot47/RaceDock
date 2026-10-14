// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed record IndyCarPollingOptions(
    TimeSpan PollInterval,
    TimeSpan IdleInterval,
    TimeSpan FailureInterval,
    TimeSpan StaleAfter)
{
    public static IndyCarPollingOptions Default { get; } = new(
        TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
}

internal sealed record IndyCarTimingPayload(JsonObject Data, DateTimeOffset? LastModified);

internal interface IIndyCarApiClient : IDisposable
{
    Task<IndyCarTimingPayload> GetTimingAsync(CancellationToken cancellationToken);
}

internal sealed class IndyCarApiClient : IIndyCarApiClient
{
    private readonly HttpClient _http;

    public IndyCarApiClient()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://indycar.blob.core.windows.net/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RaceDock", "1.0"));
    }

    public async Task<IndyCarTimingPayload> GetTimingAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "racecontrol/timingscoring-ris.json");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        FeedDump.Record("indycar", text);
        var payload = JsonNode.Parse(text) as JsonObject ??
            throw new InvalidDataException("INDYCAR live timing returned invalid JSON.");
        return new(payload, response.Content.Headers.LastModified);
    }

    public void Dispose() => _http.Dispose();
}

public sealed class IndyCarLiveTimingSource : ILiveTimingSource
{
    private readonly IIndyCarApiClient _api;
    private readonly IClock _clock;
    private readonly IAsyncDelay _delay;
    private readonly IAlertArbiter _alerts;
    private readonly IndyCarPollingOptions _options;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private CancellationTokenSource? _runCts;
    private Task? _worker;
    private ConnectionState? _connection;
    private string? _sessionKey;
    private bool _sessionStarted;
    private bool? _observedEnded;
    private TrackCondition _trackCondition = TrackCondition.Unknown;
    private string? _lastFingerprint;
    private bool _disposed;

    public IndyCarLiveTimingSource(IClock clock, IAlertArbiter alerts)
        : this(new IndyCarApiClient(), clock, new SystemAsyncDelay(), alerts, IndyCarPollingOptions.Default) { }

    internal IndyCarLiveTimingSource(
        IIndyCarApiClient api,
        IClock clock,
        IAsyncDelay delay,
        IAlertArbiter alerts,
        IndyCarPollingOptions options)
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
                var payload = await _api.GetTimingAsync(cancellationToken);
                var snapshot = IndyCarTiming.BuildSnapshot(payload.Data, _clock.UtcNow, ConnectionState.Connected);
                if (snapshot.Lifecycle == SessionLifecycle.Live && payload.LastModified is { } modified &&
                    _clock.UtcNow - modified > _options.StaleAfter)
                {
                    ResetSession();
                    SetConnection(ConnectionState.Stale);
                    PublishHidden(ConnectionState.Stale);
                    await _delay.DelayAsync(_options.IdleInterval, cancellationToken);
                    continue;
                }

                SetConnection(ConnectionState.Connected);
                if (snapshot.Lifecycle == SessionLifecycle.OffSession)
                {
                    ResetSession();
                    PublishHidden(ConnectionState.Connected);
                    await _delay.DelayAsync(_options.IdleInterval, cancellationToken);
                    continue;
                }

                var sessionKey = IndyCarTiming.SessionKey(payload.Data);
                if (!string.Equals(_sessionKey, sessionKey, StringComparison.Ordinal))
                {
                    ResetSession();
                    _sessionKey = sessionKey;
                }
                ProcessAlerts(snapshot);
                var fingerprint = payload.Data.ToJsonString();
                if (fingerprint != _lastFingerprint)
                {
                    _lastFingerprint = fingerprint;
                    DeltaReceived?.Invoke(new("IndyCarTiming", payload.Data.DeepClone(), _clock.UtcNow));
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
                Failed?.Invoke(new($"INDYCAR live timing failed: {ex.Message}", ex, _clock.UtcNow));
                try { await _delay.DelayAsync(_options.FailureInterval, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            }
        }
    }

    private void ProcessAlerts(TimingSnapshot snapshot)
    {
        if (!_sessionStarted && snapshot.Lifecycle == SessionLifecycle.Live)
        {
            _sessionStarted = true;
            _alerts.Accept(new(AlertKind.SessionStart, 10, false, "INDYCAR SESSION STARTED", snapshot.Session,
                snapshot.CurrentLap, _clock.UtcNow, $"indycar:{_sessionKey}:start", TimeSpan.FromSeconds(4)));
        }
        if (snapshot.TrackCondition != _trackCondition)
        {
            _trackCondition = snapshot.TrackCondition;
            if (_trackCondition != TrackCondition.Unknown)
                _alerts.ApplyTrackCondition(_trackCondition, null, snapshot.CurrentLap, _clock.UtcNow);
        }
        var ended = snapshot.Lifecycle == SessionLifecycle.Ended;
        if (_observedEnded == false && ended)
            _alerts.Accept(new(AlertKind.Chequered, 90, false, "CHECKERED FLAG", snapshot.Session,
                snapshot.CurrentLap, _clock.UtcNow, $"indycar:{_sessionKey}:checkered", TimeSpan.FromSeconds(6)));
        _observedEnded = ended;
    }

    private void ResetSession()
    {
        _sessionKey = null;
        _sessionStarted = false;
        _observedEnded = null;
        _trackCondition = TrackCondition.Unknown;
        _lastFingerprint = null;
    }

    private void PublishHidden(ConnectionState connection) => SnapshotReceived?.Invoke(new(
        "", "", "", 0, null, TrackCondition.Unknown, [], _clock.UtcNow,
        SessionLifecycle.OffSession, connection, Championship: Championship.IndyCar));

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
