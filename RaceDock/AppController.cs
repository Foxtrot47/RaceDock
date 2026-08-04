// SPDX-License-Identifier: GPL-3.0-or-later
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
    private readonly UserSettingsStore _settingsStore = new();
    private readonly DispatcherTimer _renderTimer;
    private readonly DispatcherTimer _healthTimer;
    private readonly SemaphoreSlim _sourceSwitch = new(1, 1);
    private ILiveTimingSource? _source;
    private TimingSnapshot? _pending;
    private TaskbarWindow? _taskbar;
    private SettingsWindow? _settingsWindow;
    private readonly CancellationTokenSource _lifetime = new();
    private UserSettings _settings;
    private bool _diagnosticRecording;
    private bool _snapshotLogged;
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
        _dispatcher = dispatcher; _alerts = new(_clock); _processor = new(_clock, _alerts); _settings = _settingsStore.Load();
        try { WindowsStartupRegistration.Apply(_settings.AutoStartOnWindowsBoot); }
        catch (Exception ex) { Log($"Windows startup registration failed: {ex.Message}"); }
        _alerts.VisibleAlertChanged += alert => _dispatcher.BeginInvoke(() => _taskbar?.SetAlert(alert));
        _renderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => RenderPending(), dispatcher);
        _healthTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            _alerts.Tick();
            if (_source is not ITimingPlaybackSource) _processor.MarkStale(TimeSpan.FromSeconds(12));
        }, dispatcher);
        _renderTimer.Start(); _healthTimer.Start();
    }

    public Task StartLiveAsync() => SwitchSourceAsync(TimingSourceMode.Live);
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
        SwitchSourceAsync(enabled ? TimingSourceMode.Scenario : TimingSourceMode.Live);

    public async Task LoadArchiveReplayAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        await _sourceSwitch.WaitAsync(_lifetime.Token);
        try
        {
            var replay = await TimingArchiveReplaySource.CreateAsync(fullPath, _clock, _lifetime.Token);
            await SwitchSourceCoreAsync(TimingSourceMode.ArchiveReplay, replay);
        }
        finally { _sourceSwitch.Release(); }
    }

    public Task ReturnToLiveAsync() => SwitchSourceAsync(TimingSourceMode.Live);

    private async Task SwitchSourceAsync(TimingSourceMode mode, bool force = false)
    {
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

    private ILiveTimingSource CreateLiveSource()
    {
        var logs = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logs);
        var factories = new List<Func<ILiveTimingSource>>();
        if (_settings.EnableFormula1) factories.Add(() => new F1LiveTimingSource(_processor, _clock, logs) { DiagnosticRecordingEnabled = _diagnosticRecording });
        if (_settings.EnableFormula2) factories.Add(() => new F2LiveTimingSource(_clock, _alerts));
        if (_settings.EnableFormula3) factories.Add(() => new F2LiveTimingSource(_clock, _alerts, "F3"));
        if (_settings.EnableGtWorldChallengeEurope) factories.Add(() => new GtwcLiveTimingSource(_clock, _alerts));
        if (_settings.EnableWrc) factories.Add(() => new WrcLiveTimingSource(_clock, _alerts));
        if (_settings.EnableImsa) factories.Add(() => new ImsaLiveTimingSource(_clock, _alerts));
        return new CompositeLiveTimingSource(factories, _clock, TimeSpan.FromSeconds(_settings.RotationSeconds));
    }

    private async Task SwitchSourceCoreAsync(TimingSourceMode mode, ILiveTimingSource candidate)
    {
        if (_source is not null)
        {
            Detach(_source);
            await _source.DisposeAsync();
            _source = null;
        }
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
        var enabled = string.Join(" + ", new[]
        {
            _settings.EnableFormula1 ? "F1" : null,
            _settings.EnableFormula2 ? "F2" : null,
            _settings.EnableFormula3 ? "F3" : null,
            _settings.EnableGtWorldChallengeEurope ? "GTWC" : null,
            _settings.EnableWrc ? "WRC" : null,
            _settings.EnableImsa ? "IMSA" : null
        }.Where(value => value is not null));
        Log(source switch
        {
            ITimingArchiveSource archive => $"Source started: archive replay {archive.ArchiveName}; {archive.FrameCount} frames",
            ITimingScenarioSource => "Source started: deterministic test scenario (paused)",
            _ => $"Source started: {enabled}; rotation {_settings.RotationSeconds}s"
        });
    }

    private void Attach(ILiveTimingSource source)
    {
        source.SnapshotReceived += OnSnapshot; source.DeltaReceived += OnDelta; source.ConnectionChanged += OnConnection; source.Failed += OnFailure;
    }
    private void Detach(ILiveTimingSource source)
    {
        source.SnapshotReceived -= OnSnapshot; source.DeltaReceived -= OnDelta; source.ConnectionChanged -= OnConnection; source.Failed -= OnFailure;
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
    private void OnFailure(FeedFailure failure) => Log($"Feed failure: {failure.Message}");
    private void RenderPending()
    {
        var snapshot = Interlocked.Exchange(ref _pending, null); if (snapshot is null) return;
        if (snapshot.Lifecycle == SessionLifecycle.OffSession || (snapshot.Lifecycle == SessionLifecycle.Ended && Archive is null)) { _taskbar?.Hide(); return; }
        _taskbar ??= new TaskbarWindow(_settings); _taskbar.UpdateSnapshot(snapshot); _taskbar.SetAlert(_alerts.Current); if (!_taskbar.IsVisible) _taskbar.Show();
        _settingsWindow?.UpdateSnapshot(snapshot);
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
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private async Task ApplySettingsAsync(UserSettings settings)
    {
        var restartFeeds = settings.EnableFormula1 != _settings.EnableFormula1 ||
            settings.EnableFormula2 != _settings.EnableFormula2 ||
            settings.EnableFormula3 != _settings.EnableFormula3 ||
            settings.EnableGtWorldChallengeEurope != _settings.EnableGtWorldChallengeEurope ||
            settings.EnableWrc != _settings.EnableWrc ||
            settings.EnableImsa != _settings.EnableImsa ||
            settings.RotationSeconds != _settings.RotationSeconds;
        _settingsStore.Save(settings);
        _settings = settings.Normalize();
        WindowsStartupRegistration.Apply(_settings.AutoStartOnWindowsBoot);
        _taskbar?.ApplySettings(_settings);
        if (restartFeeds && !TestMode) await SwitchSourceAsync(TimingSourceMode.Live, force: true);
    }

    public void OpenLogs()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Logs"); Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
    }
    public void SetDiagnosticRecording(bool enabled) { _diagnosticRecording = enabled; Log($"Diagnostic recording: {enabled}. A currently active connection applies this after feed restart."); }
    private static void Log(string message)
    {
        try { var dir = Path.Combine(AppContext.BaseDirectory, "Logs"); Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, "app.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); } catch { }
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _renderTimer.Stop();
        _healthTimer.Stop();
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
        _sourceSwitch.Dispose();
        _lifetime.Dispose();
    }
}
