// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RaceDock.Core;
using Wpf.Ui.Controls;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Page = System.Windows.Controls.Page;

namespace RaceDock.App.Settings;

public sealed partial class TestingPage : Page
{
    private static readonly ScenarioCommand[] TrackCommands =
    [
        ScenarioCommand.Green,
        ScenarioCommand.Yellow,
        ScenarioCommand.DoubleYellow,
        ScenarioCommand.SafetyCar,
        ScenarioCommand.VirtualSafetyCar,
        ScenarioCommand.VirtualSafetyCarEnding,
        ScenarioCommand.RedFlag
    ];

    private static readonly string[] ArchiveExtensions = [".zip", ".har"];

    private readonly AppController _controller;
    private readonly Dictionary<ScenarioCommand, ToggleButton> _commandButtons;
    private bool _syncingControls = true;

    public TestingPage(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        _commandButtons = new Dictionary<ScenarioCommand, ToggleButton>
        {
            [ScenarioCommand.Yellow] = YellowButton,
            [ScenarioCommand.DoubleYellow] = DoubleYellowButton,
            [ScenarioCommand.SafetyCar] = SafetyCarButton,
            [ScenarioCommand.VirtualSafetyCar] = VirtualSafetyCarButton,
            [ScenarioCommand.VirtualSafetyCarEnding] = VirtualSafetyCarEndingButton,
            [ScenarioCommand.Green] = GreenButton,
            [ScenarioCommand.FastestLap] = FastestLapButton,
            [ScenarioCommand.RedFlag] = RedFlagButton,
            [ScenarioCommand.Chequered] = ChequeredButton,
            [ScenarioCommand.Disconnect] = DisconnectButton,
            [ScenarioCommand.Stale] = StaleButton,
            [ScenarioCommand.Reconnect] = ReconnectButton
        };
        _syncingControls = false;
        SyncMode();
    }

    public void UpdateSnapshot(TimingSnapshot snapshot)
    {
        if (!_controller.TestMode) return;
        var progress = !string.IsNullOrWhiteSpace(snapshot.TimeRemaining)
            ? $"Time {snapshot.TimeRemaining}"
            : $"Lap {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}";
        var session = string.IsNullOrWhiteSpace(snapshot.Meeting)
            ? snapshot.Session
            : $"{snapshot.Meeting} · {snapshot.Session}";
        SetSecondary($"{session} · {progress} · {snapshot.TrackCondition} · {snapshot.ConnectionState}");
        UpdateTransportState();
        if (_controller.Scenario is not null) SyncScenarioState(snapshot);
    }

    public void SyncMode()
    {
        _syncingControls = true;
        TestModeToggle.IsChecked = _controller.Scenario is not null;
        _syncingControls = false;

        TransportControls.IsEnabled = _controller.Playback is not null;
        ScenarioControlsPanel.IsEnabled = _controller.Scenario is not null;
        ReturnToLiveButton.IsEnabled = _controller.TestMode;
        ScenarioHintText.Text = _controller.Scenario is not null
            ? "Track and feed states apply immediately. Select an active state again to clear it."
            : "Enable the deterministic scenario to drive track, feed, and classification states.";

        if (_controller.Playback is { } playback)
        {
            _syncingControls = true;
            SpeedSlider.Value = playback.Speed;
            _syncingControls = false;
            SpeedValueText.Text = $"{playback.Speed:0.00}x";
        }

        ArchiveSummaryText.Text = _controller.Archive is { } archive
            ? $"{archive.ArchiveName} · {archive.FrameCount} frames"
            : "Drop a Timing71 ZIP export or HAR capture here, or browse for one.";
        UpdateTransportState();
    }

    /// <summary>Renders the single source of truth for what is driving the widget right now.</summary>
    private void UpdateTransportState()
    {
        var playback = _controller.Playback;
        var archive = _controller.Archive;
        var completed = archive?.IsCompleted == true;
        var paused = playback?.IsPaused == true;

        var (label, tint) = playback switch
        {
            null => ("LIVE", "SystemFillColorSuccessBackgroundBrush"),
            _ when completed => ("COMPLETED", "SystemFillColorNeutralBackgroundBrush"),
            _ when paused => ("PAUSED", "SystemFillColorCautionBackgroundBrush"),
            _ => ("REPLAY", "SystemFillColorAttentionBackgroundBrush")
        };
        TransportStateText.Text = label;
        TransportStateBadge.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, tint);

