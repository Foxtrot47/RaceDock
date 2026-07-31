// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace RaceDock.Core;

/// <summary>Discovers and polls the current World Rally Championship event.</summary>
public sealed class WrcLiveTimingSource : ILiveTimingSource
{
    private readonly WrcApiClient _api;
    private readonly IClock _clock;
    private readonly IAsyncDelay _delay;
    private readonly IAlertArbiter _alerts;
    private readonly WrcPollingOptions _options;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private CancellationTokenSource? _runCts;
    private Task? _worker;
    private ConnectionState? _connection;
    private int? _activeStageId;
    private readonly HashSet<int> _completedStages = [];
    private readonly HashSet<int> _alertedStages = [];
    private string? _settledResultFingerprint;
    private int _settledResultPolls;
    private readonly Dictionary<int, string> _shakedownFingerprints = [];
    private IReadOnlyList<WrcStage> _knownStages = [];
    private int _knownShakedownCount;
    private DateTimeOffset? _lastShakedownChange;
    private string? _lastDeltaFingerprint;
    private bool _disposed;

    public WrcLiveTimingSource(IClock clock, IAlertArbiter alerts)
        : this(new WrcApiClient(), clock, new SystemAsyncDelay(), alerts, WrcPollingOptions.Default) { }

    internal WrcLiveTimingSource(WrcApiClient api, IClock clock, IAsyncDelay delay, IAlertArbiter alerts, WrcPollingOptions options)
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
            ResetEventState();
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
                var selection = await DiscoverEventAsync(ct);
                discoveryAttempt = 0;
                if (selection.Current is null)
                {
                    ResetEventState();
                    TransitionHidden(ConnectionState.Disconnected, ct);
                    var wait = _options.ScheduleRefreshInterval;
                    if (selection.Next is not null)
                    {
                        var untilStart = selection.Next.StartUtc - _clock.UtcNow;
                        if (untilStart > TimeSpan.Zero && untilStart < wait) wait = untilStart;
                    }
                    await _delay.DelayAsync(PositiveDelay(wait), ct);
                    continue;
                }

