// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using RaceDock.Core;

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
        var mode = PlaybackState(_controller.Playback, _controller.Archive?.IsCompleted == true);
        var progress = !string.IsNullOrWhiteSpace(snapshot.TimeRemaining)
            ? $"Time {snapshot.TimeRemaining}"
            : $"Lap {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}";
        var archive = _controller.Archive is { } replay ? $" · frame {replay.FrameIndex}/{replay.FrameCount}" : "";
        TestStatusText.Text = $"{snapshot.ConnectionState} · {snapshot.Meeting} {snapshot.Session} · {progress} · {snapshot.TrackCondition} · {mode}{archive}";
        if (_controller.Playback is { } playback) SetTransportState(playback.IsPaused);
        if (_controller.Scenario is not null) SyncScenarioState(snapshot);
    }

    public void SyncMode()
    {
        _syncingControls = true;
        TestModeToggle.IsChecked = _controller.Scenario is not null;
        _syncingControls = false;
        TestingControlsPanel.IsEnabled = _controller.Playback is not null;
        ScenarioControlsPanel.IsEnabled = _controller.Scenario is not null;
        if (_controller.Playback is { } playback)
        {
            SpeedSlider.Value = playback.Speed;
            SetTransportState(playback.IsPaused);
            if (_controller.Archive is { } archive)
            {
                ArchivePathTextBox.Text = archive.ArchivePath;
                ArchivePathTextBox.ToolTip = archive.ArchiveName;
                TestStatusText.Text = $"Archive ready · {archive.FrameCount} frames · {PlaybackState(playback, archive.IsCompleted)}";
            }
            else
            {
                ArchivePathTextBox.Text = "";
                ArchivePathTextBox.ToolTip = null;
                TestStatusText.Text = playback.IsPaused ? "Test scenario ready · PAUSED" : "Test scenario ready · RUNNING";
            }
        }
        else
        {
            ArchivePathTextBox.Text = "";
            ArchivePathTextBox.ToolTip = null;
            TestStatusText.Text = "Live streams are active.";
        }
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
        TestStatusText.Text = "Loading archive…";
        try
        {
            await _controller.LoadArchiveReplayAsync(picker.FileName);
            SyncMode();
        }
        catch (Exception ex)
        {
            TestStatusText.Text = $"Could not load archive: {ex.Message}";
        }
    }

    private async void ReturnToLiveButton_Click(object sender, RoutedEventArgs e)
    {
        TestStatusText.Text = "Returning to live streams…";
        try
        {
            await _controller.ReturnToLiveAsync();
            SyncMode();
        }
        catch (Exception ex)
        {
            TestStatusText.Text = $"Could not restore live streams: {ex.Message}";
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e) => SetPaused(PauseButton.IsChecked == true);

    private void ResumeButton_Click(object sender, RoutedEventArgs e) => SetPaused(ResumeButton.IsChecked != true);

    private void SingleStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPlayback(out var playback)) return;
        playback.Step();
        SetTransportState(true);
    }

    private void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPlayback(out var playback)) return;
        playback.Restart();
        SetTransportState(true);
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_controller.Playback is { } playback) playback.Speed = SpeedSlider.Value;
    }

    private async void TestModeToggle_Checked(object sender, RoutedEventArgs e) => await ToggleTestModeAsync(true);

    private async void TestModeToggle_Unchecked(object sender, RoutedEventArgs e) => await ToggleTestModeAsync(false);

    private async Task ToggleTestModeAsync(bool enabled)
    {
        if (_syncingControls) return;
        TestModeToggle.IsEnabled = false;
        TestStatusText.Text = enabled ? "Starting test mode…" : "Returning to live streams…";
        try
        {
            await _controller.SetTestModeAsync(enabled);
            SyncMode();
        }
        catch (Exception ex)
        {
            TestStatusText.Text = $"Could not switch feed: {ex.Message}";
            _syncingControls = true;
            TestModeToggle.IsChecked = _controller.Scenario is not null;
            _syncingControls = false;
        }
        finally
        {
            TestModeToggle.IsEnabled = true;
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

    private void SetPaused(bool paused)
    {
        if (_syncingControls || !TryPlayback(out var playback)) return;
        if (paused) playback.Pause(); else playback.Resume();
        SetTransportState(playback.IsPaused);
        if (_controller.Archive is { } archive)
            TestStatusText.Text = $"Archive · frame {archive.FrameIndex}/{archive.FrameCount} · {PlaybackState(playback, archive.IsCompleted)}";
    }

    private void SetTransportState(bool paused)
    {
        _syncingControls = true;
        PauseButton.IsChecked = paused;
        ResumeButton.IsChecked = !paused;
        _syncingControls = false;
    }

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
        TestStatusText.Text = "Load an archive or enable deterministic test mode before using playback controls.";
        return false;
    }

    private bool TryScenario(out ITimingScenarioSource scenario)
    {
        scenario = _controller.Scenario!;
        if (scenario is not null) return true;
        TestStatusText.Text = "Enable deterministic test mode before using scenario controls.";
        return false;
    }

    private static string PlaybackState(ITimingPlaybackSource? playback, bool completed)
    {
        if (completed) return "COMPLETED";
        return playback?.IsPaused == true ? "PAUSED" : "RUNNING";
    }
}
