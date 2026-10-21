// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace RaceDock.Core;

public sealed class GtwcLiveTimingSource : ILiveTimingSource
{
    private readonly GtwcApiClient _api;
    private readonly IClock _clock;
    private readonly IAsyncDelay _delay;
    private readonly IAlertArbiter _alerts;
    private readonly GtwcPollingOptions _options;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private CancellationTokenSource? _runCts;
    private Task? _worker;
    private ConnectionState? _connection;
    private string? _currentUnitId;
    private bool _sessionStarted;
    private bool? _observedChequered;
    private TrackCondition _trackCondition = TrackCondition.Unknown;
    private string? _lastFingerprint;
    private bool _disposed;

    public GtwcLiveTimingSource(IClock clock, IAlertArbiter alerts)
        : this(new GtwcApiClient(), clock, new SystemAsyncDelay(), alerts, GtwcPollingOptions.Default) { }

    internal GtwcLiveTimingSource(GtwcApiClient api, IClock clock, IAsyncDelay delay, IAlertArbiter alerts, GtwcPollingOptions options)
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

    public async Task StopAsync()
    {
        Task? worker;
        await _lifecycle.WaitAsync();
        try
        {
            if (_worker is null) return;
            if (_runCts is not null) await _runCts.CancelAsync();
            worker = _worker;
        }
        finally { _lifecycle.Release(); }

        try { await worker; }
        catch (OperationCanceledException) { }

        await _lifecycle.WaitAsync();
        try
        {
            _worker = null;
            _runCts?.Dispose();
            _runCts = null;
            ResetUnitState();
            SetConnection(ConnectionState.Disconnected);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var discoveryAttempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                SetConnection(ConnectionState.Connecting);
                var discovery = await DiscoverAsync(ct);
                discoveryAttempt = 0;
                if (discovery.Meeting is null || discovery.Meeting.State != 1 || discovery.Unit is null || discovery.Unit.State != 1)
                {
                    ResetUnitState();
                    PublishHidden(ConnectionState.Disconnected);
                    SetConnection(ConnectionState.Disconnected);
                    var delay = discovery.Meeting is { State: 1 }
                        ? _options.ScheduleRefreshInterval
                        : _options.IdlePollInterval;
                    await _delay.DelayAsync(delay, ct);
                    continue;
                }

                await MonitorUnitAsync(discovery, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                ResetUnitState();
                PublishFailure("GTWC discovery failed", ex);
                await _delay.DelayAsync(Backoff(_options.DiscoveryBackoff, discoveryAttempt++), ct);
            }
        }
    }

