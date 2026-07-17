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
    private readonly DispatcherTimer _renderTimer;
    private readonly DispatcherTimer _healthTimer;
    private ILiveTimingSource? _source;
    private TimingSnapshot? _pending;
    private TaskbarWindow? _taskbar;
    private DeveloperWindow? _developer;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _testMode;
    private bool _diagnosticRecording;
    private bool _snapshotLogged;
    private bool _disposed;
    public event Action<ConnectionState>? StatusChanged;
    public ITimingScenarioSource? Scenario => _source as ITimingScenarioSource;
    public bool TestMode => _testMode;

    public AppController(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher; _alerts = new(_clock); _processor = new(_clock, _alerts);
        _alerts.VisibleAlertChanged += alert => _dispatcher.BeginInvoke(() => _taskbar?.SetAlert(alert));
        _renderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => RenderPending(), dispatcher);
        _healthTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => { _alerts.Tick(); _processor.MarkStale(TimeSpan.FromSeconds(12)); }, dispatcher);
        _renderTimer.Start(); _healthTimer.Start();
    }

    public Task StartLiveAsync() => SwitchSourceAsync(false);
    public Task SetTestModeAsync(bool enabled) { _testMode = enabled; return SwitchSourceAsync(enabled); }
    public async Task RestartAsync() => await SwitchSourceAsync(_testMode);

    private async Task SwitchSourceAsync(bool scenario)
    {
        if (_source is not null) { Detach(_source); await _source.DisposeAsync(); }
        _alerts.Clear(); _processor.ProcessInitial(new System.Text.Json.Nodes.JsonObject());
        _snapshotLogged = false;
        _source = scenario
            ? new ScenarioTimingSource(_processor, _clock)
            : new WrcLiveTimingSource(TimingSnapshot.Hidden(_clock.UtcNow), _clock, _alerts);
        Attach(_source); await _source.StartAsync(_lifetime.Token);
        Log($"Source started: {(scenario ? "scenario (paused)" : "WRC live")}");
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
        if (snapshot.Lifecycle == SessionLifecycle.OffSession) { _taskbar?.Hide(); return; }
        _taskbar ??= new TaskbarWindow(); _taskbar.UpdateSnapshot(snapshot); if (!_taskbar.IsVisible) _taskbar.Show();
        _developer?.UpdateSnapshot(snapshot);
    }

    public void ShowDeveloper() => _ = ShowDeveloperAsync();

    private async Task ShowDeveloperAsync()
    {
        if (!_dispatcher.CheckAccess())
        {
            await _dispatcher.InvokeAsync(ShowDeveloperAsync).Task.Unwrap();
            return;
        }

        if (!_testMode) await SetTestModeAsync(true);
        _developer ??= new DeveloperWindow(this);
        _developer.Show();
        _developer.Activate();
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
        _taskbar?.Close(); _developer?.Close(); _lifetime.Dispose();
    }
}
