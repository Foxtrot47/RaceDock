// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;
using RaceDock.Core.Presentation;

namespace RaceDock.Core;

/// <summary>
/// Headless RaceDock session. It owns the live/replay source, settings, alerts, schedule, and the
/// immutable presentation snapshots consumed by desktop front ends. No UI toolkit types leak in.
/// </summary>
public sealed class RaceDockSession : IAsyncDisposable
{
    private enum TimingSourceMode { Live, Scenario, ArchiveReplay }

    private sealed record LiveFeed(Championship Championship, string Label, Func<RaceDockSession, ILiveTimingSource> Create);

    private static readonly LiveFeed[] LiveFeeds =
    [
        new(Championship.Formula1, "F1", owner => new F1LiveTimingSource(owner._processor, owner._clock)),
        new(Championship.Formula2, "F2", owner => new F2LiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.Formula3, "F3", owner => new F2LiveTimingSource(owner._clock, owner._alerts, "F3")),
        new(Championship.MotoGp, "MotoGP", owner => new MotoGpLiveTimingSource(owner._clock, owner._alerts, Championship.MotoGp)),
        new(Championship.Moto2, "Moto2", owner => new MotoGpLiveTimingSource(owner._clock, owner._alerts, Championship.Moto2)),
        new(Championship.Moto3, "Moto3", owner => new MotoGpLiveTimingSource(owner._clock, owner._alerts, Championship.Moto3)),
        new(Championship.GtWorldChallengeEurope, "GTWC", owner => new GtwcLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.WorldEnduranceChampionship, "WEC", owner => new WecLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.EuropeanLeMansSeries, "ELMS", owner => new ElmsLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.MichelinLeMansCup, "Le Mans Cup", owner => new MichelinLeMansCupLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.WorldRallyChampionship, "WRC", owner => new WrcLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.Imsa, "IMSA", owner => new ImsaLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.FormulaE, "Formula E", owner => new FormulaELiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.Dtm, "DTM", owner => new DtmLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.IndyCar, "INDYCAR", owner => new IndyCarLiveTimingSource(owner._clock, owner._alerts))
    ];

    private readonly IClock _clock;
    private readonly AlertArbiter _alerts;
    private readonly TimingStateProcessor _processor;
    private readonly UserSettingsStore _settingsStore;
    private readonly UpcomingScheduleService _schedule;
    private readonly TimeZoneInfo _timeZone;
    private readonly SemaphoreSlim _sourceSwitch = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _stateGate = new();
    private readonly object _renderGate = new();
    private readonly Dictionary<Championship, TimingSnapshot> _latestSnapshots = [];
    private ILiveTimingSource? _source;
    private TimingSnapshot? _pending;
    private TimingSnapshot? _currentSnapshot;
    private IReadOnlyList<UpcomingSession> _upcoming = [];
    private RaceDockWidgetState _widget = new();
    private RaceDockFlyoutState _flyout = new();
    private UserSettings _settings;
    private Task? _renderLoop;
    private Task? _healthLoop;
    private Task? _idleLoop;
    private Task? _notifyTask;
    private bool _scheduleRefreshRunning;
    private bool _snapshotLogged;
    private bool _started;
    private int _disposing;
    private int _dirty = 1;
    private int _notifyPending;
    private string? _wrcTransitionKey;

    private bool _disposed => Volatile.Read(ref _disposing) != 0;

    public RaceDockSession(
        UserSettingsStore? settingsStore = null,
        IClock? clock = null,
        UpcomingScheduleService? schedule = null,
        TimeZoneInfo? timeZone = null)
    {
        _clock = clock ?? new SystemClock();
        _settingsStore = settingsStore ?? new UserSettingsStore();
        _schedule = schedule ?? new UpcomingScheduleService(_clock);
        _timeZone = timeZone ?? TimeZoneInfo.Local;
        _alerts = new AlertArbiter(_clock);
        _processor = new TimingStateProcessor(_clock, _alerts);
        _settings = _settingsStore.Load();
        _alerts.VisibleAlertChanged += _ => MarkDirty();
    }

    public event Action? StateChanged;

    public UserSettings Settings => _settings;
    public RaceDockWidgetState Widget { get { lock (_stateGate) return _widget; } }
    public RaceDockFlyoutState Flyout { get { lock (_stateGate) return _flyout; } }