        TransportPrimaryText.Text = playback switch
        {
            null => "Live streams are active",
            _ when archive is not null => archive.ArchiveName,
            _ => "Deterministic scenario"
        };

        // Play and pause share one button so the transport never shows a control that does nothing.
        PlayPauseButton.Icon = paused
            ? new SymbolIcon { Symbol = SymbolRegular.Play24 }
            : new SymbolIcon { Symbol = SymbolRegular.Pause24 };
        PlayPauseButton.ToolTip = paused ? "Resume" : "Pause";

        if (archive is not null)
        {
            TransportFrameText.Text = $"{archive.FrameIndex}/{archive.FrameCount}";
            TransportFrameText.Visibility = Visibility.Visible;
        }
        else TransportFrameText.Visibility = Visibility.Collapsed;

        if (playback is null) SetSecondary(null);
    }

    private void SetSecondary(string? text)
    {
        TransportSecondaryText.Text = text ?? "";
        TransportSecondaryText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowError(string message)
    {
        TestingInfoBar.Message = message;
        TestingInfoBar.IsOpen = true;
    }

    private async void ChooseArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose timing archive",
            Filter = "Timing archives (*.zip;*.har)|*.zip;*.har|Timing71 exports (*.zip)|*.zip|HTTP archives (*.har)|*.har",
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        await LoadArchiveAsync(picker.FileName);
    }

    private void ArchiveCard_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedArchive(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void ArchiveCard_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetDroppedArchive(e, out var path)) return;
        e.Handled = true;
        await LoadArchiveAsync(path);
    }

    private static bool TryGetDroppedArchive(DragEventArgs e, out string path)
    {
        path = "";
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths) return false;
        var candidate = paths[0];
        if (!ArchiveExtensions.Contains(Path.GetExtension(candidate), StringComparer.OrdinalIgnoreCase)) return false;
        path = candidate;
        return true;
    }

    private async Task LoadArchiveAsync(string path)
    {
        TestingInfoBar.IsOpen = false;
        SetSecondary($"Loading {Path.GetFileName(path)}…");
        try
        {
            await _controller.LoadArchiveReplayAsync(path);
        }
        catch (Exception ex)
        {
            ShowError($"Could not load archive: {ex.Message}");
        }
        SyncMode();
    }

    private async void ReturnToLiveButton_Click(object sender, RoutedEventArgs e)
    {
        TestingInfoBar.IsOpen = false;
        SetSecondary("Returning to live streams…");
        try
        {
            await _controller.ReturnToLiveAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Could not restore live streams: {ex.Message}");
        }
        SyncMode();
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPlayback(out var playback)) return;
        if (playback.IsPaused) playback.Resume(); else playback.Pause();
        UpdateTransportState();
    }

    private void SingleStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPlayback(out var playback)) return;
        playback.Step();
        UpdateTransportState();
    }

    private void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPlayback(out var playback)) return;
        playback.Restart();
        UpdateTransportState();
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpeedValueText is not null) SpeedValueText.Text = $"{SpeedSlider.Value:0.00}x";
        if (_syncingControls) return;
        if (_controller.Playback is { } playback) playback.Speed = SpeedSlider.Value;
    }

    private async void TestModeToggle_Checked(object sender, RoutedEventArgs e) => await ToggleTestModeAsync(true);

    private async void TestModeToggle_Unchecked(object sender, RoutedEventArgs e) => await ToggleTestModeAsync(false);

    private async Task ToggleTestModeAsync(bool enabled)
    {
        if (_syncingControls) return;
        TestModeToggle.IsEnabled = false;
        TestingInfoBar.IsOpen = false;
        SetSecondary(enabled ? "Starting the scenario…" : "Returning to live streams…");
        try
        {
            await _controller.SetTestModeAsync(enabled);
        }
        catch (Exception ex)
        {
            ShowError($"Could not switch feed: {ex.Message}");
        }
        finally
        {
            TestModeToggle.IsEnabled = true;
            SyncMode();
        }
    }

    private void ScenarioCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: ScenarioCommand command } button) ExecuteCommand(command, button);
    }

    private void ExecuteCommand(ScenarioCommand command, ToggleButton button)
    {
        if (_syncingControls || !TryScenario(out var scenario)) return;
        if (TrackCommands.Contains(command))
        {
            var next = button.IsChecked == true ? command : ScenarioCommand.Green;
            scenario.Trigger(next);
            SetTrackState(next);
            return;
        }

        switch (command)
        {
            case ScenarioCommand.Disconnect:
            case ScenarioCommand.Stale:
                scenario.Trigger(button.IsChecked == true ? command : ScenarioCommand.Reconnect);
                SetConnectionState(button.IsChecked == true ? command : ScenarioCommand.Reconnect);
                break;
            case ScenarioCommand.Reconnect:
                scenario.Trigger(button.IsChecked == true ? ScenarioCommand.Reconnect : ScenarioCommand.Disconnect);
                SetConnectionState(button.IsChecked == true ? ScenarioCommand.Reconnect : ScenarioCommand.Disconnect);
                break;
            case ScenarioCommand.FastestLap:
                scenario.Trigger(command);
                SetChecked(command, false);
                break;
            case ScenarioCommand.Chequered:
                scenario.Trigger(command);
                SetChecked(command, true);
                break;
        }
    }

    private void ApplyTopFiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryScenario(out var scenario)) return;
        var values = TopFiveTextBox.Text
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Take(5)
            .Select(value =>
            {
                var pair = value.Split(':', 2);
                return new ManualStanding(pair[0], pair.Length == 2 && pair[1] != "—" ? pair[1] : null);
            })
            .ToList();
        scenario.SetTopFive(values);
    }

    private void DiagnosticRecordingToggle_Checked(object sender, RoutedEventArgs e) =>
        _controller.SetDiagnosticRecording(true);

    private void DiagnosticRecordingToggle_Unchecked(object sender, RoutedEventArgs e) =>
        _controller.SetDiagnosticRecording(false);

    private void SyncScenarioState(TimingSnapshot snapshot)
    {
        var track = snapshot.TrackCondition switch
        {
            TrackCondition.AllClear => ScenarioCommand.Green,
            TrackCondition.Yellow => ScenarioCommand.Yellow,
            TrackCondition.DoubleYellow => ScenarioCommand.DoubleYellow,
            TrackCondition.SafetyCar => ScenarioCommand.SafetyCar,
            TrackCondition.VirtualSafetyCar => ScenarioCommand.VirtualSafetyCar,
            TrackCondition.VirtualSafetyCarEnding => ScenarioCommand.VirtualSafetyCarEnding,
            TrackCondition.RedFlag => ScenarioCommand.RedFlag,
            _ => (ScenarioCommand?)null
        };
        if (track is { } trackCommand) SetTrackState(trackCommand);

        var connection = snapshot.ConnectionState switch
        {
            ConnectionState.Connected => ScenarioCommand.Reconnect,
            ConnectionState.Disconnected => ScenarioCommand.Disconnect,
            ConnectionState.Stale => ScenarioCommand.Stale,
            _ => (ScenarioCommand?)null
        };
        if (connection is { } connectionCommand) SetConnectionState(connectionCommand);
    }

    private void SetTrackState(ScenarioCommand active)
    {
        _syncingControls = true;
        foreach (var command in TrackCommands) SetChecked(command, command == active);
        _syncingControls = false;
    }

    private void SetConnectionState(ScenarioCommand active)
    {
        _syncingControls = true;
        SetChecked(ScenarioCommand.Disconnect, active == ScenarioCommand.Disconnect);
        SetChecked(ScenarioCommand.Stale, active == ScenarioCommand.Stale);
        SetChecked(ScenarioCommand.Reconnect, active == ScenarioCommand.Reconnect);
        _syncingControls = false;
    }

    private void SetChecked(ScenarioCommand command, bool value)
    {
        if (_commandButtons.TryGetValue(command, out var button)) button.IsChecked = value;
    }

    private bool TryPlayback(out ITimingPlaybackSource playback)
    {
        playback = _controller.Playback!;
        if (playback is not null) return true;
        ShowError("Load an archive or enable the deterministic scenario before using playback controls.");
        return false;
    }

    private bool TryScenario(out ITimingScenarioSource scenario)
    {
        scenario = _controller.Scenario!;
        if (scenario is not null) return true;
        ShowError("Enable the deterministic scenario before using scenario controls.");
        return false;
    }
}
