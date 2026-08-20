// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using RaceDock.Core;

namespace RaceDock.App;

public sealed class ClassificationFlyout : Window
{
    private static readonly (Color Start, Color End)[] PositionColors =
    [
        (Color.FromRgb(145, 62, 7), Color.FromRgb(211, 119, 26)),
        (Color.FromRgb(128, 16, 54), Color.FromRgb(202, 42, 98)),
        (Color.FromRgb(14, 73, 107), Color.FromRgb(32, 131, 185)),
        (Color.FromRgb(74, 20, 115), Color.FromRgb(130, 48, 198)),
        (Color.FromRgb(16, 84, 55), Color.FromRgb(36, 148, 101))
    ];

    private readonly StackPanel _rows = new();
    private readonly ScrollingTextClock _scrollClock = new();
    private readonly ScrollingTextBlock _title = new();
    private readonly ScrollingTextBlock _sessionDetails = new();
    private readonly TextBlock _connectionText = new();
    private readonly Border _connectionBadge = new();
    private readonly TextBlock _trackText = new();
    private readonly Border _trackBadge = new();
    private Grid _sessionHeader = null!;
    private StackPanel _badges = null!;
    private Grid _columnHeader = null!;
    private ScrollingTextBlock _gapHeader = null!;
    private ScrollingTextBlock _intervalHeader = null!;
    private ScrollingTextBlock _lapTimeHeader = null!;
    private ScrollingTextBlock _tyreHeader = null!;
    private ScrollingTextBlock _categoryHeader = null!;
    private ScrollingTextBlock _statusHeader = null!;
    private readonly DispatcherTimer _closeTimer;
    private readonly List<ClassificationRow> _standingRows = [];
    private UserSettings _settings;
    private ChampionshipDisplaySettings _display;
    private TimingSnapshot? _snapshot;
    private double _driverColumnWidth = MinimumDriverColumn;
    private int _driverSignature;
    private int _visibleRowCount;

    /// <summary>Root padding and border, added to the column total to get the window width.</summary>
    private const double ChromeWidth = 26;
    private const double MinimumDriverColumn = 150;
    private const double MaximumDriverColumn = 260;
    /// <summary>The driver grid's own left and right margins.</summary>
    private const double DriverPadding = 12;
    /// <summary>Manufacturer badge plus its trailing margin.</summary>
    private const double LogoWidth = 32;
    private const double FastestMarkerWidth = 14;
    private const double RowHeight = 42;
    /// <summary>Session header, column header and vertical padding above the rows.</summary>
    private const double ChromeHeight = 85;
    /// <summary>Rows shown before the classification starts scrolling.</summary>
    private const int MaximumVisibleRows = 12;

    public event EventHandler? PointerEntered;

    public ClassificationFlyout(UserSettings settings)
    {
        _settings = settings.Normalize();
        _display = _settings.DisplayFor(Championship.Formula1);
        _title.Clock = _scrollClock;
        _sessionDetails.Clock = _scrollClock;
        // Width follows the visible columns and the widest name in the field; height follows the row
        // count until it reaches the cap, after which the classification scrolls.
        Width = MinimumDriverColumn + ChromeWidth;
        SizeToContent = SizeToContent.Height;
        MaxHeight = Math.Min(
            Math.Round(SystemParameters.WorkArea.Height * 0.7),
            ChromeHeight + (MaximumVisibleRows * RowHeight));
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        SetResourceReference(FontFamilyProperty, "MotorsportFontFamily");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);