    public bool TestMode => _source is ITimingPlaybackSource;
    public ITimingScenarioSource? Scenario => _source as ITimingScenarioSource;
    public ITimingPlaybackSource? Playback => _source as ITimingPlaybackSource;
    public ITimingArchiveSource? Archive => _source as ITimingArchiveSource;

    public async Task StartAsync(bool scenario = false, string? archivePath = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;
        _started = true;
        _renderLoop = Task.Run(RenderLoopAsync);
        _healthLoop = Task.Run(HealthLoopAsync);
        _idleLoop = Task.Run(IdleLoopAsync);

        if (!string.IsNullOrWhiteSpace(archivePath))
        {
            await LoadArchiveReplayAsync(archivePath);
            return;
        }
        if (scenario)
        {
            await SetTestModeAsync(true);
            return;
        }
        await StartLiveAsync();
    }

    public Task StartLiveAsync()
    {
        _ = RefreshScheduleAsync();
        return SwitchSourceAsync(TimingSourceMode.Live);
    }

    public Task SetTestModeAsync(bool enabled) =>
        SwitchSourceAsync(enabled ? TimingSourceMode.Scenario : TimingSourceMode.Live);

    public async Task RestartAsync()
    {
        await _sourceSwitch.WaitAsync(_lifetime.Token);
        try
        {
            if (Playback is { } playback) playback.Restart();
            else await SwitchSourceCoreAsync(TimingSourceMode.Live, CreateLiveSource());
        }
        finally { _sourceSwitch.Release(); }
        MarkDirty();
    }

