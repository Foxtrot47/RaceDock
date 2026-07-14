// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MotorsportTaskbar.Core;

namespace MotorsportTaskbar.App;

public sealed class DeveloperWindow : Window
{
    private readonly AppController _controller;
    private readonly TextBlock _status = new();
    private readonly System.Windows.Controls.TextBox _topFive = new() { Text = "NOR:LEAD,PIA:+0.8,LEC:+1.5,VER:+2.0,RUS:+2.8", MinWidth = 430 };
    private readonly Slider _speed = new() { Minimum = .25, Maximum = 20, Value = 4, Width = 240, TickFrequency = .25 };
    public DeveloperWindow(AppController controller)
    {
        _controller = controller; Title = "MotorsportTaskbar Developer"; Width = 720; Height = 520; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "SolidBackgroundFillColorBaseBrush"); SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush"); SetResourceReference(FontFamilyProperty, "MotorsportFontFamily");
        var root = new StackPanel { Margin = new(18) }; _status.Margin = new(0, 0, 0, 12); root.Children.Add(_status);
        root.Children.Add(new TextBlock { Text = "Deterministic scenario", FontWeight = FontWeights.Bold, FontSize = 16 });
        var transport = new WrapPanel { Margin = new(0, 8, 0, 8) };
        transport.Children.Add(Button("Pause", () => _controller.Scenario?.Pause())); transport.Children.Add(Button("Resume", () => _controller.Scenario?.Resume())); transport.Children.Add(Button("Single step", () => _controller.Scenario?.Step())); transport.Children.Add(Button("Restart", () => _controller.Scenario?.Restart()));
        transport.Children.Add(new TextBlock { Text = " Speed ", VerticalAlignment = VerticalAlignment.Center }); transport.Children.Add(_speed); root.Children.Add(transport);
        _speed.ValueChanged += (_, _) => { if (_controller.Scenario is { } s) s.Speed = _speed.Value; };
        var alerts = new WrapPanel(); foreach (var command in Enum.GetValues<ScenarioCommand>()) alerts.Children.Add(Button(Label(command), () => _controller.Scenario?.Trigger(command))); root.Children.Add(alerts);
        root.Children.Add(new TextBlock { Text = "Top five (CODE:GAP; missing gap is shown as —)", Margin = new(0, 16, 0, 4) });
        var edit = new WrapPanel(); edit.Children.Add(_topFive); edit.Children.Add(Button("Apply", ApplyTopFive)); root.Children.Add(edit);
        var diagnostic = new CheckBox { Content = "Record bounded raw live feed (max 2 MiB, no credentials)", Margin = new(0, 18, 0, 0) };
        diagnostic.Checked += (_, _) => _controller.SetDiagnosticRecording(true); diagnostic.Unchecked += (_, _) => _controller.SetDiagnosticRecording(false); root.Children.Add(diagnostic);
        var note = new TextBlock { Text = "Enable test mode from the tray before using scenario controls.", Margin = new(0, 12, 0, 0) }; note.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush"); root.Children.Add(note); Content = root;
        Closing += (_, e) => { e.Cancel = true; Hide(); };
    }
    public void UpdateSnapshot(TimingSnapshot snapshot) => _status.Text = $"{snapshot.ConnectionState} · {snapshot.Meeting} {snapshot.Session} · Lap {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"} · {snapshot.TrackCondition}";
    private void ApplyTopFive()
    {
        var values = _topFive.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Take(5).Select(value => { var p = value.Split(':', 2); return new ManualStanding(p[0], p.Length == 2 && p[1] != "—" ? p[1] : null); }).ToList();
        _controller.Scenario?.SetTopFive(values);
    }
    private static System.Windows.Controls.Button Button(string text, Action action) { var b = new System.Windows.Controls.Button { Content = text, Margin = new(2), Padding = new(8, 4, 8, 4), MinWidth = 70 }; b.Click += (_, _) => action(); return b; }
    private static string Label(ScenarioCommand value) => value switch { ScenarioCommand.VirtualSafetyCar => "VSC", ScenarioCommand.VirtualSafetyCarEnding => "VSC ending", _ => value.ToString() };
}
