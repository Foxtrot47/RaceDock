// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using RaceDock.Core;
using Brush = System.Windows.Media.Brush;
using Control = System.Windows.Controls.Control;

namespace RaceDock.App;

/// <summary>Which optional values a classification row shows for the current championship.</summary>
internal readonly record struct ClassificationRowFormat(bool Rally, bool Endurance, LapTimeMode LapTimeMode)
{
    /// <summary>True when the last column carries a class instead of a lap number.</summary>
    public bool CategoryInsteadOfLap => Rally || Endurance;

    /// <summary>
    /// The lap time this session is judged on. Rally stages report their stage time, which arrives as the
    /// result time regardless of session name.
    /// </summary>
    public string? LapTime(CompetitorStanding standing) =>
        Rally || LapTimeMode == LapTimeMode.BestLap ? standing.ResultTime : standing.LastLapTime ?? standing.ResultTime;
}

/// <summary>
/// One reusable classification row. The element structure is fixed at construction and later updates only
/// assign values, so a redraw costs no visual-tree churn, no layout invalidation beyond the changed text,
/// and no UI Automation subtree rebuild.
/// </summary>
internal sealed class ClassificationRow
{
    private static readonly (Color Start, Color End)[] PositionColors =
    [
        (Color.FromRgb(145, 62, 7), Color.FromRgb(211, 119, 26)),
        (Color.FromRgb(128, 16, 54), Color.FromRgb(202, 42, 98)),
        (Color.FromRgb(14, 73, 107), Color.FromRgb(32, 131, 185)),
        (Color.FromRgb(74, 20, 115), Color.FromRgb(130, 48, 198)),
        (Color.FromRgb(16, 84, 55), Color.FromRgb(36, 148, 101))
    ];

    private static readonly Brush NeutralPositionBrush = FrozenGradient(Color.FromRgb(52, 55, 63), Color.FromRgb(83, 88, 100));
    private static readonly Brush[] ColoredPositionBrushes =
        [.. PositionColors.Select(colors => FrozenGradient(colors.Start, colors.End))];

    private static readonly SolidColorBrush FastestBrush = Frozen(Color.FromRgb(196, 87, 255));
    private static readonly SolidColorBrush TyreDarkText = Frozen(Color.FromRgb(35, 35, 35));

    private readonly Grid _grid;
    private readonly Border _container;
    private readonly Border _positionCard;
    private readonly TextBlock _positionText;
    private readonly Grid _driver;
    private readonly Border _logoHost;
    private readonly Grid _driverCopy;
    private readonly ScrollingTextBlock _driverName;
    private IReadOnlyList<FlyoutColumn>? _columnOrder;
    private readonly TextBlock _fastest;
    private readonly ScrollingTextBlock _team;
    private readonly ScrollingTextBlock _gap;
    private readonly ScrollingTextBlock _interval;
    private readonly ScrollingTextBlock _result;
    private readonly SectorTimingCell _sectors;
    private readonly RallySplitTimingCell _splits;
    private readonly Border _tyreBadge;
    private readonly TextBlock _tyreText;
    private readonly ScrollingTextBlock _category;
    private readonly Border _statusBadge;
    private readonly TextBlock _statusText;

    private string _logoKey = "";

    public ClassificationRow(ScrollingTextClock clock)
    {
        _grid = new Grid { MinHeight = 38 };
        for (var index = 0; index < 10; index++) _grid.ColumnDefinitions.Add(new ColumnDefinition());
        _container = new Border { Child = _grid, CornerRadius = new CornerRadius(6) };
        SurfaceMotion.AttachHover(_container, highlightTopEdge: false);

        _positionText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _positionCard = new Border
        {
            Width = 32,
            Height = 32,
            Margin = new Thickness(5),
            CornerRadius = new CornerRadius(8),
            Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Direction = 270, Opacity = 0.28 },
            Child = _positionText
        };
        _grid.Children.Add(_positionCard);