                await MonitorEventAsync(selection.Current, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                ResetEventState();
                PublishFailure("WRC discovery failed", ex, ct);
                var wait = Backoff(_options.DiscoveryBackoff, discoveryAttempt++);
                await _delay.DelayAsync(wait, ct);
            }
        }
    }

    private async Task<WrcEventSelection> DiscoverEventAsync(CancellationToken ct)
    {
        var seasons = WrcTiming.OrderSeasons(await _api.GetSeasonsAsync(ct), _clock.UtcNow.Year);
        WrcCalendarEvent? next = null;
        foreach (var season in seasons)
        {
            var selection = WrcTiming.SelectEvent(await _api.GetSeasonEventsAsync(season.SeasonId, ct), _clock.UtcNow);
            if (selection.Current is not null) return selection;
            if (selection.Next is not null && (next is null || selection.Next.StartUtc < next.StartUtc)) next = selection.Next;
            // A previous-season rally could overlap the new year, but current/future season priority is retained for now.
            if (season.Year >= _clock.UtcNow.Year && next is not null) return new(null, next);
        }
        return new(null, next);
    }

    private async Task MonitorEventAsync(WrcCalendarEvent candidate, CancellationToken ct)
    {
        SetConnection(ConnectionState.Connecting, ct);
        var context = await LoadEventContextAsync(candidate.EventId, ct);
        ResetActivityState();
        if (_clock.UtcNow < context.Event.StartUtc)
        {
            TransitionHidden(ConnectionState.Disconnected, ct);
            await _delay.DelayAsync(PositiveDelay(Min(context.Event.StartUtc - _clock.UtcNow, _options.ScheduleRefreshInterval)), ct);
            return;
        }
        if (_clock.UtcNow >= context.Event.EndUtc)
        {
            TransitionHidden(ConnectionState.Disconnected, ct);
            await _delay.DelayAsync(_options.ScheduleRefreshInterval, ct);
            return;
        }
        TransitionHidden(ConnectionState.Connected, ct);
        var nextMetadataRefresh = _clock.UtcNow + _options.MetadataRefreshInterval;
        var pollAttempt = 0;

        while (!ct.IsCancellationRequested && _clock.UtcNow < context.Event.EndUtc)
        {
            try
            {
                if (_clock.UtcNow >= nextMetadataRefresh)
                {
                    SetConnection(ConnectionState.Connecting, ct);
                    context = await LoadEventContextAsync(candidate.EventId, ct);
                    nextMetadataRefresh = _clock.UtcNow + _options.MetadataRefreshInterval;
                }
                var activityActive = await PollEventAsync(context, ct);
                // The first successful poll after one or more failures runs one normal-cadence confirmation
                // poll before returning to idle, so recovery is verified without waiting a full idle interval.
                var recovered = pollAttempt > 0;
                pollAttempt = 0;
                SetConnection(ConnectionState.Connected, ct);
                var nextStage = WrcTiming.SelectNextScheduledStage(_knownStages, _clock.UtcNow);
                var delay = WrcTiming.NextPollDelay(_clock.UtcNow, activityActive || recovered, nextStage,
                    nextMetadataRefresh, context.Event.EndUtc, _options);
                await _delay.DelayAsync(PositiveDelay(delay), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                PublishFailure($"WRC event {context.Event.EventId} polling failed", ex, ct);
                if (_clock.UtcNow >= context.Event.EndUtc) break;
                SetConnection(ConnectionState.Connecting, ct);
                await _delay.DelayAsync(Backoff(_options.PollBackoff, pollAttempt++), ct);
            }
        }

        ResetEventState();
        TransitionHidden(ConnectionState.Disconnected, ct);
    }

    private async Task<WrcEventContext> LoadEventContextAsync(int eventId, CancellationToken ct)
    {
        var detail = await _api.GetEventAsync(eventId, ct);
        var rally = WrcTiming.SelectMainRally(detail.Rallies);
        var entries = await _api.GetEntriesAsync(detail.EventId, rally.RallyId, ct);
        return new(detail, rally, entries);
    }

    private async Task<bool> PollEventAsync(WrcEventContext context, CancellationToken ct)
    {
        if (_clock.UtcNow >= context.Event.EndUtc)
        {
            PublishHidden(ConnectionState.Disconnected, ct);
            return false;
        }

        var stages = await _api.GetStagesAsync(context.Event.EventId, ct);
        _knownStages = stages;
        WrcStage? selected = WrcTiming.SelectRunningStage(stages);
        IReadOnlyList<WrcStageTime>? times = null;
        IReadOnlyList<WrcStageResult>? results = null;

        // Once the WRC1 field has finished a stage we settle and hide it, so support or national classes
        // still on the road cannot keep a finished stage on screen even while the provider reports Running.
        // A settled stage only reopens if the WRC1 results themselves regress — a genuine re-run — which we
        // detect by re-reading its data and clearing the completion marker when WRC1 is no longer complete.
        if (selected is { Status: WrcStageStatus.Running } && _completedStages.Contains(selected.StageId))
        {
            var reopenTimes = await _api.GetStageTimesAsync(context.Event.EventId, selected.StageId, context.Rally.RallyId, ct);
            var reopenResults = await _api.GetStageResultsAsync(context.Event.EventId, selected.StageId, context.Rally.RallyId, ct);
            if (!WrcTiming.Wrc1ResultsComplete(context.Entries, reopenTimes, reopenResults))
            {
                _completedStages.Remove(selected.StageId);
                (times, results) = (reopenTimes, reopenResults);
            }
        }

        if (selected is null && _activeStageId is int activeId)
        {
            var retained = stages.FirstOrDefault(stage => stage.StageId == activeId);
            if (retained?.Status == WrcStageStatus.Completed) selected = retained;
            else if (retained?.Status is WrcStageStatus.Interrupted or WrcStageStatus.Cancelled) _activeStageId = null;
        }

        if (selected is null)
        {
            foreach (var due in WrcTiming.DueStages(stages, _clock.UtcNow).Take(3))
            {
                if (_completedStages.Contains(due.StageId)) continue;
                var candidateTimes = await _api.GetStageTimesAsync(context.Event.EventId, due.StageId, context.Rally.RallyId, ct);
                if (candidateTimes.Count == 0) continue;
                selected = due;
                times = candidateTimes;
                break;
            }
        }

        if (selected is not null && !_completedStages.Contains(selected.StageId))
        {
            if (times is null && results is null) (times, results) = await GetStageDataAsync(context, selected.StageId, ct);
            else
            {
                times ??= await _api.GetStageTimesAsync(context.Event.EventId, selected.StageId, context.Rally.RallyId, ct);
                results ??= await _api.GetStageResultsAsync(context.Event.EventId, selected.StageId, context.Rally.RallyId, ct);
            }
            var fingerprint = WrcTiming.StageFingerprint(selected, times, results);
            if (ResultsSettled(context, times, results, fingerprint))
            {
                CompleteStage(selected.StageId);
                PublishHidden(ConnectionState.Connected, ct);
                return await PollShakedownAsync(context, ct);
            }
            PublishStage(context, selected, times, fingerprint, ct);
            return true;
        }

        ResetStageState();
        var shakedownActive = await PollShakedownAsync(context, ct);
        if (!shakedownActive) PublishHidden(ConnectionState.Connected, ct);
        return shakedownActive;
    }

    private bool ResultsSettled(WrcEventContext context, IReadOnlyList<WrcStageTime> times,
        IReadOnlyList<WrcStageResult> results, string fingerprint)
    {
        if (!WrcTiming.Wrc1ResultsComplete(context.Entries, times, results))
        {
            _settledResultFingerprint = null;
            _settledResultPolls = 0;
            return false;
        }
        if (fingerprint == _settledResultFingerprint) _settledResultPolls++;
        else
        {
            _settledResultFingerprint = fingerprint;
            _settledResultPolls = 1;
        }
        return _settledResultPolls >= _options.ResultStablePolls;
    }

    private void PublishStage(WrcEventContext context, WrcStage stage, IReadOnlyList<WrcStageTime> times,
        string fingerprint, CancellationToken ct)
    {
        var standings = WrcTiming.BuildStageStandings(context.Entries, times);
        if (_activeStageId != stage.StageId)
        {
            ResetStageState();
            _activeStageId = stage.StageId;
        }
        if (fingerprint != _lastDeltaFingerprint)
        {
            _lastDeltaFingerprint = fingerprint;
            DeltaReceived?.Invoke(new("WrcStage", stage.Raw.DeepClone(), _clock.UtcNow));
        }
        if (_alertedStages.Add(stage.StageId))
            _alerts.Accept(new(AlertKind.SessionStart, 10, false, $"{stage.Code} STARTED", stage.Name, null,
                _clock.UtcNow, $"wrc:event:{context.Event.EventId}:stage:{stage.StageId}:start", TimeSpan.FromSeconds(4)));
        Publish(new(context.Event.Name, $"{stage.Code}  {stage.Name}", "", 0, null, TrackCondition.AllClear,
            standings, _clock.UtcNow, SessionLifecycle.Live, ConnectionState.Connected, null,
            Championship.WorldRallyChampionship), ct);
    }

    private async Task<bool> PollShakedownAsync(WrcEventContext context, CancellationToken ct)
    {
        var advertised = Math.Max(1, context.Event.ShakedownCount);
        var count = Math.Min(_options.ShakedownProbeLimit, Math.Max(advertised, _knownShakedownCount + 1));
        var probes = Enumerable.Range(1, count)
            .Select(async number => (Number: number, Times: await _api.TryGetShakedownTimesAsync(context.Event.EventId, number, ct)))
            .ToArray();
        var responses = await Task.WhenAll(probes);
        IReadOnlyList<WrcShakedownTime>? latest = null;
        foreach (var (number, times) in responses)
        {
            var fingerprint = WrcTiming.ShakedownFingerprint(times);
            if (_shakedownFingerprints.TryGetValue(number, out var previous) && fingerprint != previous && times.Count > 0)
                _lastShakedownChange = _clock.UtcNow;
            _shakedownFingerprints[number] = fingerprint;
            if (times.Count > 0)
            {
                _knownShakedownCount = Math.Max(_knownShakedownCount, number);
                latest = times;
            }
        }

        if (latest is null || !_lastShakedownChange.HasValue || _clock.UtcNow - _lastShakedownChange.Value >= _options.ShakedownQuietGrace) return false;
        var standings = WrcTiming.BuildShakedownStandings(context.Entries, latest);
        if (standings.Count == 0) return false;
        Publish(new(context.Event.Name, "SHAKEDOWN", "", 0, null, TrackCondition.AllClear,
            standings, _clock.UtcNow, SessionLifecycle.Live, ConnectionState.Connected, null,
            Championship.WorldRallyChampionship), ct);
        return true;
    }

    private void CompleteStage(int stageId)
    {
        _completedStages.Add(stageId);
        ResetStageState();
    }

    private async Task<(IReadOnlyList<WrcStageTime> Times, IReadOnlyList<WrcStageResult> Results)> GetStageDataAsync(
        WrcEventContext context, int stageId, CancellationToken ct)
    {
        var timesTask = _api.GetStageTimesAsync(context.Event.EventId, stageId, context.Rally.RallyId, ct);
        var resultsTask = _api.GetStageResultsAsync(context.Event.EventId, stageId, context.Rally.RallyId, ct);
        await Task.WhenAll(timesTask, resultsTask);
        return (await timesTask, await resultsTask);
    }

    private void TransitionHidden(ConnectionState connection, CancellationToken ct)
    {
        PublishHidden(connection, ct);
        SetConnection(connection, ct);
    }

    private void PublishFailure(string message, Exception ex, CancellationToken ct)
    {
        TransitionHidden(ConnectionState.Faulted, ct);
        if (!ct.IsCancellationRequested) Failed?.Invoke(new($"{message}: {ex.Message}", ex, _clock.UtcNow));
    }

    private void PublishHidden(ConnectionState connection, CancellationToken ct) =>
        Publish(TimingSnapshot.Hidden(_clock.UtcNow) with
        {
            ConnectionState = connection,
            Championship = Championship.WorldRallyChampionship
        }, ct);

    private void Publish(TimingSnapshot snapshot, CancellationToken ct)
    {
        if (!ct.IsCancellationRequested) SnapshotReceived?.Invoke(snapshot);
    }

    private void SetConnection(ConnectionState value, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested || _connection == value) return;
        _connection = value;
        ConnectionChanged?.Invoke(value);
    }

    private void ResetEventState()
    {
        _completedStages.Clear();
        _alertedStages.Clear();
        _knownStages = [];
        ResetActivityState();
    }

    private void ResetActivityState()
    {
        ResetStageState();
        _shakedownFingerprints.Clear();
        _knownShakedownCount = 0;
        _lastShakedownChange = null;
    }

    private void ResetStageState()
    {
        _activeStageId = null;
        _settledResultFingerprint = null;
        _settledResultPolls = 0;
        _lastDeltaFingerprint = null;
    }

    private static TimeSpan Backoff(IReadOnlyList<TimeSpan> values, int attempt) => values[Math.Min(attempt, values.Count - 1)];
    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
    private static TimeSpan PositiveDelay(TimeSpan value) => value > TimeSpan.FromSeconds(1) ? value : TimeSpan.FromSeconds(1);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync();
        _disposed = true;
        _api.Dispose();
        _lifecycle.Dispose();
    }
}