    public async Task LoadArchiveReplayAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        await _sourceSwitch.WaitAsync(_lifetime.Token);
        try
        {
            ITimingArchiveSource replay = string.Equals(Path.GetExtension(fullPath), ".ndjson", StringComparison.OrdinalIgnoreCase)
                ? await FeedDumpReplayLoader.CreateAsync(fullPath, _clock, _lifetime.Token)
                : await TimingArchiveReplaySource.CreateAsync(fullPath, _clock, _lifetime.Token);
            await SwitchSourceCoreAsync(TimingSourceMode.ArchiveReplay, replay);
        }
        finally { _sourceSwitch.Release(); }
    }

    public Task ReturnToLiveAsync() => SwitchSourceAsync(TimingSourceMode.Live);

    public async Task ApplySettingsAsync(UserSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var normalized = settings.Normalize();
        var restartFeeds = FeedsDiffer(normalized, _settings);
        _settingsStore.Save(normalized);
        _settings = normalized;
        // Hiding the championship that is on track must clear it immediately: no new snapshot is
        // guaranteed to arrive for a paused replay, and a stale classification would ignore the setting.
        if (_currentSnapshot is { } current && !normalized.IsChampionshipVisible(current.Championship))
        {
            _currentSnapshot = null;
            Interlocked.Exchange(ref _pending, null);
        }
        if (restartFeeds) _upcoming = [];
        MarkDirty();
        _ = RefreshScheduleAsync();
        if (restartFeeds && !TestMode && !_disposed) await SwitchSourceAsync(TimingSourceMode.Live, force: true);
        else BuildState();
    }

    public async Task<string> CommandAsync(string command, string? argument)
    {
        if (_disposed) return Error("The RaceDock session has shut down.");
        try
        {
            switch (command.Trim().ToLowerInvariant())
            {
                case "resume":
                    Playback?.Resume();
                    break;
                case "pause":
                    Playback?.Pause();
                    break;
                case "restart":
                    await RestartAsync();
                    break;
                case "step":
                    Playback?.Step();
                    break;
                case "speed":
                    if (Playback is null) return Error("No replay source is active.");
                    if (!double.TryParse(argument, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                        System.Globalization.CultureInfo.InvariantCulture, out var speed))
                        return Error("Speed must be a number.");
                    Playback.Speed = speed;
                    break;
                case "trigger":
                    if (Scenario is null) return Error("No scenario source is active.");
                    if (!Enum.TryParse<ScenarioCommand>(argument, true, out var scenarioCommand))
                        return Error($"Unknown scenario command '{argument}'.");
                    Scenario.Trigger(scenarioCommand);
                    break;
                case "scenario":
                    await SetTestModeAsync(!string.Equals(argument, "false", StringComparison.OrdinalIgnoreCase));
                    break;
                case "archive":
                    if (string.IsNullOrWhiteSpace(argument)) return Error("An archive path is required.");
                    await LoadArchiveReplayAsync(argument);
                    break;
                case "live":
                    await ReturnToLiveAsync();
                    break;
                case "refreshschedule":
                case "refresh":
                    await RefreshScheduleAsync();
                    break;
                case "refreshstate":
                    MarkDirty();
                    break;
                case "resetsettings":
                    await ApplySettingsAsync(new UserSettings());
                    return RaceDockJson.Serialize(new { ok = true, settings = _settings });
                default:
                    return Error($"Unknown command '{command}'.");
            }
            // A step/trigger/restart publishes a snapshot synchronously. Rendering it here means a
            // client that polls right after the command sees the new frame instead of the previous
            // one until the 250 ms render loop ticks.
            RenderPendingSnapshot();
            BuildState();
            return RaceDockJson.Serialize(new { ok = true, command });
        }
        catch (Exception exception)
        {
            AppLog.Warning("Session", $"Command '{command}' failed.", exception);
            return Error(exception.Message);
        }
    }

    private static string Error(string message) =>
        RaceDockJson.Serialize(new { ok = false, error = message });

    private async Task SwitchSourceAsync(TimingSourceMode mode, bool force = false)
    {
        if (_disposed) return;
        await _sourceSwitch.WaitAsync(_lifetime.Token);
        try
        {
            if (!force && mode == TimingSourceMode.Live && _source is not null && _source is not ITimingPlaybackSource) return;
            if (!force && mode == TimingSourceMode.Scenario && _source is ITimingScenarioSource) return;
            var candidate = mode == TimingSourceMode.Scenario
                ? new ScenarioTimingSource(_processor, _clock)
                : CreateLiveSource();
            await SwitchSourceCoreAsync(mode, candidate);
        }
        finally { _sourceSwitch.Release(); }
    }

    private IEnumerable<LiveFeed> EnabledFeeds()
    {
        var enabled = EnabledChampionships(_settings);
        return LiveFeeds.Where(feed => enabled.Contains(feed.Championship));
    }

    private ILiveTimingSource CreateLiveSource() =>
        new CompositeLiveTimingSource(
            EnabledFeeds().Select(feed => (Func<ILiveTimingSource>)(() => feed.Create(this))).ToList(),
            _clock,
            TimeSpan.FromSeconds(_settings.RotationSeconds),
            TimeSpan.FromMinutes(_settings.CompletedSessionRetentionMinutes));

    private async Task SwitchSourceCoreAsync(TimingSourceMode mode, ILiveTimingSource candidate)
    {
        if (_source is not null)
        {
            Detach(_source);
            await _source.DisposeAsync();
            _source = null;
        }
        _latestSnapshots.Clear();
        Interlocked.Exchange(ref _pending, null);
        _currentSnapshot = null;
        _alerts.Clear();
        _processor.ProcessInitial(new JsonObject());
        _snapshotLogged = false;
        _wrcTransitionKey = null;
        try
        {
            Attach(candidate);
            await candidate.StartAsync(_lifetime.Token);
            _source = candidate;
        }
        catch
        {
            await StopQuietlyAsync(candidate);
            if (mode != TimingSourceMode.Live && !_lifetime.IsCancellationRequested)
            {
                // A replay that cannot start leaves the user with nothing; fall back to live timing.
                var fallback = CreateLiveSource();
                try
                {
                    Attach(fallback);
                    await fallback.StartAsync(_lifetime.Token);
                    _source = fallback;
                    BuildState();
                }
                catch (Exception fallbackFailure)
                {
                    AppLog.Warning("Session", "Live timing could not start after the replay failed.", fallbackFailure);
                    await StopQuietlyAsync(fallback);
                }
            }
            throw;
        }
        LogSourceStarted(candidate);
        BuildState();
    }

    private async Task StopQuietlyAsync(ILiveTimingSource source)
    {
        try
        {
            Detach(source);
            await source.DisposeAsync();
        }
        catch (Exception exception)
        {
            AppLog.Warning("Session", "A timing source did not shut down cleanly.", exception);
        }
    }

    private void LogSourceStarted(ILiveTimingSource source)
    {
        var enabled = string.Join(" + ", EnabledFeeds().Select(feed => feed.Label));
        var message = source switch
        {
            ITimingArchiveSource archive => $"Source started: archive replay {archive.ArchiveName}; {archive.FrameCount} frames",
            ITimingScenarioSource => "Source started: deterministic test scenario (paused)",
            _ => $"Source started: {enabled}; rotation {_settings.RotationSeconds}s; completed sessions {_settings.CompletedSessionRetentionMinutes}m"
        };
        AppLog.Info("Session", message);
    }

    private void Attach(ILiveTimingSource source)
    {
        source.SnapshotReceived += OnSnapshot;
        source.DeltaReceived += OnDelta;
        source.ConnectionChanged += OnConnection;
        source.Failed += OnFailure;
    }

    private void Detach(ILiveTimingSource source)
    {
        source.SnapshotReceived -= OnSnapshot;
        source.DeltaReceived -= OnDelta;
        source.ConnectionChanged -= OnConnection;
        source.Failed -= OnFailure;
    }

    private void OnSnapshot(TimingSnapshot snapshot)
    {
        Interlocked.Exchange(ref _pending, snapshot);
        if (_snapshotLogged || snapshot.Competitors.Count == 0) return;
        _snapshotLogged = true;
        AppLog.Info("Session", $"Live snapshot: {snapshot.Championship}; {snapshot.Meeting} — {snapshot.Session}; " +
            $"{snapshot.Competitors.Count} competitors; lap {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}; " +
            $"time remaining {snapshot.TimeRemaining ?? "—"}");
    }

    private void OnDelta(TimingDelta delta)
    {
        if (delta.Topic != "WrcStage") return;
        var stage = delta.Data.AsObject();
        var id = JsonSupport.Int(stage["stageId"]);
        var status = JsonSupport.String(stage["status"]);
        var key = $"{id}:{status}";
        if (key == _wrcTransitionKey) return;
        _wrcTransitionKey = key;
        var code = JsonSupport.String(stage["code"]) ?? "SS";
        var name = JsonSupport.String(stage["name"]) ?? "";
        AppLog.Info("Session", $"WRC stage transition: {code} ({name}) stage {id} -> {status}");
    }

    private void OnConnection(ConnectionState state) => AppLog.Info("Session", $"Connection: {state}");
    private void OnFailure(FeedFailure failure) => AppLog.Warning("Feed", failure.Message, failure.Exception);

    private async Task RenderLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try
        {
            while (await timer.WaitForNextTickAsync(_lifetime.Token))
            {
                var snapshot = Interlocked.Exchange(ref _pending, null);
                if (snapshot is not null) RenderSnapshot(snapshot);
                else if (Interlocked.Exchange(ref _dirty, 0) == 1) BuildState();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    private async Task HealthLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(_lifetime.Token))
            {
                _alerts.Tick();
                if (_source is not ITimingPlaybackSource)
                {
                    _processor.MarkStale(TimeSpan.FromSeconds(12));
                    if (_currentSnapshot is not null && _currentSnapshot.Lifecycle == SessionLifecycle.Live &&
                        _clock.UtcNow - _currentSnapshot.FreshnessTimestamp >= TimeSpan.FromSeconds(12))
                        _currentSnapshot = _currentSnapshot with { Lifecycle = SessionLifecycle.OffSession, ConnectionState = ConnectionState.Stale };
                }
                if (Interlocked.Exchange(ref _dirty, 0) == 1) BuildState();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    private async Task IdleLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(_lifetime.Token))
            {
                if (_currentSnapshot is null && _settings.ShowNextSessionWhenIdle)
                {
                    var now = _clock.UtcNow;
                    if (_upcoming.Count == 0 || _upcoming.Any(session => !session.IsAhead(now, _timeZone)))
                        await RefreshScheduleAsync();
                    else
                        BuildState();
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    private void RenderPendingSnapshot()
    {
        var snapshot = Interlocked.Exchange(ref _pending, null);
        if (snapshot is not null) RenderSnapshot(snapshot);
    }

    private void RenderSnapshot(TimingSnapshot snapshot)
    {
        // Commands can render a pending snapshot outside the render loop, so serialize the two
        // renderers; otherwise an older snapshot could overwrite a newer one.
        lock (_renderGate)
        {
            if (_disposed) return;
            if (snapshot.Championship != Championship.Unknown && !_settings.IsChampionshipVisible(snapshot.Championship)) return;
            UpdateTimingStatuses(snapshot);
            if (snapshot.Lifecycle == SessionLifecycle.OffSession || (snapshot.Lifecycle == SessionLifecycle.Ended && Archive is null))
            {
                _currentSnapshot = null;
                BuildState();
                _ = RefreshScheduleAsyncIfIdle();
                return;
            }
            _currentSnapshot = snapshot;
            BuildState();
        }
    }

    private void UpdateTimingStatuses(TimingSnapshot snapshot)
    {
        if (snapshot.Championship == Championship.Unknown && snapshot.Lifecycle == SessionLifecycle.OffSession)
            _latestSnapshots.Clear();
        else if (snapshot.Championship != Championship.Unknown)
        {
            if (snapshot.Lifecycle == SessionLifecycle.OffSession) _latestSnapshots.Remove(snapshot.Championship);
            else _latestSnapshots[snapshot.Championship] = snapshot;
        }
    }

    private async Task RefreshScheduleAsyncIfIdle()
    {
        if (!_settings.ShowNextSessionWhenIdle) return;
        await RefreshScheduleAsync();
    }

    private async Task RefreshScheduleAsync()
    {
        if (_scheduleRefreshRunning || _disposed) return;
        _scheduleRefreshRunning = true;
        try
        {
            var sessions = await _schedule.GetUpcomingAsync(EnabledChampionships(_settings), _lifetime.Token);
            _upcoming = NextSessionDisplay.Schedule(sessions, _clock.UtcNow, _timeZone);
            BuildState();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (_disposed) { }
        catch (Exception failure)
        {
            AppLog.Warning("Session", "Could not load the next published session.", failure);
        }
        finally { _scheduleRefreshRunning = false; }
    }

    private void BuildState()
    {
        if (_disposed) return;
        var now = _clock.UtcNow;
        lock (_stateGate)
        {
            _widget = RaceDockPresentationBuilder.BuildWidget(
                _currentSnapshot, _settings, _upcoming, _alerts.Current, now, _timeZone, TestMode);
            _flyout = RaceDockPresentationBuilder.BuildFlyout(
                _currentSnapshot, _settings, _upcoming, _alerts.Current, now, _timeZone);
        }
        RequestStateChanged();
    }

    /// <summary>
    /// Signals listeners without running them on the caller's thread. Source switches call
    /// <see cref="BuildState"/> while holding the session semaphore, so a subscriber that calls back
    /// into the session (a command, a restart, a poll) would otherwise block or recurse forever.
    /// Notifications are coalesced: they only mean "read the latest snapshot".
    /// </summary>
    private void RequestStateChanged()
    {
        if (_disposed) return;
        if (Interlocked.Exchange(ref _notifyPending, 1) == 1) return;
        lock (_stateGate)
        {
            if (_notifyTask is not { IsCompleted: false })
                _notifyTask = Task.Run(DispatchStateChangedAsync);
        }
    }

    private async Task DispatchStateChangedAsync()
    {
        // Leave the switching thread before touching subscribers.
        await Task.Yield();
        while (!_disposed)
        {
            if (Interlocked.Exchange(ref _notifyPending, 0) == 0) return;
            if (StateChanged is not { } handlers) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                if (_disposed) return;
                try { ((Action)handler)(); }
                catch (Exception exception) { AppLog.Warning("Session", "A state-change subscriber threw.", exception); }
            }
        }
    }

    private void MarkDirty() => Interlocked.Exchange(ref _dirty, 1);

    internal static HashSet<Championship> EnabledChampionships(UserSettings settings) =>
        Enum.GetValues<Championship>().Where(settings.IsChampionshipEnabled).ToHashSet();

    private static bool FeedsDiffer(UserSettings left, UserSettings right) =>
        !EnabledChampionships(left).SetEquals(EnabledChampionships(right)) ||
        left.RotationSeconds != right.RotationSeconds ||
        left.CompletedSessionRetentionMinutes != right.CompletedSessionRetentionMinutes;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposing, 1) != 0) return;
        await _lifetime.CancelAsync();
        if (_renderLoop is not null) try { await _renderLoop; } catch (OperationCanceledException) { }
        if (_healthLoop is not null) try { await _healthLoop; } catch (OperationCanceledException) { }
        if (_idleLoop is not null) try { await _idleLoop; } catch (OperationCanceledException) { }
        lock (_stateGate) { if (_notifyTask is not null) try { _notifyTask.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { } }
        await _sourceSwitch.WaitAsync();
        try
        {
            if (_source is not null)
            {
                var source = _source;
                _source = null;
                await StopQuietlyAsync(source);
            }
        }
        finally { _sourceSwitch.Release(); }
        _schedule.Dispose();
        _sourceSwitch.Dispose();
        _lifetime.Dispose();
    }
}
