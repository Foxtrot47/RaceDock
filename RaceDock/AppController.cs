// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Threading;
using RaceDock.Core;

namespace RaceDock.App;

public sealed class AppController : IAsyncDisposable
{
    private enum TimingSourceMode { Live, Scenario, ArchiveReplay }

    private readonly Dispatcher _dispatcher;
    private readonly SystemClock _clock = new();
    private readonly AlertArbiter _alerts;
    private readonly TimingStateProcessor _processor;
    private readonly UserSettingsStore _settingsStore;
    private readonly DispatcherTimer _renderTimer;
    private readonly DispatcherTimer _healthTimer;
    private readonly SemaphoreSlim _sourceSwitch = new(1, 1);
    private ILiveTimingSource? _source;
    private TimingSnapshot? _pending;
    private readonly Dictionary<Championship, TimingSnapshot> _latestSnapshots = [];
    private readonly Dictionary<Championship, TimingSnapshot> _feedSnapshots = [];
    private readonly ConcurrentQueue<TimingSnapshot> _feedUpdates = new();
    private IReadOnlyList<LiveFeedStatus> _liveFeeds = [];
    private TimingSnapshot? _lastRotation;
    private TaskbarWindow? _taskbar;
    private SettingsWindow? _settingsWindow;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly UpcomingScheduleService _schedule = new();
    private NascarFeed? _nascarFeed;
    private FeedEndpoints _feedEndpoints = FeedEndpoints.Live;
    private IReadOnlySet<Championship>? _feedServerChampionships;
    private readonly DispatcherTimer _idleTimer;
    private IReadOnlyList<UpcomingSession> _upcoming = [];
    private bool _scheduleRefreshRunning;
    private UserSettings _settings;
    private bool _snapshotLogged;
    private readonly Lock _failureGate = new();
    private readonly Dictionary<string, DateTimeOffset> _recentFailures = [];
    private static readonly TimeSpan FailureRepeatWindow = TimeSpan.FromSeconds(1);
    private string? _wrcTransitionKey;
    private bool _disposed;
    public event Action<ConnectionState>? StatusChanged;
    public UserSettings Settings => _settings;
    public ITimingScenarioSource? Scenario => _source as ITimingScenarioSource;
    public ITimingPlaybackSource? Playback => _source as ITimingPlaybackSource;
    public ITimingArchiveSource? Archive => _source as ITimingArchiveSource;
    public bool TestMode => _source is ITimingPlaybackSource;

