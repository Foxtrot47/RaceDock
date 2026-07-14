// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MotorsportTaskbar.Core;

namespace MotorsportTaskbar.App;

public sealed class TaskbarWindow : Window
{
    private readonly Grid _root = new();
    private readonly UniformGrid _strip = new() { Rows = 1, Columns = 5 };
    private readonly Border _alert = new() { Visibility = Visibility.Collapsed, CornerRadius = new(6), Padding = new(10, 2, 10, 2) };
    private readonly TextBlock _alertText = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly NativeTaskbar _host;
    private readonly ClassificationFlyout _flyout = new();
    private readonly DispatcherTimer _reattach;
    private TimingSnapshot? _snapshot;

    public TaskbarWindow()
    {
        Width = 420; Height = 40; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        _root.Background = new SolidColorBrush(Color.FromArgb(245, 28, 28, 32)); _root.Children.Add(_strip); _alert.Child = _alertText; _root.Children.Add(_alert); Content = _root;
        _host = new(this); SourceInitialized += (_, _) => _host.Attach();
        _host.PositionChanged += visible => { if (!visible) _flyout.Hide(); else if (_flyout.IsVisible) _host.PositionFlyout(_flyout); };
        MouseEnter += (_, _) => ShowFlyout(); MouseLeave += (_, _) => _flyout.ScheduleClose();
        _flyout.PointerEntered += (_, _) => _flyout.CancelClose();
        _reattach = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => _host.Reattach(), Dispatcher); _reattach.Start();
    }

    public void UpdateSnapshot(TimingSnapshot snapshot)
    {
        _snapshot = snapshot; _strip.Children.Clear();
        foreach (var standing in snapshot.Competitors.Take(5)) _strip.Children.Add(CreateCell(standing));
        while (_strip.Children.Count < 5) _strip.Children.Add(CreateEmptyCell());
        _flyout.UpdateSnapshot(snapshot);
    }

    public void SetAlert(RaceAlert? alert)
    {
        _alert.Visibility = alert is null ? Visibility.Collapsed : Visibility.Visible; _strip.Visibility = alert is null ? Visibility.Visible : Visibility.Collapsed;
        if (alert is null) return;
        _alertText.Text = string.IsNullOrWhiteSpace(alert.Detail) ? alert.Title : $"{alert.Title}   {alert.Detail}";
        _alert.Background = new SolidColorBrush(alert.Kind switch { AlertKind.RedFlag => Color.FromRgb(166, 24, 32), AlertKind.Yellow or AlertKind.DoubleYellow => Color.FromRgb(180, 145, 0), AlertKind.SafetyCar or AlertKind.VirtualSafetyCar or AlertKind.VirtualSafetyCarEnding => Color.FromRgb(182, 105, 0), AlertKind.FastestLap => Color.FromRgb(112, 47, 154), _ => Color.FromRgb(62, 62, 68) });
    }

    private static Border CreateCell(CompetitorStanding s)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = $"{s.Position}  {s.Code}", Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = s.Position == 1 ? "LEAD" : s.GapToLeader ?? "—", Foreground = Brushes.LightGray, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center });
        return new Border { Child = panel, BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 66)), BorderThickness = new(0, 0, 1, 0) };
    }
    private static Border CreateEmptyCell() => new() { Child = new TextBlock { Text = "—", Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
    private void ShowFlyout()
    {
        if (_snapshot is null) return; _flyout.CancelClose(); _flyout.Show(); _flyout.UpdateLayout(); _host.PositionFlyout(_flyout);
    }
    protected override void OnClosed(EventArgs e) { _reattach.Stop(); _flyout.Close(); base.OnClosed(e); }
}
