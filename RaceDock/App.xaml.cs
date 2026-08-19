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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();
        _mutex = new Mutex(true, @"Local\RaceDock.Singleton", out var first);
        if (!first) { Shutdown(); return; }
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        _controller = new AppController(Dispatcher);
        BuildTray();
        _controller.StatusChanged += status => Dispatcher.Invoke(() => { if (_tray is not null) { _tray.Text = $"RaceDock — {status}"; _tray.Icon = status is ConnectionState.Connected ? SystemIcons.Information : status is ConnectionState.Stale or ConnectionState.Faulted ? SystemIcons.Warning : SystemIcons.Application; } if (_statusItem is not null) _statusItem.Text = $"Connection: {status}"; });
        _ = _controller.StartLiveAsync();
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
        _tray = new System.Windows.Forms.NotifyIcon { Icon = SystemIcons.Application, Text = "RaceDock", Visible = true, ContextMenuStrip = menu };
        _tray.DoubleClick += (_, _) => _controller?.ShowSettings();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose(); _controller?.DisposeAsync().AsTask().GetAwaiter().GetResult(); _mutex?.Dispose(); base.OnExit(e);
    }
}