        var root = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 10, 12, 10),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 3,
                Direction = 270,
                Opacity = 0.28
            }
        };
        // Fluent flyouts use a solid layer surface. Transparency is limited to the
        // rounded window corners; the panel itself must remain an opaque backdrop.
        root.SetResourceReference(Border.BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        root.SetResourceReference(Border.BorderBrushProperty, "SurfaceStrokeColorFlyoutBrush");

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _sessionHeader = CreateSessionHeader();
        layout.Children.Add(_sessionHeader);

        _columnHeader = CreateColumnHeader();
        Grid.SetRow(_columnHeader, 1);
        layout.Children.Add(_columnHeader);

        var scroll = new ScrollViewer
        {
            Content = _rows,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 2, 0, 0)
        };
        Grid.SetRow(scroll, 2);
        layout.Children.Add(scroll);

        root.Child = layout;
        Content = root;

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            Hide();
        };
        MouseEnter += (_, _) =>
        {
            PointerEntered?.Invoke(this, EventArgs.Empty);
            CancelClose();
        };
        MouseLeave += (_, _) => ScheduleClose();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) return;
            _scrollClock.Reset();
            // Updates are skipped while hidden, so the retained snapshot is drawn on the way back in.
            RefreshFromSnapshot();
        };
    }

    public void ApplySettings(UserSettings settings)
    {
        _settings = settings.Normalize();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        NativeNoActivate.Set(hwnd);
    }

    public void UpdateSnapshot(TimingSnapshot snapshot)
    {
        _snapshot = snapshot;
        // A closed flyout is redrawn when it opens, so keeping it current while hidden is pure cost.
        if (!IsVisible) return;
        _display = _settings.DisplayFor(snapshot.Championship);
        _visibleRowCount = Math.Min(snapshot.Competitors.Count, _display.FlyoutCompetitorCount);
        UpdateDriverColumnWidth(snapshot);
        ApplyDisplay();
        _title.Text = string.IsNullOrWhiteSpace(snapshot.Meeting) ? "Live classification" : ChampionshipDisplay.FullEvent(snapshot);
        var rally = IsRallySnapshot(snapshot);
        var endurance = IsEnduranceSnapshot(snapshot);
        var lapTimeMode = ChampionshipDisplay.LapTimeModeFor(snapshot);
        _lapTimeHeader.Text = rally ? "TIME" : lapTimeMode == LapTimeMode.LastLap ? "LAST" : "BEST";
        _categoryHeader.Text = rally || endurance ? "CLASS" : "LAP";
        _sessionDetails.Text = string.Join("  ·  ", new[]
        {
            snapshot.Session,
            snapshot.Circuit,
            SessionProgress(snapshot)
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        UpdateConnectionBadge(snapshot.ConnectionState);
        UpdateTrackBadge(snapshot.TrackCondition);

        UpdateRows(snapshot, new ClassificationRowFormat(rally, endurance, lapTimeMode));
    }

    /// <summary>Redraws the classification by reusing the realized rows instead of rebuilding the visual tree.</summary>
    private void UpdateRows(TimingSnapshot snapshot, ClassificationRowFormat format)
    {
        var standings = snapshot.Competitors
            .OrderBy(standing => standing.Position)
            .Take(_display.FlyoutCompetitorCount)
            .ToList();

        while (_standingRows.Count < standings.Count)
        {
            var row = new ClassificationRow(_scrollClock);
            _standingRows.Add(row);
            _rows.Children.Add(row.Element);
        }
        while (_standingRows.Count > standings.Count)
        {
            _rows.Children.RemoveAt(_rows.Children.Count - 1);
            _standingRows.RemoveAt(_standingRows.Count - 1);
        }

        var widths = ColumnWidths();
        for (var index = 0; index < standings.Count; index++)
        {
            var row = _standingRows[index];
            row.ApplyDisplay(_display, widths);
            row.Update(standings[index], snapshot.Championship, _display, format);
        }
    }

    /// <summary>Redraws from the retained snapshot, used when the flyout becomes visible again.</summary>
    private void RefreshFromSnapshot()
    {
        if (_snapshot is { } snapshot) UpdateSnapshot(snapshot);
    }

    /// <summary>
    /// Sizes the driver column to the widest name and team in the field, capped so that endurance entries
    /// with long names scroll within the column instead of widening the flyout without bound. The field is
    /// fixed for a session, so this settles immediately and does not resize as positions change.
    /// </summary>
    private void UpdateDriverColumnWidth(TimingSnapshot snapshot)
    {
        var signature = new HashCode();
        foreach (var standing in snapshot.Competitors)
        {
            signature.Add(standing.Name);
            signature.Add(standing.Team);
        }
        signature.Add(_display.FlyoutShowTeamNames);
        signature.Add(_display.FlyoutUseManufacturerLogos);
        var current = signature.ToHashCode();
        if (current == _driverSignature) return;
        _driverSignature = current;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var nameFace = new Typeface(FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var teamFace = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        double widest = 0;
        foreach (var standing in snapshot.Competitors)
        {
            var name = string.IsNullOrWhiteSpace(standing.Name) ? standing.Code : standing.Name;
            // The fastest-lap dot sits beside the name, so it has to be reserved or it would clip the text.
            widest = Math.Max(widest, Measure(name, nameFace, 11.5, dpi) + FastestMarkerWidth);
            if (_display.FlyoutShowTeamNames)
                widest = Math.Max(widest, Measure(standing.Team, teamFace, 9.5, dpi));
        }

        var logo = _display.FlyoutUseManufacturerLogos ? LogoWidth : 0;
        _driverColumnWidth = Math.Clamp(
            Math.Ceiling(widest) + logo + DriverPadding, MinimumDriverColumn, MaximumDriverColumn);

        double Measure(string text, Typeface face, double size, double pixelsPerDip) =>
            string.IsNullOrEmpty(text)
                ? 0
                : new FormattedText(text, CultureInfo.CurrentUICulture, System.Windows.FlowDirection.LeftToRight, face, size,
                    Brushes.Black, pixelsPerDip).WidthIncludingTrailingWhitespace;
    }

    private void ApplyDisplay()
    {
        _title.Visibility = _display.FlyoutShowEventHeader ? Visibility.Visible : Visibility.Collapsed;
        _sessionDetails.Visibility = _display.FlyoutShowSessionDetails ? Visibility.Visible : Visibility.Collapsed;
        _trackBadge.Visibility = _display.FlyoutShowTrackStatus ? Visibility.Visible : Visibility.Collapsed;
        _connectionBadge.Visibility = _display.FlyoutShowConnectionStatus ? Visibility.Visible : Visibility.Collapsed;
        _badges.Visibility = _display.FlyoutShowTrackStatus || _display.FlyoutShowConnectionStatus
            ? Visibility.Visible : Visibility.Collapsed;
        _sessionHeader.Visibility = _display.FlyoutShowEventHeader || _display.FlyoutShowSessionDetails ||
            _display.FlyoutShowTrackStatus || _display.FlyoutShowConnectionStatus
            ? Visibility.Visible : Visibility.Collapsed;
        _gapHeader.Visibility = _display.FlyoutShowGaps ? Visibility.Visible : Visibility.Collapsed;
        _intervalHeader.Visibility = _display.FlyoutShowIntervals ? Visibility.Visible : Visibility.Collapsed;
        _lapTimeHeader.Visibility = _display.FlyoutShowLapTime ? Visibility.Visible : Visibility.Collapsed;
        _tyreHeader.Visibility = _display.FlyoutShowTyre ? Visibility.Visible : Visibility.Collapsed;
        _categoryHeader.Visibility = _display.FlyoutShowCategoryOrLap ? Visibility.Visible : Visibility.Collapsed;
        _statusHeader.Visibility = _display.FlyoutShowStatus ? Visibility.Visible : Visibility.Collapsed;
        ApplyColumnWidths(_columnHeader);
        // Every column keeps its full width, so the window grows to fit rather than truncating values.
        // Once the classification scrolls, the scrollbar needs its own space or it would clip the last column.
        var scrollbar = _visibleRowCount > MaximumVisibleRows ? SystemParameters.VerticalScrollBarWidth : 0;
        Width = ColumnWidths().Sum(column => column.Value) + ChromeWidth + scrollbar;
    }

    public void ScheduleClose()
    {
        _closeTimer.Stop();
        _closeTimer.Start();
    }

    public void CancelClose() => _closeTimer.Stop();

    private static bool IsRallySnapshot(TimingSnapshot snapshot) => snapshot.Championship == Championship.WorldRallyChampionship || snapshot.Meeting.Contains("Rally", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SS", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SHAKEDOWN", StringComparison.OrdinalIgnoreCase);
    private static bool IsEnduranceSnapshot(TimingSnapshot snapshot) => snapshot.Championship is Championship.GtWorldChallengeEurope or Championship.Imsa;

    private static string SessionProgress(TimingSnapshot snapshot)
    {
        if (IsRallySnapshot(snapshot)) return snapshot.Session;
        if (!string.IsNullOrWhiteSpace(snapshot.TimeRemaining))
        {
            var endurance = IsEnduranceSnapshot(snapshot);
            var timedSession = endurance ||
                snapshot.Session.Contains("Practice", StringComparison.OrdinalIgnoreCase) ||
                snapshot.Session.Contains("Qualifying", StringComparison.OrdinalIgnoreCase);
            if (timedSession)
            {
                if (endurance && snapshot.CurrentLap > 0)
                    return $"Lap {snapshot.CurrentLap} · {snapshot.TimeRemaining} remaining";
                return $"Time remaining {snapshot.TimeRemaining}";
            }
            if (snapshot.TotalLaps is null && snapshot.CurrentLap > 0)
                return $"Lap {snapshot.CurrentLap} · {snapshot.TimeRemaining} remaining";
        }
        return $"Lap {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}";
    }

    private Grid CreateSessionHeader()
    {
        var header = new Grid { Margin = new Thickness(2, 1, 2, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _title.FontSize = 15;
        _title.FontWeight = FontWeights.SemiBold;
        _title.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        _sessionDetails.FontSize = 10.5;
        _sessionDetails.Margin = new Thickness(0, 2, 0, 0);
        _sessionDetails.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "TextFillColorSecondaryBrush");
        copy.Children.Add(_title);
        copy.Children.Add(_sessionDetails);
        header.Children.Add(copy);

        _badges = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        ConfigureBadge(_trackBadge, _trackText);
        ConfigureBadge(_connectionBadge, _connectionText);
        _trackBadge.Margin = new Thickness(0, 0, 6, 0);
        _badges.Children.Add(_trackBadge);
        _badges.Children.Add(_connectionBadge);
        Grid.SetColumn(_badges, 1);
        header.Children.Add(_badges);
        return header;
    }

    private Grid CreateColumnHeader()
    {
        var header = CreateColumns();
        header.Margin = new Thickness(2, 0, 4, 2);
        AddHeaderLabel(header, "POS", 0);
        AddHeaderLabel(header, "DRIVER", 1);
        _gapHeader = AddHeaderLabel(header, "GAP", 2);
        _intervalHeader = AddHeaderLabel(header, "INTERVAL", 3);
        _lapTimeHeader = AddHeaderLabel(header, "BEST", 4);
        _tyreHeader = AddHeaderLabel(header, "TYRE", 5, TextAlignment.Center);
        _categoryHeader = AddHeaderLabel(header, "LAP", 6, TextAlignment.Center);
        _statusHeader = AddHeaderLabel(header, "STATUS", 7, TextAlignment.Center);
        return header;
    }

    private void UpdateConnectionBadge(ConnectionState state)
    {
        _connectionText.Text = state.ToString().ToUpperInvariant();
        _connectionBadge.Background = state switch
        {
            ConnectionState.Connected => BrushFrom(30, 138, 88, 0.28),
            ConnectionState.Connecting => BrushFrom(33, 132, 188, 0.28),
            ConnectionState.Stale => BrushFrom(207, 133, 24, 0.35),
            ConnectionState.Faulted => BrushFrom(199, 45, 70, 0.35),
            _ => BrushFrom(92, 96, 106, 0.3)
        };
    }

    private void UpdateTrackBadge(TrackCondition condition)
    {
        (_trackText.Text, _trackBadge.Background) = condition switch
        {
            TrackCondition.AllClear => ("GREEN", BrushFrom(30, 138, 88, 0.28)),
            TrackCondition.Yellow => ("YELLOW", BrushFrom(218, 164, 22, 0.36)),
            TrackCondition.DoubleYellow => ("DOUBLE YELLOW", BrushFrom(218, 164, 22, 0.36)),
            TrackCondition.SafetyCar => ("SAFETY CAR", BrushFrom(210, 119, 25, 0.38)),
            TrackCondition.VirtualSafetyCar => ("VSC", BrushFrom(210, 119, 25, 0.38)),
            TrackCondition.VirtualSafetyCarEnding => ("VSC ENDING", BrushFrom(210, 119, 25, 0.38)),
            TrackCondition.RedFlag => ("RED FLAG", BrushFrom(204, 40, 62, 0.42)),
            _ => ("TRACK —", BrushFrom(92, 96, 106, 0.3))
        };
    }

    private static void ConfigureBadge(Border badge, TextBlock text)
    {
        badge.CornerRadius = new CornerRadius(7);
        badge.Padding = new Thickness(8, 4, 8, 4);
        text.Foreground = Brushes.White;
        text.FontSize = 9.5;
        text.FontWeight = FontWeights.SemiBold;
        badge.Child = text;
    }



    private Grid CreateColumns()
    {
        var grid = new Grid();
        for (var index = 0; index < 8; index++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        ApplyColumnWidths(grid);
        return grid;
    }

    private void ApplyColumnWidths(Grid grid)
    {
        var widths = ColumnWidths();
        for (var index = 0; index < widths.Count; index++) grid.ColumnDefinitions[index].Width = widths[index];
    }


    private IReadOnlyList<GridLength> ColumnWidths() =>
    [
        new GridLength(42),
        new GridLength(_driverColumnWidth),
        _display.FlyoutShowGaps ? new GridLength(78) : new GridLength(0),
        _display.FlyoutShowIntervals ? new GridLength(78) : new GridLength(0),
        _display.FlyoutShowLapTime ? new GridLength(78) : new GridLength(0),
        _display.FlyoutShowTyre ? new GridLength(60) : new GridLength(0),
        _display.FlyoutShowCategoryOrLap ? new GridLength(58) : new GridLength(0),
        _display.FlyoutShowStatus ? new GridLength(64) : new GridLength(0)
    ];

    private ScrollingTextBlock AddHeaderLabel(Grid grid, string text, int column, TextAlignment alignment = TextAlignment.Left)
    {
        var label = new ScrollingTextBlock
        {
            Text = text,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            Clock = _scrollClock,
            Margin = new Thickness(column == 0 ? 5 : 4, 0, 8, 0),
        };
        label.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "TextFillColorTertiaryBrush");
        Grid.SetColumn(label, column);
        grid.Children.Add(label);
        return label;
    }

    private static SolidColorBrush BrushFrom(byte red, byte green, byte blue, double opacity) =>
        new(Color.FromArgb((byte)Math.Round(opacity * 255), red, green, blue));

    private static class NativeNoActivate
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr h, int i);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);

        public static void Set(IntPtr h) =>
            SetWindowLongPtr(h, -20, new IntPtr(GetWindowLongPtr(h, -20).ToInt64() | 0x08000000L));
    }
}