    private async Task<GtwcDiscovery> DiscoverAsync(CancellationToken ct)
    {
        var seasons = await _api.GetSeasonsAsync(ct);
        var year = GtwcTiming.CurrentSeason(seasons.Full, _clock.UtcNow.Year)
            ?? throw new InvalidOperationException("GTWC current season was missing.");
        var season = await _api.GetSeasonAsync(year, ct);
        var meeting = GtwcTiming.PresentationMeeting(season.Full);
        if (meeting is null || meeting.State != 1) return new(year, meeting, null);
        var schedule = await _api.GetScheduleAsync(year, meeting.Id, ct);
        var returnedMeetingId = JsonSupport.String(schedule.Full["MeetingId"]);
        if (!string.Equals(returnedMeetingId, meeting.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"GTWC schedule returned meeting {returnedMeetingId ?? "—"} instead of {meeting.Id}.");
        return new(year, meeting, GtwcTiming.PresentedUnit(schedule.Full));
    }

    private async Task MonitorUnitAsync(GtwcDiscovery discovery, CancellationToken ct)
    {
        var meeting = discovery.Meeting!;
        var unit = discovery.Unit!;
        if (!string.Equals(_currentUnitId, unit.Id, StringComparison.OrdinalIgnoreCase))
        {
            ResetUnitState();
            _currentUnitId = unit.Id;
        }
        var nextScheduleRefresh = _clock.UtcNow + _options.ScheduleRefreshInterval;
        var pollAttempt = 0;
        var consecutiveFailures = 0;

        while (!ct.IsCancellationRequested)
        {
            // The published session can move on while this unit keeps failing, so the schedule is re-read on its own
            // cadence rather than only after a poll that may never succeed again.
            if (_clock.UtcNow >= nextScheduleRefresh)
            {
                nextScheduleRefresh = _clock.UtcNow + _options.ScheduleRefreshInterval;
                var refreshed = await DiscoverAsync(ct);
                if (refreshed.Meeting is null || refreshed.Unit is null || refreshed.Unit.State != 1 ||
                    !string.Equals(refreshed.Meeting.Id, meeting.Id, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(refreshed.Unit.Id, unit.Id, StringComparison.OrdinalIgnoreCase)) return;
                meeting = refreshed.Meeting;
                unit = refreshed.Unit;
                consecutiveFailures = 0;
            }

            try
            {
                var timingTask = _api.GetTimingAsync(discovery.Year, unit.Id, ct);
                var detailTask = _api.GetCompetitionDetailAsync(discovery.Year, unit.Id, ct);
                await Task.WhenAll(timingTask, detailTask);
                var timing = await timingTask;
                var detail = await detailTask;
                var snapshot = GtwcTiming.BuildSnapshot(meeting, unit, timing.Full, detail.Full, _clock.UtcNow, ConnectionState.Connected);
                ProcessAlerts(meeting, unit, timing.Full, snapshot);
                var fingerprint = $"{timing.Hash}:{detail.Hash}";
                if (fingerprint != _lastFingerprint)
                {
                    _lastFingerprint = fingerprint;
                    DeltaReceived?.Invoke(new("GtwcTiming", timing.Full.DeepClone(), _clock.UtcNow));
                }
                pollAttempt = 0;
                consecutiveFailures = 0;
                SetConnection(ConnectionState.Connected);
                SnapshotReceived?.Invoke(snapshot);
                if (snapshot.Lifecycle == SessionLifecycle.Ended)
                {
                    await _delay.DelayAsync(_options.ScheduleRefreshInterval, ct);
                    return;
                }

                await _delay.DelayAsync(_options.PollInterval, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                PublishFailure($"GTWC unit {unit.Id} polling failed", ex);
                SetConnection(ConnectionState.Connecting);
                // A unit that keeps failing has most likely been replaced at the end of its session, so the next pass
                // rediscovers instead of retrying a unit that is gone.
                if (++consecutiveFailures >= FailureLimit(_options))
                    nextScheduleRefresh = _clock.UtcNow;
                await _delay.DelayAsync(Backoff(_options.PollBackoff, pollAttempt++), ct);
            }
        }
    }

    private static int FailureLimit(GtwcPollingOptions options) => Math.Max(1, options.PollBackoff.Count);

    private void ProcessAlerts(GtwcMeeting meeting, GtwcUnit unit, JsonObject timing, TimingSnapshot snapshot)
    {
        if (!_sessionStarted && snapshot.Lifecycle == SessionLifecycle.Live)
        {
            _sessionStarted = true;
            _alerts.Accept(new(AlertKind.SessionStart, 10, false, "GTWC SESSION STARTED", unit.Name, null,
                _clock.UtcNow, $"gtwc:{meeting.Id}:{unit.Id}:start", TimeSpan.FromSeconds(4)));
        }

        var info = timing["UntInfo"] as JsonObject ?? [];
        var track = GtwcTiming.TrackState(info);
        if (track.Condition != _trackCondition)
        {
            _trackCondition = track.Condition;
            if (track.Condition != TrackCondition.Unknown)
                _alerts.ApplyTrackCondition(track.Condition, track.Detail, snapshot.CurrentLap, _clock.UtcNow);
        }

        var chequered = JsonSupport.Bool(info["ChequeredFlag"]);
        if (_observedChequered == false && chequered)
            _alerts.Accept(new(AlertKind.Chequered, 90, false, "CHEQUERED FLAG", unit.Name, snapshot.CurrentLap,
                _clock.UtcNow, $"gtwc:{meeting.Id}:{unit.Id}:chequered", TimeSpan.FromSeconds(6)));
        _observedChequered = chequered;
    }

    private void PublishFailure(string message, Exception ex)
    {
        PublishHidden(ConnectionState.Faulted);
        SetConnection(ConnectionState.Faulted);
        Failed?.Invoke(new($"{message}: {ex.Message}", ex, _clock.UtcNow));
    }

    private void PublishHidden(ConnectionState connection) => SnapshotReceived?.Invoke(new(
        "", "", "", 0, null, TrackCondition.Unknown, [], _clock.UtcNow,
        SessionLifecycle.OffSession, connection, Championship: Championship.GtWorldChallengeEurope));

    private void SetConnection(ConnectionState value)
    {
        if (_connection == value) return;
        _connection = value;
        ConnectionChanged?.Invoke(value);
    }

    private void ResetUnitState()
    {
        _currentUnitId = null;
        _sessionStarted = false;
        _observedChequered = null;
        _trackCondition = TrackCondition.Unknown;
        _lastFingerprint = null;
    }

    private static TimeSpan Backoff(IReadOnlyList<TimeSpan> values, int attempt) =>
        values.Count == 0 ? TimeSpan.FromSeconds(1) : values[Math.Min(attempt, values.Count - 1)];

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync();
        _disposed = true;
        _api.Dispose();
        _lifecycle.Dispose();
    }
}
