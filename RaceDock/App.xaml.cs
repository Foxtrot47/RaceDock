// SPDX-License-Identifier: GPL-3.0-or-later
using System.Drawing;
using System.Threading;
using System.Windows;
using RaceDock.Core;

namespace RaceDock.App;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Windows.Forms.ToolStripMenuItem? _statusItem;
    private AppController? _controller;
    private SecondInstanceSignal? _secondInstanceSignal;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();
        _mutex = new Mutex(true, @"Local\RaceDock.Singleton", out var first);
        // A second launch is a request to see the app, so hand it to the instance that owns the UI.
        if (!first) { SecondInstanceSignal.RequestExistingWindow(); Shutdown(); return; }
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        InstallCrashHandlers();
        var automaticStartup = LaunchMode.IsAutomaticStartup(e.Args);
        AppLog.Info("App", $"Started version {AppVersion.Display} ({(automaticStartup ? "sign-in launch" : "user launch")}).");
        _controller = new AppController(Dispatcher);
        BuildTray();
        ReportPreviousCrash();
        _controller.StatusChanged += status => Dispatcher.Invoke(() => { if (_tray is not null) _tray.Text = $"RaceDock — {status}"; if (_statusItem is not null) _statusItem.Text = $"Connection: {status}"; });
        _secondInstanceSignal = SecondInstanceSignal.Listen(() => Dispatcher.Invoke(() => _controller?.ShowSettings()));
        // Windows starts the app silently at sign-in; every other launch has to show something.
        if (!automaticStartup) _controller.ShowSettings();
        _ = StartFeedAsync(LaunchArguments.ReplayPath(e.Args));
    }

    private void InstallCrashHandlers()
    {
        // Neither fatal handler marks the exception handled: carrying on from an unknown fault risks
        // writing corrupted settings over good ones.
        DispatcherUnhandledException += (_, args) => Fatal("Dispatcher", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Fatal("AppDomain", args.ExceptionObject as Exception);
        // A faulted background task does not end the process, so this one is survivable and observed.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Task", "Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
    }

    private static void Fatal(string source, Exception? exception)
    {
        AppLog.Fatal(source, "Unhandled exception ended the session.", exception);
        CrashMarker.Set(exception?.Message ?? "Unknown error.");
    }

    private void ReportPreviousCrash()
    {
        var summary = CrashMarker.Consume(); if (summary is null) return;
        _tray?.ShowBalloonTip(10_000, "RaceDock closed unexpectedly",
            $"The last session ended with: {summary}", System.Windows.Forms.ToolTipIcon.Warning);
    }

    private async Task StartFeedAsync(string? replayPath)
    {
        // Nothing awaits this task, so it has to report its own failure.
        try
        {
            if (_controller is null) return;
            if (string.IsNullOrWhiteSpace(replayPath)) await _controller.StartLiveAsync();
            else await _controller.LoadArchiveReplayAsync(replayPath);
        }
        catch (OperationCanceledException) when (Dispatcher.HasShutdownStarted) { }
        catch (Exception exception) { AppLog.Error("App", "Timing source failed to start.", exception); }
    }

    private void BuildTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        _statusItem = new System.Windows.Forms.ToolStripMenuItem("Connection: starting") { Enabled = false }; menu.Items.Add(_statusItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem($"Version {AppVersion.Display}") { Enabled = false });
        menu.Items.Add("Settings", null, (_, _) => _controller?.ShowSettings());
        menu.Items.Add("Restart feed", null, async (_, _) =>
        {
            // Restart was interrupted because the application is shutting down.
            try { if (_controller is not null) await _controller.RestartAsync(); }
            catch (OperationCanceledException) when (System.Windows.Application.Current.Dispatcher.HasShutdownStarted) { }
        });
        menu.Items.Add("View logs", null, (_, _) => _controller?.OpenLogs());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => { if (_controller is not null) await _controller.DisposeAsync(); Shutdown(); });
        _tray = new System.Windows.Forms.NotifyIcon { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!), Text = "RaceDock", Visible = true, ContextMenuStrip = menu };
        _tray.DoubleClick += (_, _) => _controller?.ShowSettings();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Info("App", "Exited.");
        _secondInstanceSignal?.Dispose(); _tray?.Dispose(); _controller?.DisposeAsync().AsTask().GetAwaiter().GetResult(); _mutex?.Dispose(); base.OnExit(e);
    }
}
