// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using MotorsportTaskbar.Core;

namespace MotorsportTaskbar.App;

public sealed class DeveloperWindow : Window
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
    private readonly TextBlock _status = new();
    private readonly System.Windows.Controls.TextBox _topFive = new() { Text = "NOR:LEAD,PIA:+0.8,LEC:+1.5,VER:+2.0,RUS:+2.8", MinWidth = 430 };
    private readonly Slider _speed = new() { Minimum = .25, Maximum = 20, Value = 4, Width = 240, TickFrequency = .25 };
    private readonly Dictionary<ScenarioCommand, System.Windows.Controls.Primitives.ToggleButton> _commandButtons = [];
    private bool _syncingButtons;

    public DeveloperWindow(AppController controller)
    {
        _controller = controller;
        Title = "MotorsportTaskbar Developer";
        Width = 720;
        Height = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        SetResourceReference(FontFamilyProperty, "MotorsportFontFamily");

        var root = new StackPanel { Margin = new Thickness(18) };
        _status.Margin = new Thickness(0, 0, 0, 12);
        _status.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(_status);
        root.Children.Add(new TextBlock { Text = "Deterministic scenario", FontWeight = FontWeights.Bold, FontSize = 16 });

        var transport = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        var pause = TransportToggle("Pause", true);
        var resume = TransportToggle("Resume", false);
        pause.Click += (_, _) =>
        {
            if (_syncingButtons) return;
            if (pause.IsChecked == true)
            {
                if (TryScenario(out var scenario)) scenario.Pause();
                resume.IsChecked = false;
            }
            else
            {
                if (TryScenario(out var scenario)) scenario.Resume();
                resume.IsChecked = true;
            }
        };
        resume.Click += (_, _) =>
        {
            if (_syncingButtons) return;
            if (resume.IsChecked == true)
            {
                if (TryScenario(out var scenario)) scenario.Resume();
                pause.IsChecked = false;
            }
            else
            {
                if (TryScenario(out var scenario)) scenario.Pause();
                pause.IsChecked = true;
            }
        };
        transport.Children.Add(pause);
        transport.Children.Add(resume);
        transport.Children.Add(Button("Single step", () =>
        {
            if (TryScenario(out var scenario)) scenario.Step();
        }));
        transport.Children.Add(Button("Restart", () =>
        {
            if (TryScenario(out var scenario))
            {
                scenario.Restart();
                _syncingButtons = true;
                pause.IsChecked = true;
                resume.IsChecked = false;
                _syncingButtons = false;
            }
        }));
        transport.Children.Add(new TextBlock { Text = " Speed ", VerticalAlignment = VerticalAlignment.Center });
        transport.Children.Add(_speed);
        root.Children.Add(transport);
        _speed.ValueChanged += (_, _) =>
        {
            if (_controller.Scenario is { } scenario) scenario.Speed = _speed.Value;
        };

        root.Children.Add(new TextBlock
        {
            Text = "Track and feed controls (click again to clear a persistent state)",
            Margin = new Thickness(0, 4, 0, 4)
        });
        var controls = new WrapPanel();
        foreach (var command in Enum.GetValues<ScenarioCommand>())
        {
            var button = CommandToggle(command);
            _commandButtons[command] = button;
            controls.Children.Add(button);
        }
        root.Children.Add(controls);

        root.Children.Add(new TextBlock { Text = "Top five (CODE:GAP; missing gap is shown as —)", Margin = new Thickness(0, 16, 0, 4) });
        var edit = new WrapPanel();
        edit.Children.Add(_topFive);
        edit.Children.Add(Button("Apply", ApplyTopFive));
        root.Children.Add(edit);

        var diagnostic = new CheckBox
        {
            Content = "Record bounded raw live feed (max 2 MiB, no credentials)",
            Margin = new Thickness(0, 18, 0, 0)
        };
        diagnostic.Checked += (_, _) => _controller.SetDiagnosticRecording(true);
        diagnostic.Unchecked += (_, _) => _controller.SetDiagnosticRecording(false);
        root.Children.Add(diagnostic);

        var note = new TextBlock
        {
            Text = "Opening this panel enables deterministic test mode. The script starts paused; use Resume or Single step to advance it.",
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        note.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        root.Children.Add(note);
        Content = root;
        Closing += (_, e) => { e.Cancel = true; Hide(); };
    }

    public void UpdateSnapshot(TimingSnapshot snapshot)
    {
        var mode = _controller.Scenario?.IsPaused == true ? "PAUSED" : "RUNNING";
        var timed = snapshot.Session.Contains("Practice", StringComparison.OrdinalIgnoreCase) || snapshot.Session.Contains("Qualifying", StringComparison.OrdinalIgnoreCase);
        var progress = timed && !string.IsNullOrWhiteSpace(snapshot.TimeRemaining) ? $"Time {snapshot.TimeRemaining}" : $"Lap {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}";
        _status.Text = $"{snapshot.ConnectionState} · {snapshot.Meeting} {snapshot.Session} · {progress} · {snapshot.TrackCondition} · {mode}";
        SyncState(snapshot);
    }

    private void ApplyTopFive()
    {
        if (!TryScenario(out var scenario)) return;
        var values = _topFive.Text
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

    private System.Windows.Controls.Primitives.ToggleButton CommandToggle(ScenarioCommand command)
    {
        var button = new System.Windows.Controls.Primitives.ToggleButton
        {
            Content = Label(command),
            Margin = new Thickness(2),
            Padding = new Thickness(8, 4, 8, 4),
            MinWidth = 78,
            IsThreeState = false
        };
        button.Click += (_, _) => ExecuteCommand(command, button);
        return button;
    }

    private void ExecuteCommand(ScenarioCommand command, System.Windows.Controls.Primitives.ToggleButton button)
    {
        if (_syncingButtons || !TryScenario(out var scenario)) return;

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

    private void SyncState(TimingSnapshot snapshot)
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

        var connectionCommand = snapshot.ConnectionState switch
        {
            ConnectionState.Connected => ScenarioCommand.Reconnect,
            ConnectionState.Disconnected => ScenarioCommand.Disconnect,
            ConnectionState.Stale => ScenarioCommand.Stale,
            _ => (ScenarioCommand?)null
        };
        if (connectionCommand is { } command) SetConnectionState(command);
    }

    private void SetTrackState(ScenarioCommand active)
    {
        _syncingButtons = true;
        foreach (var command in TrackCommands) SetChecked(command, command == active);
        _syncingButtons = false;
    }

    private void SetConnectionState(ScenarioCommand active)
    {
        _syncingButtons = true;
        SetChecked(ScenarioCommand.Disconnect, active == ScenarioCommand.Disconnect);
        SetChecked(ScenarioCommand.Stale, active == ScenarioCommand.Stale);
        SetChecked(ScenarioCommand.Reconnect, active == ScenarioCommand.Reconnect);
        _syncingButtons = false;
    }

    private void SetChecked(ScenarioCommand command, bool value)
    {
        if (_commandButtons.TryGetValue(command, out var button)) button.IsChecked = value;
    }

    private bool TryScenario(out ITimingScenarioSource scenario)
    {
        scenario = _controller.Scenario!;
        if (scenario is not null) return true;
        _status.Text = "Test mode is still starting; try the control again in a moment.";
        return false;
    }

    private static System.Windows.Controls.Primitives.ToggleButton TransportToggle(string text, bool isChecked) => new()
    {
        Content = text,
        IsChecked = isChecked,
        Margin = new Thickness(2),
        Padding = new Thickness(8, 4, 8, 4),
        MinWidth = 70
    };

    private static System.Windows.Controls.Button Button(string text, Action action)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = text,
            Margin = new Thickness(2),
            Padding = new Thickness(8, 4, 8, 4),
            MinWidth = 70
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static string Label(ScenarioCommand value) => value switch
    {
        ScenarioCommand.VirtualSafetyCar => "VSC",
        ScenarioCommand.VirtualSafetyCarEnding => "VSC ending",
        _ => value.ToString()
    };
}
