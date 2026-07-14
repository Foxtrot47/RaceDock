// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MotorsportTaskbar.Core;

namespace MotorsportTaskbar.App;

public sealed class TaskbarWindow : Window
{
    private static readonly (Color Start, Color End)[] CardColors =
    [
        (Color.FromRgb(145, 62, 7), Color.FromRgb(211, 119, 26)),
        (Color.FromRgb(128, 16, 54), Color.FromRgb(202, 42, 98)),
        (Color.FromRgb(14, 73, 107), Color.FromRgb(32, 131, 185)),
        (Color.FromRgb(74, 20, 115), Color.FromRgb(130, 48, 198)),
        (Color.FromRgb(16, 84, 55), Color.FromRgb(36, 148, 101))
    ];

    private readonly Grid _root = new();
    private readonly UniformGrid _strip = new() { Rows = 1, Columns = 5, Margin = new(2) };
    private readonly StackPanel _raceContext = new() { Margin = new(6, 2, 4, 2), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _raceText = new() { FontSize = 10.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _lapText = new() { FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Border _alert = new()
    {
        Visibility = Visibility.Collapsed,
        CornerRadius = new(8),
        BorderThickness = new(1),
        Padding = new(12, 2, 12, 2),
        Margin = new(1),
        SnapsToDevicePixels = true
    };
    private readonly TextBlock _alertText = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = 12.5,
        FontWeight = FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Foreground = Brushes.White
    };
    private readonly NativeTaskbar _host;
    private readonly ClassificationFlyout _flyout = new();
    private readonly DispatcherTimer _reattach;
    private TimingSnapshot? _snapshot;

    public TaskbarWindow()
    {
        Width = 520; Height = 40; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        UseLayoutRounding = true; SnapsToDevicePixels = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        SetResourceReference(FontFamilyProperty, "MotorsportFontFamily");
        _root.Background = Brushes.Transparent;
        _alert.BorderBrush = new SolidColorBrush(Color.FromArgb(72, 255, 255, 255));
        _alert.Child = _alertText;
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _raceText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _lapText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        _raceContext.Children.Add(_raceText); _raceContext.Children.Add(_lapText);
        Grid.SetColumn(_raceContext, 0); Grid.SetColumn(_strip, 1);
        Grid.SetColumn(_alert, 0); Grid.SetColumnSpan(_alert, 2);
        _root.Children.Add(_raceContext); _root.Children.Add(_strip); _root.Children.Add(_alert); Content = _root;
        _host = new(this); SourceInitialized += (_, _) => _host.Attach();
        _host.PositionChanged += visible => { if (!visible) _flyout.Hide(); else if (_flyout.IsVisible) _host.PositionFlyout(_flyout); };
        MouseEnter += (_, _) => ShowFlyout(); MouseLeave += (_, _) => _flyout.ScheduleClose();
        _flyout.PointerEntered += (_, _) => _flyout.CancelClose();
        _reattach = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => _host.Reattach(), Dispatcher); _reattach.Start();
    }

    public void UpdateSnapshot(TimingSnapshot snapshot)
    {
        _snapshot = snapshot;
        var fullMeeting = string.IsNullOrWhiteSpace(snapshot.Meeting)
            ? (string.IsNullOrWhiteSpace(snapshot.Circuit) ? "F1" : snapshot.Circuit)
            : snapshot.Meeting;
        _raceText.Text = ShortenRaceName(fullMeeting);
        _raceText.ToolTip = fullMeeting;
        _lapText.Text = snapshot.CurrentLap > 0
            ? $"LAP {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}"
            : "LAP —";
        _strip.Children.Clear();
        foreach (var standing in snapshot.Competitors.Take(5)) _strip.Children.Add(CreateCell(standing));
        while (_strip.Children.Count < 5) _strip.Children.Add(CreateEmptyCell(_strip.Children.Count + 1));
        _flyout.UpdateSnapshot(snapshot);
    }

    private static string ShortenRaceName(string meeting) =>
        meeting.Replace("Grand Prix", "GP", StringComparison.OrdinalIgnoreCase);

    public void SetAlert(RaceAlert? alert)
    {
        _alert.Visibility = alert is null ? Visibility.Collapsed : Visibility.Visible;
        _strip.Visibility = alert is null ? Visibility.Visible : Visibility.Collapsed;
        _raceContext.Visibility = alert is null ? Visibility.Visible : Visibility.Collapsed;
        if (alert is null) return;
        _alertText.Text = string.IsNullOrWhiteSpace(alert.Detail) ? alert.Title : $"{alert.Title}  ·  {alert.Detail}";
        _alert.Background = new SolidColorBrush(alert.Kind switch
        {
            AlertKind.RedFlag => Color.FromRgb(166, 24, 32),
            AlertKind.Yellow or AlertKind.DoubleYellow => Color.FromRgb(180, 145, 0),
            AlertKind.SafetyCar or AlertKind.VirtualSafetyCar or AlertKind.VirtualSafetyCarEnding => Color.FromRgb(182, 105, 0),
            AlertKind.FastestLap => Color.FromRgb(112, 47, 154),
            _ => Color.FromRgb(62, 62, 68)
        });
    }

    private static Border CreateCell(CompetitorStanding standing)
    {
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(30) });
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        layout.Children.Add(CreatePositionCard(standing.Position, standing.Position.ToString()));

        var details = new StackPanel { Margin = new(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var driver = new TextBlock
        {
            Text = standing.Code,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        driver.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        var gap = new TextBlock
        {
            Text = standing.Position == 1 ? "LEADER" : standing.GapToLeader ?? "—",
            FontSize = 11,
            MinHeight = 11,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        gap.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        details.Children.Add(driver); details.Children.Add(gap);
        Grid.SetColumn(details, 1); layout.Children.Add(details);
        return CreateCellContainer(layout);
    }

    private static Border CreateEmptyCell(int position)
    {
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(30) });
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        layout.Children.Add(CreatePositionCard(position, "—", .45));
        return CreateCellContainer(layout);
    }

    private static Border CreateCellContainer(UIElement child) => new()
    {
        Child = child,
        Background = Brushes.Transparent,
        Margin = new(2, 2, 2, 2),
        SnapsToDevicePixels = true
    };

    private static Border CreatePositionCard(int position, string text, double opacity = 1)
    {
        var colors = CardColors[Math.Clamp(position, 1, CardColors.Length) - 1];
        var label = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White
        };
        return new Border
        {
            Width = 26,
            Height = 28,
            Margin = new(1),
            Padding = new(1),
            CornerRadius = new(7),
            Opacity = opacity,
            Child = label,
            SnapsToDevicePixels = true,
            Background = new LinearGradientBrush(colors.Start, colors.End, 35),
            Effect = new DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 1,
                Direction = 270,
                Opacity = .28,
                Color = Colors.Black
            }
        };
    }

    private void ShowFlyout()
    {
        if (_snapshot is null) return; _flyout.CancelClose(); _flyout.Show(); _flyout.UpdateLayout(); _host.PositionFlyout(_flyout);
    }
    protected override void OnClosed(EventArgs e) { _reattach.Stop(); _flyout.Close(); base.OnClosed(e); }
}
