// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Windows.Threading;
using MotorsportTaskbar.Core;

namespace MotorsportTaskbar.App;

public sealed class AppController : IAsyncDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly SystemClock _clock = new();
    private readonly AlertArbiter _alerts;
    private readonly TimingStateProcessor _processor;
    private readonly UserSettingsStore _settingsStore = new();
    private readonly DispatcherTimer _renderTimer;
    private readonly DispatcherTimer _healthTimer;
    private ILiveTimingSource? _source;
    private TimingSnapshot? _pending;
    private TaskbarWindow? _taskbar;
    private SettingsWindow? _settingsWindow;
    private readonly CancellationTokenSource _lifetime = new();
    private UserSettings _settings;
    private bool _testMode;
    private bool _diagnosticRecording;
    private bool _snapshotLogged;
    private bool _disposed;
    public event Action<ConnectionState>? StatusChanged;
    public UserSettings Settings => _settings;
    public ITimingScenarioSource? Scenario => _source as ITimingScenarioSource;
    public bool TestMode => _testMode;

    public AppController(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher; _alerts = new(_clock); _processor = new(_clock, _alerts); _settings = _settingsStore.Load();
        _alerts.VisibleAlertChanged += alert => _dispatcher.BeginInvoke(() => _taskbar?.SetAlert(alert));
        _renderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => RenderPending(), dispatcher);
        _healthTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            _alerts.Tick();
            if (!_testMode) _processor.MarkStale(TimeSpan.FromSeconds(12));
        }, dispatcher);
        _renderTimer.Start(); _healthTimer.Start();
    }

    public Task StartLiveAsync() => SwitchSourceAsync();
    public Task RestartAsync() => SwitchSourceAsync();
    public async Task SetTestModeAsync(bool enabled)
    {
        if (_testMode == enabled) return;
        _testMode = enabled;
        await SwitchSourceAsync();
    }

    private async Task SwitchSourceAsync()
    {
        if (_source is not null) { Detach(_source); await _source.DisposeAsync(); }
        _pending = null;
        _taskbar?.Hide();
        _alerts.Clear(); _processor.ProcessInitial(new System.Text.Json.Nodes.JsonObject());
        _snapshotLogged = false;
        var logs = Path.Combine(AppContext.BaseDirectory, "Logs"); Directory.CreateDirectory(logs);
        if (_testMode)
        {
            _source = new ScenarioTimingSource(_processor, _clock);
        }
        else
        {
            var factories = new List<Func<ILiveTimingSource>>();
            if (_settings.EnableFormula1) factories.Add(() => new F1LiveTimingSource(_processor, _clock, logs) { DiagnosticRecordingEnabled = _diagnosticRecording });
            if (_settings.EnableFormula2) factories.Add(() => new F2LiveTimingSource(_clock, _alerts));
            if (_settings.EnableFormula3) factories.Add(() => new F2LiveTimingSource(_clock, _alerts, "F3"));
            if (_settings.EnableWrc) factories.Add(() => new WrcLiveTimingSource(TimingSnapshot.Hidden(_clock.UtcNow), _clock, _alerts));
            _source = new CompositeLiveTimingSource(factories, _clock, TimeSpan.FromSeconds(_settings.RotationSeconds));
        }
        Attach(_source); await _source.StartAsync(_lifetime.Token);
        var enabled = string.Join(" + ", new[]
        {
            _settings.EnableFormula1 ? "F1" : null,
            _settings.EnableFormula2 ? "F2" : null,
            _settings.EnableFormula3 ? "F3" : null,
            _settings.EnableWrc ? "WRC" : null
        }.Where(value => value is not null));
        Log(_testMode ? "Source started: deterministic test scenario (paused)" : $"Source started: {enabled}; rotation {_settings.RotationSeconds}s");
    }

    private void Attach(ILiveTimingSource source)
    {
        source.SnapshotReceived += OnSnapshot; source.ConnectionChanged += OnConnection; source.Failed += OnFailure;
    }
    private void Detach(ILiveTimingSource source)
    {
        source.SnapshotReceived -= OnSnapshot; source.ConnectionChanged -= OnConnection; source.Failed -= OnFailure;
    }
    private void OnSnapshot(TimingSnapshot snapshot)
    {
        _pending = snapshot;
        if (_snapshotLogged || snapshot.Competitors.Count == 0) return;
        _snapshotLogged = true;
        Log($"Live snapshot: {snapshot.Meeting} — {snapshot.Session}; {snapshot.Competitors.Count} competitors; leader {snapshot.Competitors[0].Code}; time remaining {snapshot.TimeRemaining ?? "—"}");
    }
    private void OnConnection(ConnectionState state) { StatusChanged?.Invoke(state); Log($"Connection: {state}"); }
    private void OnFailure(FeedFailure failure) => Log($"Feed failure: {failure.Message}");
    private void RenderPending()
    {
        var snapshot = Interlocked.Exchange(ref _pending, null); if (snapshot is null) return;
        if (snapshot.Lifecycle is SessionLifecycle.OffSession or SessionLifecycle.Ended) { _taskbar?.Hide(); return; }
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
            settings.EnableWrc != _settings.EnableWrc ||
            settings.RotationSeconds != _settings.RotationSeconds;
        _settingsStore.Save(settings);
        _settings = settings.Normalize();
        _taskbar?.ApplySettings(_settings);
        if (restartFeeds && !_testMode) await SwitchSourceAsync();
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
        if (_disposed) return; _disposed = true;
        _renderTimer.Stop(); _healthTimer.Stop(); await _lifetime.CancelAsync();
        if (_source is not null) { Detach(_source); await _source.DisposeAsync(); }
        _taskbar?.Close(); _settingsWindow?.ClosePermanently(); _lifetime.Dispose();
    }
}
