// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
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
    private readonly Grid _eventHeader = new();
    private readonly Border _alert = new()
    {
        Visibility = Visibility.Collapsed,
        Width = 18,
        Height = 18,
        Margin = new(4, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
        SnapsToDevicePixels = true
    };
    private readonly Canvas _flagCanvas = new() { Width = 18, Height = 18 };
    private readonly System.Windows.Shapes.Rectangle _flagPole = new() { Width = 1.5, Height = 15, Fill = Brushes.Black };
    private readonly Polygon _flagShape = new()
    {
        Points = new PointCollection { new(3, 2), new(16, 3), new(14, 10), new(3, 9) },
        Fill = Brushes.Gold
    };
    private readonly Polygon _secondFlagShape = new()
    {
        Visibility = Visibility.Collapsed,
        Points = new PointCollection { new(3, 8), new(16, 9), new(14, 16), new(3, 15) },
        Fill = Brushes.Gold
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
        _flagCanvas.Children.Add(_flagPole); Canvas.SetLeft(_flagPole, 1); Canvas.SetTop(_flagPole, 1);
        _flagCanvas.Children.Add(_flagShape); _flagCanvas.Children.Add(_secondFlagShape);
        _alert.Child = _flagCanvas;
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 72, MaxWidth = 150 });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _eventHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _eventHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_raceText, 0); Grid.SetColumn(_alert, 1);
        _eventHeader.Children.Add(_raceText); _eventHeader.Children.Add(_alert);
        _raceContext.Children.Add(_eventHeader); _raceContext.Children.Add(_lapText);
        Grid.SetColumn(_raceContext, 0); Grid.SetColumn(_strip, 1);
        _root.Children.Add(_raceContext); _root.Children.Add(_strip); Content = _root;
        _host = new(this); SourceInitialized += (_, _) => _host.Attach();
        _host.PositionChanged += visible => { if (!visible) _flyout.Hide(); else if (_flyout.IsVisible) _host.PositionFlyout(_flyout); };
        MouseLeftButtonUp += (_, _) => ShowFlyout(); MouseLeave += (_, _) => _flyout.ScheduleClose();
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
        _lapText.Text = FormatSessionProgress(snapshot);
        _strip.Children.Clear();
        foreach (var standing in snapshot.Competitors.Take(5)) _strip.Children.Add(CreateCell(standing));
        while (_strip.Children.Count < 5) _strip.Children.Add(CreateEmptyCell(_strip.Children.Count + 1));
        _flyout.UpdateSnapshot(snapshot);
    }

    private static bool IsRallySnapshot(TimingSnapshot snapshot) => snapshot.Meeting.Contains("Rally", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SS", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SHAKEDOWN", StringComparison.OrdinalIgnoreCase);
    private static string FormatSessionProgress(TimingSnapshot snapshot)
    {
        if (IsRallySnapshot(snapshot)) return snapshot.Session;
        var session = ShortSessionName(snapshot.Session);
        if (IsTimedSession(snapshot.Session) && !string.IsNullOrWhiteSpace(snapshot.TimeRemaining))
            return $"{session} {CompactTime(snapshot.TimeRemaining)}";
        return snapshot.CurrentLap > 0
            ? $"{session} {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}"
            : session;
    }

    private static string ShortSessionName(string session) => session switch
    {
        "Free Practice" or "Free Practice 1" or "Practice" or "Practice 1" => "FP1",
        "Free Practice 2" or "Practice 2" => "FP2",
        "Free Practice 3" or "Practice 3" => "FP3",
        "Sprint Qualifying" or "Sprint Qualifying 1" or "Sprint Shootout" or "Sprint Shootout 1" => "SQ1",
        "Sprint Qualifying 2" or "Sprint Shootout 2" => "SQ2",
        "Sprint Qualifying 3" or "Sprint Shootout 3" => "SQ3",
        "Sprint" or "Sprint Race" => "SPR",
        "Feature Race" => "FEA",
        "Qualifying" or "Qualifying 1" => "Q1",
        "Qualifying 2" => "Q2",
        "Qualifying 3" => "Q3",
        "Race" => "RAC",
        _ => session.ToUpperInvariant()
    };

    private static bool IsTimedSession(string session) =>
        session.Contains("Practice", StringComparison.OrdinalIgnoreCase) ||
        session.Contains("Qualifying", StringComparison.OrdinalIgnoreCase);

    private static string CompactTime(string value) =>
        TimeSpan.TryParse(value, out var remaining)
            ? remaining.ToString(remaining.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss")
            : value;

    private static string ShortenRaceName(string meeting) =>
        meeting.Replace("Grand Prix", "GP", StringComparison.OrdinalIgnoreCase);

    public void SetAlert(RaceAlert? alert)
    {
        var show = alert is not null && _snapshot is not null && !IsRallySnapshot(_snapshot) && IsFiaFlag(alert.Kind);
        _alert.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show || alert is null) return;
        _secondFlagShape.Visibility = alert.Kind == AlertKind.DoubleYellow ? Visibility.Visible : Visibility.Collapsed;
        _flagShape.Fill = FlagBrush(alert.Kind);
        _secondFlagShape.Fill = _flagShape.Fill;
        _alert.ToolTip = string.IsNullOrWhiteSpace(alert.Detail) ? alert.Title : $"{alert.Title}  ·  {alert.Detail}";
        System.Windows.Automation.AutomationProperties.SetName(_alert, alert.Title);
    }

    private static bool IsFiaFlag(AlertKind kind) => kind is AlertKind.RedFlag or AlertKind.Yellow or AlertKind.DoubleYellow or AlertKind.Chequered;

    private static System.Windows.Media.Brush FlagBrush(AlertKind kind) => kind switch
    {
        AlertKind.RedFlag => Brushes.Red,
        AlertKind.Chequered => new DrawingBrush(new DrawingGroup
        {
            Children = new DrawingCollection
            {
                new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 8, 8))),
                new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(8, 0, 8, 8))),
                new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(0, 8, 8, 8))),
                new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(8, 8, 8, 8)))
            }
        }) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 8, 8), ViewportUnits = BrushMappingMode.Absolute },
        _ => Brushes.Gold
    };

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