    public AppController(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher; _alerts = new(_clock); _processor = new(_clock, _alerts);
        _settingsStore = new(legacyPath: WindowsStartupRegistration.LegacySettingsPath);
        _settings = _settingsStore.Load();
        try { WindowsStartupRegistration.Apply(_settings.AutoStartOnWindowsBoot); }
        catch (Exception ex) { AppLog.Error("Startup", "Windows startup registration failed.", ex); }
        _alerts.VisibleAlertChanged += alert => _dispatcher.BeginInvoke(() => _taskbar?.SetAlert(alert));
        _renderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => RenderPending(), dispatcher);
        _healthTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            _alerts.Tick();
            if (_source is not ITimingPlaybackSource) _processor.MarkStale(TimeSpan.FromSeconds(12));
        }, dispatcher);
        // The countdown only has to stay honest to the minute, and it also carries the schedule refresh.
        _idleTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => UpdateIdleWidget(), dispatcher);
        _renderTimer.Start(); _healthTimer.Start(); _idleTimer.Start();
    }

    public Task StartLiveAsync()
    {
        // The widget can show the next session before any feed reports in, so the calendar is asked at once.
        _ = RefreshNextSessionAsync();
        return SwitchSourceAsync(TimingSourceMode.Live);
    }
    public async Task RestartAsync()
    {
        await _sourceSwitch.WaitAsync(_lifetime.Token);
        try
        {
            if (Playback is { } playback) playback.Restart();
            else await SwitchSourceCoreAsync(TimingSourceMode.Live, CreateLiveSource());
        }
        finally { _sourceSwitch.Release(); }
    }

    public Task SetTestModeAsync(bool enabled) =>
        SwitchSourceAsync(enabled ? TimingSourceMode.Scenario : TimingSourceMode.Live, resetOverlay: true);

    public async Task LoadArchiveReplayAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        await _sourceSwitch.WaitAsync(_lifetime.Token);
        try
        {
            ITimingArchiveSource replay = await FeedDumpReplayLoader.CreateAsync(fullPath, _clock, _lifetime.Token);
            await SwitchSourceCoreAsync(TimingSourceMode.ArchiveReplay, replay, resetOverlay: true);
        }
        finally { _sourceSwitch.Release(); }
    }

    /// <summary>
    /// Points every live adapter at a local feed server and enables the championships that server advertises.
    /// The widget and flyout then exercise the real transports and mapping against recorded payloads.
    /// </summary>
    public async Task ConnectFeedServerAsync(Uri server)
    {
        _feedEndpoints = FeedEndpoints.ForServer(server);
        var served = FeedServerCatalog.Championships(
            await FeedServerCatalog.GetStreamIdsAsync(server, _lifetime.Token));
        // The flag means "do not reach providers", so an unreachable server leaves the emulated adapters
        // retrying rather than quietly opening live connections because no stream list came back.
        _feedServerChampionships = served.Count > 0 ? served : FeedServerCatalog.EmulatedChampionships;
        Log($"Feed server {server}: {string.Join(", ", _feedServerChampionships.OrderBy(value => value))}");
    }

    public Task ReturnToLiveAsync() => SwitchSourceAsync(TimingSourceMode.Live, resetOverlay: true);

    private async Task SwitchSourceAsync(TimingSourceMode mode, bool force = false, bool resetOverlay = false)
    {
        await _sourceSwitch.WaitAsync(_lifetime.Token);
        try
        {
            if (!force && mode == TimingSourceMode.Live && _source is not null && _source is not ITimingPlaybackSource) return;
            if (!force && mode == TimingSourceMode.Scenario && _source is ITimingScenarioSource) return;
            var candidate = mode == TimingSourceMode.Scenario
                ? new ScenarioTimingSource(_processor, _clock)
                : CreateLiveSource();
            await SwitchSourceCoreAsync(mode, candidate, resetOverlay);
        }
        finally { _sourceSwitch.Release(); }
    }

    /// <summary>Every championship that can be fed live, in rotation order, with the label used in the log.</summary>
    private sealed record LiveFeed(Championship Championship, string Label, Func<AppController, ILiveTimingSource> Create);

    private static readonly LiveFeed[] LiveFeeds =
    [
        new(Championship.Formula1, "F1", owner => new F1LiveTimingSource(owner._processor, owner._clock, owner._feedEndpoints.Formula1WebSocket)),
        new(Championship.Formula2, "F2", owner => new F2LiveTimingSource(owner._clock, owner._alerts, "F2", owner._feedEndpoints.Formula2Base)),
        new(Championship.Formula3, "F3", owner => new F2LiveTimingSource(owner._clock, owner._alerts, "F3", owner._feedEndpoints.Formula3Base)),
        new(Championship.F1Academy, "F1 Academy", owner => new F2LiveTimingSource(owner._clock, owner._alerts, "F1 Academy", owner._feedEndpoints.F1AcademyBase)),
        new(Championship.MotoGp, "MotoGP", owner => new MotoGpLiveTimingSource(owner._clock, owner._alerts, Championship.MotoGp)),
        new(Championship.Moto2, "Moto2", owner => new MotoGpLiveTimingSource(owner._clock, owner._alerts, Championship.Moto2)),
        new(Championship.Moto3, "Moto3", owner => new MotoGpLiveTimingSource(owner._clock, owner._alerts, Championship.Moto3)),
        new(Championship.GtWorldChallengeEurope, "GTWC", owner => new GtwcLiveTimingSource(owner._clock, owner._alerts, owner._feedEndpoints.GtwcBase)),
        new(Championship.WorldEnduranceChampionship, "WEC", owner => new WecLiveTimingSource(owner._clock, owner._alerts, owner._feedEndpoints.GriiipBase)),
        new(Championship.EuropeanLeMansSeries, "ELMS", owner => new ElmsLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.MichelinLeMansCup, "Le Mans Cup", owner => new MichelinLeMansCupLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.WorldRallyChampionship, "WRC", owner => new WrcLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.Imsa, "IMSA", owner => new ImsaLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.FormulaE, "Formula E", owner => new FormulaELiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.Dtm, "DTM", owner => new DtmLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.IndyCar, "INDYCAR", owner => new IndyCarLiveTimingSource(owner._clock, owner._alerts)),
        new(Championship.NascarCup, "NASCAR Cup", owner => new NascarLiveTimingSource(owner.NascarFeedSource(), NascarSeries.Cup, owner._clock, owner._alerts)),
        new(Championship.NascarXfinity, "NASCAR Xfinity", owner => new NascarLiveTimingSource(owner.NascarFeedSource(), NascarSeries.Xfinity, owner._clock, owner._alerts)),
        new(Championship.NascarTrucks, "NASCAR Trucks", owner => new NascarLiveTimingSource(owner.NascarFeedSource(), NascarSeries.Trucks, owner._clock, owner._alerts)),
    ];

    /// <summary>The one NASCAR request scheduler, created against whichever endpoint the session configured.</summary>
    private NascarFeed NascarFeedSource() => _nascarFeed ??= new(_clock, apiBase: _feedEndpoints.NascarBase);

    private IEnumerable<LiveFeed> EnabledFeeds()
    {
        if (_feedServerChampionships is { } served)
            return LiveFeeds.Where(feed => served.Contains(feed.Championship));
        var enabled = EnabledChampionships(_settings);
        return LiveFeeds.Where(feed => enabled.Contains(feed.Championship));
    }

    private ILiveTimingSource CreateLiveSource()
    {
        var factories = EnabledFeeds()
            .Select(feed => (Func<ILiveTimingSource>)(() => feed.Create(this)))
            .ToList();
        return new CompositeLiveTimingSource(factories, _clock,
            TimeSpan.FromSeconds(_settings.RotationSeconds),
            TimeSpan.FromMinutes(_settings.CompletedSessionRetentionMinutes));
    }

    private async Task SwitchSourceCoreAsync(TimingSourceMode mode, ILiveTimingSource candidate, bool resetOverlay = false)
    {
        if (_source is not null)
        {
            Detach(_source);
            await _source.DisposeAsync();
            _source = null;
        }
        _latestSnapshots.Clear();
        _feedSnapshots.Clear();
        _feedUpdates.Clear();
        _liveFeeds = [];
        _lastRotation = null;
        if (resetOverlay) _taskbar?.ResetOverlayFeed();
        _taskbar?.ResetLiveFeeds();
        _settingsWindow?.ClearTimingStatuses();
        _pending = null;
        _taskbar?.Hide();
        _alerts.Clear();
        _processor.ProcessInitial(new System.Text.Json.Nodes.JsonObject());
        _snapshotLogged = false;
        _wrcTransitionKey = null;
        try
        {
            Attach(candidate);
            await candidate.StartAsync(_lifetime.Token);
            _source = candidate;
            _settingsWindow?.SyncTestingMode();
        }
        catch
        {
            Detach(candidate);
            await candidate.DisposeAsync();
            if (mode != TimingSourceMode.Live && !_lifetime.IsCancellationRequested)
            {
                var fallback = CreateLiveSource();
                Attach(fallback);
                await fallback.StartAsync(_lifetime.Token);
                _source = fallback;
            }
            throw;
        }
        LogSourceStarted(candidate);
    }

    private void LogSourceStarted(ILiveTimingSource source)
    {
        var enabled = string.Join(" + ", EnabledFeeds().Select(feed => feed.Label));
        Log(source switch
        {
            ITimingArchiveSource archive => $"Source started: archive replay {archive.ArchiveName}; {archive.FrameCount} frames",
            ITimingScenarioSource => "Source started: deterministic test scenario (paused)",
            _ => $"Source started: {enabled}; rotation {_settings.RotationSeconds}s; completed sessions {_settings.CompletedSessionRetentionMinutes}m"
        });
    }

    private void Attach(ILiveTimingSource source)
    {
        source.SnapshotReceived += OnSnapshot; source.DeltaReceived += OnDelta; source.ConnectionChanged += OnConnection; source.Failed += OnFailure;
        if (source is ILiveFeedDirectory directory)
        {
            directory.FeedSnapshotReceived += OnFeedSnapshot;
            directory.LiveFeedsChanged += OnLiveFeedsChanged;
        }
    }
    private void Detach(ILiveTimingSource source)
    {
        source.SnapshotReceived -= OnSnapshot; source.DeltaReceived -= OnDelta; source.ConnectionChanged -= OnConnection; source.Failed -= OnFailure;
        if (source is ILiveFeedDirectory directory)
        {
            directory.FeedSnapshotReceived -= OnFeedSnapshot;
            directory.LiveFeedsChanged -= OnLiveFeedsChanged;
        }
    }
    private void OnFeedSnapshot(TimingSnapshot snapshot) => _feedUpdates.Enqueue(snapshot);
    private void OnLiveFeedsChanged(IReadOnlyList<LiveFeedStatus> feeds)
    {
        if (_disposed) return;
        _dispatcher.BeginInvoke(() => ApplyLiveFeeds(feeds));
    }
    private void OnSnapshot(TimingSnapshot snapshot)
    {
        _pending = snapshot;
        if (_snapshotLogged || snapshot.Competitors.Count == 0) return;
        _snapshotLogged = true;
        var gaps = string.Join(", ", snapshot.Competitors.Take(5).Select(standing =>
            $"{standing.Code}:{standing.GapToLeader ?? "—"}/{standing.IntervalToPositionAhead ?? "—"}"));
        Log($"Live snapshot: {snapshot.Championship}; {snapshot.Meeting} — {snapshot.Session}; {snapshot.Competitors.Count} competitors; " +
            $"lap {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}; time remaining {snapshot.TimeRemaining ?? "—"}; gaps {gaps}");
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
        Log($"WRC stage transition: {code} ({name}) stage {id} -> {status}");
    }
    private void OnConnection(ConnectionState state) { StatusChanged?.Invoke(state); Log($"Connection: {state}"); }
    /// <summary>
    /// One shared failure reaches the controller once per subscribed source, so an identical message inside the
    /// repeat window is logged once. Each source still resets its own session and connection state.
    /// </summary>
    private void OnFailure(FeedFailure failure)
    {
        var now = _clock.UtcNow;
        lock (_failureGate)
        {
            foreach (var stale in _recentFailures.Where(pair => now - pair.Value >= FailureRepeatWindow).Select(pair => pair.Key).ToList())
                _recentFailures.Remove(stale);
            if (_recentFailures.ContainsKey(failure.Message)) return;
            _recentFailures[failure.Message] = now;
        }
        AppLog.Warning("Feed", failure.Message, failure.Exception);
    }
    private void RenderPending()
    {
        if (_disposed) return;
        DrainFeedUpdates();
        var snapshot = Interlocked.Exchange(ref _pending, null); if (snapshot is null) return;
        _lastRotation = snapshot;
        // Live feeds never start for a hidden championship, but archive playback can still produce one.
        // A feed-server session is an explicit test of the streams the server advertises, so it renders them
        // even when the user hid that championship from normal rotation.
        if (snapshot.Championship != Championship.Unknown && _feedServerChampionships is null &&
            !_settings.IsChampionshipVisible(snapshot.Championship)) return;
        UpdateTimingStatuses(snapshot);
        if (snapshot.Lifecycle == SessionLifecycle.OffSession || (snapshot.Lifecycle == SessionLifecycle.Ended && Archive is null))
        {
            _taskbar?.Hide();
            UpdateIdleWidget();
            return;
        }
        var taskbar = EnsureTaskbar();
        taskbar.UpdateSnapshot(snapshot);
        // A held flyout refuses every other championship, which is what keeps a pinned panel still.
        taskbar.UpdateOverlaySnapshot(snapshot);
        taskbar.SetAlert(_alerts.Current);
        if (!taskbar.IsVisible) taskbar.Show();
    }

    /// <summary>
    /// Reads snapshots from every feed, not only the rotation-selected one, so the flyout can follow a
    /// championship the widget happens not to be showing.
    /// </summary>
    private void DrainFeedUpdates()
    {
        var overlayFeed = _taskbar?.OverlayFeed;
        var overlayUpdated = false;
        while (_feedUpdates.TryDequeue(out var snapshot))
        {
            if (snapshot.Championship == Championship.Unknown) continue;
            if (snapshot.Lifecycle == SessionLifecycle.Live) _feedSnapshots[snapshot.Championship] = snapshot;
            else if (snapshot.Lifecycle == SessionLifecycle.OffSession) _feedSnapshots.Remove(snapshot.Championship);
            // An ended snapshot keeps the last live classification, mirroring the composite's retention window.
            if (snapshot.Championship == overlayFeed) overlayUpdated = true;
        }
        if (!overlayUpdated || overlayFeed is not { } feed) return;
        if (_feedSnapshots.TryGetValue(feed, out var overlay)) _taskbar?.UpdateOverlaySnapshot(overlay);
    }

    private void ApplyLiveFeeds(IReadOnlyList<LiveFeedStatus> feeds)
    {
        if (_disposed) return;
        _liveFeeds = feeds;
        var live = feeds.Select(feed => feed.Championship).ToHashSet();
        foreach (var championship in _feedSnapshots.Keys.Where(championship => !live.Contains(championship)).ToList())
            _feedSnapshots.Remove(championship);
        _taskbar?.UpdateLiveFeeds(feeds);
    }

    /// <summary>The flyout changed feeds by itself: a chip was picked, a pin froze it, or it returned to rotation.</summary>
    private void OnOverlayFeedChanged(Championship? feed)
    {
        if (feed is { } championship)
        {
            if (_feedSnapshots.TryGetValue(championship, out var overlay)) _taskbar?.UpdateOverlaySnapshot(overlay);
            return;
        }
        if (_lastRotation is not null) _taskbar?.UpdateOverlaySnapshot(_lastRotation);
    }

    private void UpdateTimingStatuses(TimingSnapshot snapshot)
    {
        if (snapshot.Championship == Championship.Unknown && snapshot.Lifecycle == SessionLifecycle.OffSession)
        {
            _latestSnapshots.Clear();
            _settingsWindow?.ClearTimingStatuses();
        }
        else if (snapshot.Championship != Championship.Unknown)
        {
            if (snapshot.Lifecycle == SessionLifecycle.OffSession) _latestSnapshots.Remove(snapshot.Championship);
            else _latestSnapshots[snapshot.Championship] = snapshot;
            _settingsWindow?.UpdateSnapshot(snapshot);
        }
    }

    /// <summary>A closed WPF window cannot be shown again; later updates must create a new widget.</summary>
    private TaskbarWindow EnsureTaskbar()
    {
        if (_taskbar is { IsClosed: false } existing) return existing;
        _taskbar = null;
        var taskbar = new TaskbarWindow(_settings);
        taskbar.SettingsRequested += ShowSettings;
        taskbar.OverlayFeedChanged += OnOverlayFeedChanged;
        // A recreated widget starts following rotation and reuses the feed choices already known.
        taskbar.UpdateLiveFeeds(_liveFeeds);
        taskbar.Closed += (_, _) =>
        {
            if (ReferenceEquals(_taskbar, taskbar)) _taskbar = null;
        };
        _taskbar = taskbar;
        return taskbar;
    }

    /// <summary>
    /// Puts the next published session on the taskbar while no session is on track, so the widget still says
    /// something on a day without running sessions. Live timing always takes the widget back.
    /// </summary>
    private void UpdateIdleWidget()
    {
        // A snapshot on the widget outranks the calendar, including a completed session still being retained.
        if (_disposed || TestMode) return;
        if (_taskbar?.IsClosed == true) _taskbar = null;
        if (_taskbar is { ShowingSnapshot: true }) return;
        var now = _clock.UtcNow;
        var upcoming = NextSessionDisplay.Schedule(_upcoming, now);
        _taskbar?.UpdateOverlaySchedule(upcoming, now);
        if (!_settings.ShowNextSessionWhenIdle) { _taskbar?.Hide(); return; }
        if (_upcoming.Any(session => !session.IsAhead(now, TimeZoneInfo.Local))) _ = RefreshNextSessionAsync();
        // An exhausted calendar means the feeds, not the schedule, own what happens next.
        if (upcoming.Count == 0)
        {
            _taskbar?.Hide();
            _ = RefreshNextSessionAsync();
            return;
        }
        EnsureTaskbar().ShowNextSession(upcoming[0], upcoming, now);
    }

    /// <summary>Reloads the published schedule. The service caches, so calling this often costs nothing.</summary>
    private async Task RefreshNextSessionAsync()
    {
        if (_scheduleRefreshRunning) return;
        // A feed-server session exists to exercise recorded streams; asking provider calendars would be live
        // traffic, and the widget's idle state is not what the session is testing.
        if (_feedServerChampionships is not null) return;
        _scheduleRefreshRunning = true;
        try
        {
            var sessions = await _schedule.GetUpcomingAsync(EnabledChampionships(_settings), _lifetime.Token);
            var refreshed = NextSessionDisplay.Schedule(sessions, _clock.UtcNow);
            // The widget reads the schedule, so it is replaced on the thread that draws it.
            await _dispatcher.InvokeAsync(() =>
            {
                _upcoming = refreshed;
                UpdateIdleWidget();
            });
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception failure)
        {
            AppLog.Warning("AppController", "Could not load the next published session.", failure);
        }
        finally { _scheduleRefreshRunning = false; }
    }

    public void ShowSettings() => _ = ShowSettingsAsync();

    private async Task ShowSettingsAsync()
    {
        if (!_dispatcher.CheckAccess())
        {
            await _dispatcher.InvokeAsync(ShowSettingsAsync).Task.Unwrap();
            return;
        }

        _settingsWindow ??= new SettingsWindow(this, _settings, ApplySettingsAsync);
        _settingsWindow.LoadSettings(_settings);
        foreach (var snapshot in _latestSnapshots.Values) _settingsWindow.UpdateSnapshot(snapshot);
        _settingsWindow.Show();
        _settingsWindow.Activate();
        // The schedule comes from third-party calendars, so it loads after the window is already usable.
        _ = RefreshUpcomingSessionsAsync(_settingsWindow);
    }

    private async Task RefreshUpcomingSessionsAsync(SettingsWindow window)
    {
        try
        {
            // Only enabled championships are contacted, so a disabled series never causes a network call.
            await window.RefreshUpcomingSessionsAsync(_schedule, EnabledChampionships(_settings), _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception failure)
        {
            AppLog.Warning("AppController", "Could not load the published schedule.", failure);
        }
    }

    internal static HashSet<Championship> EnabledChampionships(UserSettings settings) =>
        Enum.GetValues<Championship>().Where(settings.IsChampionshipEnabled).ToHashSet();

    /// <summary>The live feeds only depend on which championships run and how their sessions are cycled.</summary>
    private static bool FeedsDiffer(UserSettings left, UserSettings right) =>
        !EnabledChampionships(left).SetEquals(EnabledChampionships(right)) ||
        left.RotationSeconds != right.RotationSeconds ||
        left.CompletedSessionRetentionMinutes != right.CompletedSessionRetentionMinutes;

    private async Task ApplySettingsAsync(UserSettings settings)
    {
        var restartFeeds = FeedsDiffer(settings, _settings);
        _settingsStore.Save(settings);
        _settings = settings.Normalize();
        WindowsStartupRegistration.Apply(_settings.AutoStartOnWindowsBoot);
        _taskbar?.ApplySettings(_settings);
        // Enabling a championship is what allows its calendar to be contacted, so the schedule follows the change.
        if (restartFeeds && _settingsWindow is { } window) _ = RefreshUpcomingSessionsAsync(window);
        if (restartFeeds) _upcoming = [];
        UpdateIdleWidget();
        if (restartFeeds && !TestMode) await SwitchSourceAsync(TimingSourceMode.Live, force: true);
    }

    public void OpenLogs()
    {
        Directory.CreateDirectory(AppLog.Folder);
        Process.Start(new ProcessStartInfo("explorer.exe", AppLog.Folder) { UseShellExecute = true });
    }
    public string StartFeedDump(string folder)
    {
        var path = FeedDump.Start(folder);
        Log($"Feed dump started: {path}");
        return path;
    }

    public void StopFeedDump()
    {
        if (FeedDump.Folder is null) return;
        Log($"Feed dump stopped: {FeedDump.Folder}");
        FeedDump.Stop();
    }
    private static void Log(string message) => AppLog.Info("AppController", message);
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _renderTimer.Stop();
        _healthTimer.Stop();
        _idleTimer.Stop();
        await _lifetime.CancelAsync();
        await _sourceSwitch.WaitAsync();
        try
        {
            if (_source is not null)
            {
                Detach(_source);
                await _source.DisposeAsync();
                _source = null;
            }
        }
        finally { _sourceSwitch.Release(); }
        _taskbar?.Close();
        _settingsWindow?.ClosePermanently();
        _schedule.Dispose();
        _nascarFeed?.Dispose();
        _sourceSwitch.Dispose();
        _lifetime.Dispose();
    }
}