        _driver = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) };
        _driver.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _driver.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _logoHost = new Border
        {
            Width = 26,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _driver.Children.Add(_logoHost);

        _driverCopy = new Grid { VerticalAlignment = VerticalAlignment.Center };
        _driverCopy.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _driverCopy.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var driverLine = new Grid();
        driverLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        driverLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _driverName = new ScrollingTextBlock { FontSize = 11.5, FontWeight = FontWeights.SemiBold, Clock = clock };
        driverLine.Children.Add(_driverName);
        _fastest = new TextBlock
        {
            Text = "●",
            Foreground = FastestBrush,
            FontSize = 9,
            Margin = new Thickness(5, 1, 0, 0),
            ToolTip = "Overall fastest lap",
            Visibility = Visibility.Collapsed
        };
        Grid.SetColumn(_fastest, 1);
        driverLine.Children.Add(_fastest);
        _team = new ScrollingTextBlock { FontSize = 9.5, Clock = clock };
        _team.SetResourceReference(Control.ForegroundProperty, "TextFillColorTertiaryBrush");
        _driverCopy.Children.Add(driverLine);
        Grid.SetRow(_team, 1);
        _driverCopy.Children.Add(_team);
        Grid.SetColumn(_driverCopy, 1);
        _driver.Children.Add(_driverCopy);
        Grid.SetColumn(_driver, 1);
        _grid.Children.Add(_driver);

        // Columns are assigned by ApplyColumnOrder; these indices are only the starting arrangement.
        _gap = Value(clock, 2);
        _interval = Value(clock, 3);
        _result = Value(clock, 4);
        _sectors = new SectorTimingCell();
        Grid.SetColumn(_sectors.Element, 5);
        _splits = new RallySplitTimingCell();
        Grid.SetColumn(_splits.Element, 6);
        _category = Value(clock, 8, TextAlignment.Center);
        foreach (var value in new[] { _gap, _interval, _result, _category }) _grid.Children.Add(value);
        _grid.Children.Add(_sectors.Element);
        _grid.Children.Add(_splits.Element);

        _tyreText = new TextBlock { Foreground = Brushes.White, FontSize = 9.5, FontWeight = FontWeights.SemiBold };
        _tyreBadge = new Border
        {
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(7, 3, 7, 3),
            Margin = new Thickness(4, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _tyreText
        };
        Grid.SetColumn(_tyreBadge, 7);
        _grid.Children.Add(_tyreBadge);

        _statusText = new TextBlock { Foreground = Brushes.White, FontSize = 9.5, FontWeight = FontWeights.SemiBold };
        _statusBadge = new Border
        {
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(7, 3, 7, 3),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _statusText
        };
        Grid.SetColumn(_statusBadge, 9);
        _grid.Children.Add(_statusBadge);
    }

    public UIElement Element => _container;

    /// <summary>
    /// Places the movable cells in the configured order. The order only changes when the user rearranges
    /// the columns, so the common redraw path exits on the reference check without touching layout.
    /// </summary>
    private void ApplyColumnOrder(IReadOnlyList<FlyoutColumn> order)
    {
        if (ReferenceEquals(order, _columnOrder)) return;
        _columnOrder = order;
        for (var index = 0; index < order.Count; index++)
        {
            // Position and driver occupy the first two columns, so movable cells start at index 2.
            var column = index + 2;
            switch (order[index])
            {
                case FlyoutColumn.Gap: Grid.SetColumn(_gap, column); break;
                case FlyoutColumn.Interval: Grid.SetColumn(_interval, column); break;
                case FlyoutColumn.LapTime: Grid.SetColumn(_result, column); break;
                case FlyoutColumn.Sectors: Grid.SetColumn(_sectors.Element, column); break;
                case FlyoutColumn.Splits: Grid.SetColumn(_splits.Element, column); break;
                case FlyoutColumn.Tyre: Grid.SetColumn(_tyreBadge, column); break;
                case FlyoutColumn.Category: Grid.SetColumn(_category, column); break;
                case FlyoutColumn.Status: Grid.SetColumn(_statusBadge, column); break;
            }
        }
    }

    /// <summary>Applies the column geometry and which optional cells participate at all.</summary>
    public void ApplyDisplay(ChampionshipDisplaySettings display, IReadOnlyList<GridLength> columnWidths,
        IReadOnlyList<RallySplitPoint> splitPoints)
    {
        _splits.Configure(splitPoints);
        for (var index = 0; index < columnWidths.Count; index++) _grid.ColumnDefinitions[index].Width = columnWidths[index];
        ApplyColumnOrder(display.FlyoutColumnOrder);
        _gap.Visibility = display.FlyoutShowGaps ? Visibility.Visible : Visibility.Collapsed;
        _interval.Visibility = display.FlyoutShowIntervals ? Visibility.Visible : Visibility.Collapsed;
        _result.Visibility = display.FlyoutShowLapTime ? Visibility.Visible : Visibility.Collapsed;
        _sectors.Element.Visibility = display.FlyoutShowSectorTimes ? Visibility.Visible : Visibility.Collapsed;
        _splits.Element.Visibility = display.FlyoutShowSplitTimes ? Visibility.Visible : Visibility.Collapsed;
        _tyreBadge.Visibility = display.FlyoutShowTyre && _tyreText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _category.Visibility = display.FlyoutShowCategoryOrLap ? Visibility.Visible : Visibility.Collapsed;
        _statusBadge.Visibility = display.FlyoutShowStatus ? Visibility.Visible : Visibility.Collapsed;
        _team.Visibility = display.FlyoutShowTeamNames ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Update(CompetitorStanding standing, Championship championship, ChampionshipDisplaySettings display,
        ClassificationRowFormat format)
    {
        _positionText.Text = standing.PositionLabel ?? standing.Position.ToString();
        _positionCard.Background = display.FlyoutShowPositionColors && standing.Position is >= 1 and <= 5
            ? ColoredPositionBrushes[standing.Position - 1]
            : NeutralPositionBrush;

        var name = string.IsNullOrWhiteSpace(standing.Name) ? standing.Code : standing.Name;
        _driverName.Text = name;
        _driverName.ToolTip = name;
        _driver.ToolTip = string.IsNullOrWhiteSpace(standing.Name) ? null : standing.Name;
        _driverName.SetResourceReference(Control.ForegroundProperty,
            standing.Retired ? "TextFillColorTertiaryBrush" : "TextFillColorPrimaryBrush");
        _fastest.Visibility = standing.IsOverallFastest ? Visibility.Visible : Visibility.Collapsed;
        _team.Text = standing.Team;
        _team.ToolTip = standing.Team;

        UpdateLogo(standing, championship, display);

        if (display.FlyoutShowGaps)
        {
            SetValue(_gap, standing.Position == 1 ? "LEADER" : standing.GapToLeader ?? "—");
            _gap.FontWeight = standing.Position == 1 ? FontWeights.SemiBold : FontWeights.Normal;
        }
        if (display.FlyoutShowIntervals)
            SetValue(_interval, standing.Position == 1 ? "LEADER" : standing.IntervalToPositionAhead ?? "—");

        if (display.FlyoutShowLapTime) SetValue(_result, format.LapTime(standing) ?? "—");
        if (display.FlyoutShowSectorTimes)
        {
            var bestLap = display.FlyoutSectorTimeMode == SectorTimeMode.BestLap;
            _sectors.Update(bestLap ? standing.BestLapSectors : standing.CurrentSectors,
                bestLap ? null : standing.CurrentMiniSectors);
        }
        if (display.FlyoutShowSplitTimes) _splits.Update(standing.StageSplits);
        if (display.FlyoutShowTyre) UpdateTyre(standing.Tyre);

        if (display.FlyoutShowCategoryOrLap)
            SetValue(_category, format.CategoryInsteadOfLap
                ? ChampionshipDisplay.CompactCategory(championship, standing.Category)
                : standing.Lap.ToString());

        if (display.FlyoutShowStatus) UpdateStatus(standing);

        _grid.Opacity = standing.Retired || standing.Stopped ? 0.62 : 1;
    }

    private void UpdateLogo(CompetitorStanding standing, Championship championship, ChampionshipDisplaySettings display)
    {
        // Rebuilding the logo element is only worth it when the competitor's branding actually changed.
        var key = display.FlyoutUseManufacturerLogos ? $"{championship}|{standing.Manufacturer}|{standing.Team}" : "";
        if (key == _logoKey) return;
        _logoKey = key;

        if (key.Length > 0 && ManufacturerLogo.TryCreateForStanding(standing, championship, 26, out var logo))
        {
            _logoHost.Child = logo;
            _logoHost.Visibility = Visibility.Visible;
            Grid.SetColumn(_driverCopy, 1);
            Grid.SetColumnSpan(_driverCopy, 1);
        }
        else
        {
            _logoHost.Child = null;
            // Keep the logo slot when branding is enabled so entries without artwork align with
            // the rest of the field. Collapse it only when the whole logo column is disabled.
            _logoHost.Visibility = key.Length > 0 ? Visibility.Hidden : Visibility.Collapsed;
            Grid.SetColumn(_driverCopy, key.Length > 0 ? 1 : 0);
            Grid.SetColumnSpan(_driverCopy, key.Length > 0 ? 1 : 2);
        }
    }

    private void UpdateTyre(string? tyre)
    {
        var normalized = tyre?.Trim().ToUpperInvariant();
        // A feed that sends no compound, or sends its own "UNKNOWN", has nothing to put in a badge.
        if (string.IsNullOrEmpty(normalized) || normalized == "UNKNOWN")
        {
            _tyreText.Text = "";
            _tyreBadge.Visibility = Visibility.Collapsed;
            return;
        }
        var (background, foreground) = normalized switch
        {
            "SOFT" or "S" or "O" => (TyreBrushes.Soft, Brushes.White),
            "MEDIUM" or "M" or "P" => (TyreBrushes.Medium, TyreDarkText),
            "HARD" or "H" => (TyreBrushes.Hard, TyreDarkText),
            "INTERMEDIATE" or "I" => (TyreBrushes.Intermediate, Brushes.White),
            "WET" or "W" => (TyreBrushes.Wet, Brushes.White),
            _ => (TyreBrushes.Unknown, (SolidColorBrush)Brushes.White)
        };
        _tyreText.Text = normalized;
        _tyreText.Foreground = foreground;
        _tyreBadge.Background = background;
        _tyreBadge.Visibility = Visibility.Visible;
    }

    private void UpdateStatus(CompetitorStanding standing)
    {
        var (text, background) = standing switch
        {
            { Retired: true } => ("RET", StatusBrushes.Critical),
            { Stopped: true } => ("STOP", StatusBrushes.Critical),
            { InPit: true } => ("PIT", StatusBrushes.Information),
            { StatusLabel: "RUN" } => ("RUN", StatusBrushes.Information),
            { StatusLabel: "OUT" } => ("OUT", StatusBrushes.Information),
            { StatusLabel: "FIN" } => ("FIN", StatusBrushes.Success),
            { StatusLabel: "N/S" } => ("N/S", StatusBrushes.Neutral),
            { StatusLabel: "N/C" } => ("N/C", StatusBrushes.Neutral),
            { StatusLabel: "DSQ" } => ("DSQ", StatusBrushes.Critical),
            { StatusLabel: "DUE" } => ("DUE", StatusBrushes.Neutral),
            _ => ("", StatusBrushes.Transparent)
        };
        _statusText.Text = text;
        _statusBadge.Background = background;
    }

    private static void SetValue(ScrollingTextBlock target, string text)
    {
        target.Text = text;
        target.ToolTip = text;
    }

    private static ScrollingTextBlock Value(ScrollingTextClock clock, int column, TextAlignment alignment = TextAlignment.Left)
    {
        var value = new ScrollingTextBlock
        {
            FontSize = 11,
            TextAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            Clock = clock,
            Margin = new Thickness(4, 0, 8, 0)
        };
        value.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        Grid.SetColumn(value, column);
        return value;
    }

    private static LinearGradientBrush FrozenGradient(Color start, Color end)
    {
        var brush = new LinearGradientBrush(start, end, 35);
        brush.Freeze();
        return brush;
    }

    internal static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    internal static SolidColorBrush Frozen(byte red, byte green, byte blue, double opacity) =>
        Frozen(Color.FromArgb((byte)Math.Round(opacity * 255), red, green, blue));

    private static class TyreBrushes
    {
        public static readonly SolidColorBrush Soft = Frozen(205, 45, 62, 0.85);
        public static readonly SolidColorBrush Medium = Frozen(226, 182, 36, 0.9);
        public static readonly SolidColorBrush Hard = Frozen(224, 226, 232, 0.9);
        public static readonly SolidColorBrush Intermediate = Frozen(40, 156, 87, 0.85);
        public static readonly SolidColorBrush Wet = Frozen(35, 115, 196, 0.85);
        public static readonly SolidColorBrush Unknown = Frozen(92, 96, 106, 0.3);
    }

    private static class StatusBrushes
    {
        public static readonly SolidColorBrush Critical = Frozen(199, 45, 70, 0.35);
        public static readonly SolidColorBrush Information = Frozen(33, 132, 188, 0.3);
        public static readonly SolidColorBrush Success = Frozen(30, 138, 88, 0.28);
        public static readonly SolidColorBrush Neutral = Frozen(92, 96, 106, 0.3);
        public static readonly SolidColorBrush Transparent = Frozen(Colors.Transparent);
    }
}
