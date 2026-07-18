// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MotorsportTaskbar.Core;

namespace MotorsportTaskbar.App;

public sealed class SettingsWindow : Window
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
    private readonly Func<UserSettings, Task> _apply;
    private readonly CheckBox _formula1 = Toggle("Formula 1", "Official Formula 1 live timing");
    private readonly CheckBox _formula2 = Toggle("Formula 2", "Official FIA Formula 2 live timing");
    private readonly CheckBox _formula3 = Toggle("Formula 3", "Official FIA Formula 3 live timing");
    private readonly CheckBox _wrc = Toggle("World Rally Championship", "Live WRC stage timing");
    private readonly CheckBox _eventHeader = Toggle("Event name", "Show the championship and venue on the taskbar");
    private readonly CheckBox _sessionProgress = Toggle("Session progress", "Show session, clock, lap or stage below the event");
    private readonly CheckBox _gaps = Toggle("Driver gaps", "Show leader gaps below driver codes");
    private readonly CheckBox _positionColors = Toggle("Position colours", "Use coloured cards for the leading positions");
    private readonly CheckBox _manufacturerLogos = Toggle("Manufacturer logos", "Replace taskbar position cards with a team or manufacturer mark when the feed supplies one");
    private readonly CheckBox _flagAlerts = Toggle("Flag alerts", "Show FIA flag indicators beside the event name");
    private readonly Slider _driverCount = IntegerSlider(1, 5);
    private readonly Slider _rotation = IntegerSlider(2, 30);
    private readonly TextBlock _driverCountValue = ValueLabel();
    private readonly TextBlock _rotationValue = ValueLabel();
    private readonly TextBlock _message = new() { Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _save = new() { Content = "Save settings", MinWidth = 110, Padding = new Thickness(14, 7, 14, 7) };
    private readonly CheckBox _testMode = Toggle("Enable deterministic test mode", "Temporarily replace live streams with the scripted testing feed");
    private readonly StackPanel _testingControls = new();
    private readonly TextBlock _testStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 4, 2, 8) };
    private readonly System.Windows.Controls.TextBox _topFive = new() { Text = "NOR:LEAD,PIA:+0.8,LEC:+1.5,VER:+2.0,RUS:+2.8", MinWidth = 430 };
    private readonly Slider _speed = new() { Minimum = .25, Maximum = 20, Value = 4, Width = 240, TickFrequency = .25 };
    private readonly Dictionary<ScenarioCommand, System.Windows.Controls.Primitives.ToggleButton> _commandButtons = [];
    private readonly System.Windows.Controls.Primitives.ToggleButton _pause = TransportToggle("Pause", true);
    private readonly System.Windows.Controls.Primitives.ToggleButton _resume = TransportToggle("Resume", false);
    private bool _syncingTestControls;
    private bool _allowClose;

    public SettingsWindow(AppController controller, UserSettings current, Func<UserSettings, Task> apply)
    {
        _controller = controller;
        _apply = apply;
        Title = "MotorsportTaskbar Settings";
        Width = 680;
        Height = 570;
        MinWidth = 620;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        SetResourceReference(FontFamilyProperty, "MotorsportFontFamily");

        var layout = new Grid { Margin = new Thickness(24, 20, 24, 18) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(2, 0, 2, 16) };
        heading.Children.Add(new TextBlock { Text = "Settings", FontSize = 24, FontWeight = FontWeights.SemiBold });
        var subtitle = new TextBlock { Text = "Choose live championships and tailor the taskbar display.", FontSize = 11.5, Margin = new Thickness(0, 4, 0, 0) };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        heading.Children.Add(subtitle);
        layout.Children.Add(heading);

        var tabs = new System.Windows.Controls.TabControl { Margin = new Thickness(0, 0, 0, 14) };
        tabs.Items.Add(new TabItem { Header = "Streams", Content = BuildStreamsTab() });
        tabs.Items.Add(new TabItem { Header = "Appearance", Content = BuildAppearanceTab() });
        tabs.Items.Add(new TabItem { Header = "Testing", Content = BuildTestingTab() });
        Grid.SetRow(tabs, 1);
        layout.Children.Add(tabs);

        var footer = new Grid { Margin = new Thickness(2, 0, 2, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _message.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        footer.Children.Add(_message);
        var reset = new Button { Content = "Reset defaults", MinWidth = 105, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(6, 0, 6, 0) };
        reset.Click += (_, _) => LoadSettings(new UserSettings());
        Grid.SetColumn(reset, 1);
        footer.Children.Add(reset);
        _save.Click += async (_, _) => await SaveAsync();
        Grid.SetColumn(_save, 2);
        footer.Children.Add(_save);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        Content = layout;
        LoadSettings(current);
        Closing += (_, e) => { if (!_allowClose) { e.Cancel = true; Hide(); } };
    }

    public void ClosePermanently() { _allowClose = true; Close(); }

    public void LoadSettings(UserSettings settings)
    {
        _formula1.IsChecked = settings.EnableFormula1;
        _formula2.IsChecked = settings.EnableFormula2;
        _formula3.IsChecked = settings.EnableFormula3;
        _wrc.IsChecked = settings.EnableWrc;
        _rotation.Value = settings.RotationSeconds;
        _driverCount.Value = settings.DriverCount;
        _eventHeader.IsChecked = settings.ShowEventHeader;
        _sessionProgress.IsChecked = settings.ShowSessionProgress;
        _gaps.IsChecked = settings.ShowGaps;
        _positionColors.IsChecked = settings.ShowPositionColors;
        _manufacturerLogos.IsChecked = settings.UseManufacturerLogos;
        _flagAlerts.IsChecked = settings.ShowFlagAlerts;
        UpdateSliderLabels();
        SyncTestingMode();
        _message.Text = "";
    }

    private UIElement BuildStreamsTab()
    {
        var panel = TabPanel();
        panel.Children.Add(SectionTitle("Supported live streams", "Disabled championships are not connected or rotated onto the taskbar."));
        panel.Children.Add(_formula1);
        panel.Children.Add(_formula2);
        panel.Children.Add(_formula3);
        panel.Children.Add(_wrc);
        panel.Children.Add(SectionTitle("Rotation", "When several selected championships are live, rotate between them at this interval."));
        panel.Children.Add(SliderRow("Seconds per championship", _rotation, _rotationValue));
        _rotation.ValueChanged += (_, _) => UpdateSliderLabels();
        return Scroll(panel);
    }

    private UIElement BuildAppearanceTab()
    {
        var panel = TabPanel();
        panel.Children.Add(SectionTitle("Taskbar layout", "Changes apply immediately to the live taskbar."));
        panel.Children.Add(SliderRow("Visible drivers", _driverCount, _driverCountValue));
        _driverCount.ValueChanged += (_, _) => UpdateSliderLabels();
        panel.Children.Add(_eventHeader);
        panel.Children.Add(_sessionProgress);
        panel.Children.Add(_gaps);
        panel.Children.Add(_positionColors);
        panel.Children.Add(_manufacturerLogos);
        panel.Children.Add(_flagAlerts);
        return Scroll(panel);
    }

    private UIElement BuildTestingTab()
    {
        var panel = TabPanel();
        panel.Children.Add(SectionTitle("Deterministic scenario", "Exercise taskbar states without waiting for a live session. Disabling test mode returns to the selected streams."));
        _testMode.Checked += async (_, _) => await ToggleTestModeAsync(true);
        _testMode.Unchecked += async (_, _) => await ToggleTestModeAsync(false);
        panel.Children.Add(_testMode);
        _testStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        panel.Children.Add(_testStatus);

        var transport = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
        _pause.Click += (_, _) => SetPaused(_pause.IsChecked == true);
        _resume.Click += (_, _) => SetPaused(_resume.IsChecked != true);
        transport.Children.Add(_pause);
        transport.Children.Add(_resume);
        transport.Children.Add(ActionButton("Single step", () =>
        {
            if (TryScenario(out var scenario)) scenario.Step();
        }));
        transport.Children.Add(ActionButton("Restart", () =>
        {
            if (!TryScenario(out var scenario)) return;
            scenario.Restart();
            SetTransportState(true);
        }));
        transport.Children.Add(new TextBlock { Text = " Speed ", VerticalAlignment = VerticalAlignment.Center });
        transport.Children.Add(_speed);
        _speed.ValueChanged += (_, _) =>
        {
            if (_controller.Scenario is { } scenario) scenario.Speed = _speed.Value;
        };
        _testingControls.Children.Add(transport);

        _testingControls.Children.Add(new TextBlock
        {
            Text = "Track and feed controls (click an active state again to clear it)",
            Margin = new Thickness(2, 4, 2, 4)
        });
        var commands = new WrapPanel();
        foreach (var command in Enum.GetValues<ScenarioCommand>())
        {
            var button = CommandToggle(command);
            _commandButtons[command] = button;
            commands.Children.Add(button);
        }
        _testingControls.Children.Add(commands);

        _testingControls.Children.Add(new TextBlock
        {
            Text = "Top five (CODE:GAP; use — for a missing gap)",
            Margin = new Thickness(2, 16, 2, 4)
        });
        var edit = new WrapPanel();
        edit.Children.Add(_topFive);
        edit.Children.Add(ActionButton("Apply", ApplyTopFive));
        _testingControls.Children.Add(edit);
        panel.Children.Add(_testingControls);

        panel.Children.Add(SectionTitle("Diagnostics", "Raw recording is bounded to 2 MiB and excludes connection credentials. Restart the live feed after changing it."));
        var diagnostic = new CheckBox { Content = "Record bounded raw Formula 1 feed", Margin = new Thickness(2, 4, 2, 8) };
        diagnostic.Checked += (_, _) => _controller.SetDiagnosticRecording(true);
        diagnostic.Unchecked += (_, _) => _controller.SetDiagnosticRecording(false);
        panel.Children.Add(diagnostic);

        SyncTestingMode();
        return Scroll(panel);
    }

    public void UpdateSnapshot(TimingSnapshot snapshot)
    {
        if (!_controller.TestMode) return;
        var mode = _controller.Scenario?.IsPaused == true ? "PAUSED" : "RUNNING";
        var timed = snapshot.Session.Contains("Practice", StringComparison.OrdinalIgnoreCase) || snapshot.Session.Contains("Qualifying", StringComparison.OrdinalIgnoreCase);
        var progress = timed && !string.IsNullOrWhiteSpace(snapshot.TimeRemaining)
            ? $"Time {snapshot.TimeRemaining}"
            : $"Lap {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}";
        _testStatus.Text = $"{snapshot.ConnectionState} · {snapshot.Meeting} {snapshot.Session} · {progress} · {snapshot.TrackCondition} · {mode}";
        SyncScenarioState(snapshot);
    }

    private async Task ToggleTestModeAsync(bool enabled)
    {
        if (_syncingTestControls) return;
        _testMode.IsEnabled = false;
        _testStatus.Text = enabled ? "Starting test mode…" : "Returning to live streams…";
        try
        {
            await _controller.SetTestModeAsync(enabled);
            SyncTestingMode();
        }
        catch (Exception ex)
        {
            _testStatus.Text = $"Could not switch feed: {ex.Message}";
            _syncingTestControls = true;
            _testMode.IsChecked = _controller.TestMode;
            _syncingTestControls = false;
        }
        finally { _testMode.IsEnabled = true; }
    }

    private void SyncTestingMode()
    {
        _syncingTestControls = true;
        _testMode.IsChecked = _controller.TestMode;
        _syncingTestControls = false;
        _testingControls.IsEnabled = _controller.TestMode;
        if (_controller.TestMode)
        {
            var paused = _controller.Scenario?.IsPaused != false;
            SetTransportState(paused);
            _testStatus.Text = paused ? "Test scenario ready · PAUSED" : "Test scenario ready · RUNNING";
        }
        else _testStatus.Text = "Live streams are active.";
    }

    private void SetPaused(bool paused)
    {
        if (_syncingTestControls || !TryScenario(out var scenario)) return;
        if (paused) scenario.Pause(); else scenario.Resume();
        SetTransportState(paused);
    }

    private void SetTransportState(bool paused)
    {
        _syncingTestControls = true;
        _pause.IsChecked = paused;
        _resume.IsChecked = !paused;
        _syncingTestControls = false;
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
            Content = CommandLabel(command),
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
        if (_syncingTestControls || !TryScenario(out var scenario)) return;
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
        _syncingTestControls = true;
        foreach (var command in TrackCommands) SetChecked(command, command == active);
        _syncingTestControls = false;
    }

    private void SetConnectionState(ScenarioCommand active)
    {
        _syncingTestControls = true;
        SetChecked(ScenarioCommand.Disconnect, active == ScenarioCommand.Disconnect);
        SetChecked(ScenarioCommand.Stale, active == ScenarioCommand.Stale);
        SetChecked(ScenarioCommand.Reconnect, active == ScenarioCommand.Reconnect);
        _syncingTestControls = false;
    }

    private void SetChecked(ScenarioCommand command, bool value)
    {
        if (_commandButtons.TryGetValue(command, out var button)) button.IsChecked = value;
    }

    private bool TryScenario(out ITimingScenarioSource scenario)
    {
        scenario = _controller.Scenario!;
        if (scenario is not null) return true;
        _testStatus.Text = "Enable deterministic test mode before using these controls.";
        return false;
    }

    private async Task SaveAsync()
    {
        if (_formula1.IsChecked != true && _formula2.IsChecked != true && _formula3.IsChecked != true && _wrc.IsChecked != true)
        {
            _message.Text = "Select at least one live stream.";
            return;
        }

        var next = new UserSettings
        {
            EnableFormula1 = _formula1.IsChecked == true,
            EnableFormula2 = _formula2.IsChecked == true,
            EnableFormula3 = _formula3.IsChecked == true,
            EnableWrc = _wrc.IsChecked == true,
            RotationSeconds = (int)Math.Round(_rotation.Value),
            DriverCount = (int)Math.Round(_driverCount.Value),
            ShowEventHeader = _eventHeader.IsChecked == true,
            ShowSessionProgress = _sessionProgress.IsChecked == true,
            ShowGaps = _gaps.IsChecked == true,
            ShowPositionColors = _positionColors.IsChecked == true,
            UseManufacturerLogos = _manufacturerLogos.IsChecked == true,
            ShowFlagAlerts = _flagAlerts.IsChecked == true
        }.Normalize();

        _save.IsEnabled = false;
        _message.Text = "Applying…";
        _message.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        try
        {
            await _apply(next);
            _message.Text = "Saved";
            Hide();
        }
        catch (Exception ex)
        {
            _message.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
            _message.Text = $"Could not apply settings: {ex.Message}";
        }
        finally { _save.IsEnabled = true; }
    }

    private void UpdateSliderLabels()
    {
        _driverCountValue.Text = Math.Round(_driverCount.Value).ToString();
        _rotationValue.Text = $"{Math.Round(_rotation.Value)} s";
    }

    private static StackPanel TabPanel() => new() { Margin = new Thickness(14, 12, 18, 18) };
    private static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
    };

    private static FrameworkElement SectionTitle(string title, string description)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 10) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock { Text = description, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        panel.Children.Add(detail);
        return panel;
    }

    private static CheckBox Toggle(string title, string description)
    {
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 11.5 });
        var detail = new TextBlock { Text = description, FontSize = 10, Margin = new Thickness(0, 2, 0, 0) };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        text.Children.Add(detail);
        return new CheckBox { Content = text, Margin = new Thickness(2, 6, 2, 6), VerticalContentAlignment = VerticalAlignment.Center };
    }

    private static Slider IntegerSlider(double minimum, double maximum) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        TickFrequency = 1,
        IsSnapToTickEnabled = true,
        Width = 260,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static TextBlock ValueLabel() => new()
    {
        MinWidth = 44,
        TextAlignment = TextAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        FontWeight = FontWeights.SemiBold
    };

    private static Grid SliderRow(string title, Slider slider, TextBlock value)
    {
        var row = new Grid { Margin = new Thickness(2, 8, 2, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold });
        Grid.SetColumn(slider, 1);
        row.Children.Add(slider);
        value.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(value, 2);
        row.Children.Add(value);
        return row;
    }

    private static System.Windows.Controls.Primitives.ToggleButton TransportToggle(string text, bool isChecked) => new()
    {
        Content = text,
        IsChecked = isChecked,
        Margin = new Thickness(2),
        Padding = new Thickness(8, 4, 8, 4),
        MinWidth = 70
    };

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(2),
            Padding = new Thickness(8, 4, 8, 4),
            MinWidth = 70
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static string CommandLabel(ScenarioCommand value) => value switch
    {
        ScenarioCommand.VirtualSafetyCar => "VSC",
        ScenarioCommand.VirtualSafetyCarEnding => "VSC ending",
        _ => value.ToString()
    };
}
