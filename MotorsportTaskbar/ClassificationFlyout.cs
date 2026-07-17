// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MotorsportTaskbar.Core;

namespace MotorsportTaskbar.App;

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
    private readonly TextBlock _title = new();
    private readonly TextBlock _sessionDetails = new();
    private readonly TextBlock _connectionText = new();
    private readonly Border _connectionBadge = new();
    private readonly TextBlock _trackText = new();
    private readonly Border _trackBadge = new();
    private TextBlock _tyreHeader = null!;
    private TextBlock _categoryHeader = null!;
    private readonly DispatcherTimer _closeTimer;

    public event EventHandler? PointerEntered;

    public ClassificationFlyout()
    {
        Width = 610;
        Height = 470;
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

        layout.Children.Add(CreateSessionHeader());

        var columnHeader = CreateColumnHeader();
        Grid.SetRow(columnHeader, 1);
        layout.Children.Add(columnHeader);

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
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        NativeNoActivate.Set(hwnd);
    }

    public void UpdateSnapshot(TimingSnapshot snapshot)
    {
        _title.Text = string.IsNullOrWhiteSpace(snapshot.Meeting) ? "Live classification" : snapshot.Meeting;
        var rally = IsRallySnapshot(snapshot);
        _tyreHeader.Text = rally ? "TIME" : "TYRE";
        _categoryHeader.Text = rally ? "CLASS" : "LAP";
        _sessionDetails.Text = string.Join("  ·  ", new[]
        {
            snapshot.Session,
            snapshot.Circuit,
            SessionProgress(snapshot)
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        UpdateConnectionBadge(snapshot.ConnectionState);
        UpdateTrackBadge(snapshot.TrackCondition);

        _rows.Children.Clear();
        foreach (var standing in snapshot.Competitors.OrderBy(standing => standing.Position))
            _rows.Children.Add(CreateStandingRow(standing, rally));
    }

    public void ScheduleClose()
    {
        _closeTimer.Stop();
        _closeTimer.Start();
    }

    public void CancelClose() => _closeTimer.Stop();

    private static bool IsRallySnapshot(TimingSnapshot snapshot) => snapshot.Meeting.Contains("Rally", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SS", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SHAKEDOWN", StringComparison.OrdinalIgnoreCase);
    private static string SessionProgress(TimingSnapshot snapshot)
    {
        if (IsRallySnapshot(snapshot)) return snapshot.Session;
        if ((snapshot.Session.Contains("Practice", StringComparison.OrdinalIgnoreCase) ||
             snapshot.Session.Contains("Qualifying", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(snapshot.TimeRemaining))
            return $"Time remaining {snapshot.TimeRemaining}";
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
        _title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _sessionDetails.FontSize = 10.5;
        _sessionDetails.Margin = new Thickness(0, 2, 0, 0);
        _sessionDetails.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        copy.Children.Add(_title);
        copy.Children.Add(_sessionDetails);
        header.Children.Add(copy);

        var badges = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        ConfigureBadge(_trackBadge, _trackText);
        ConfigureBadge(_connectionBadge, _connectionText);
        _trackBadge.Margin = new Thickness(0, 0, 6, 0);
        badges.Children.Add(_trackBadge);
        badges.Children.Add(_connectionBadge);
        Grid.SetColumn(badges, 1);
        header.Children.Add(badges);
        return header;
    }

    private Grid CreateColumnHeader()
    {
        var header = CreateColumns();
        header.Margin = new Thickness(2, 0, 4, 2);
        AddHeaderLabel(header, "POS", 0);
        AddHeaderLabel(header, "DRIVER", 1);
        AddHeaderLabel(header, "GAP", 2);
        AddHeaderLabel(header, "INTERVAL", 3);
        _tyreHeader = AddHeaderLabel(header, "TYRE", 4);
        _categoryHeader = AddHeaderLabel(header, "LAP", 5, TextAlignment.Center);
        AddHeaderLabel(header, "STATUS", 6, TextAlignment.Center);
        return header;
    }

    private UIElement CreateStandingRow(CompetitorStanding standing, bool rally)
    {
        var grid = CreateColumns();
        grid.MinHeight = 38;

        var positionCard = new Border
        {
            Width = 32,
            Height = 32,
            Margin = new Thickness(5),
            CornerRadius = new CornerRadius(8),
            Background = CreatePositionBrush(standing.Position),
            Effect = new DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 1,
                Direction = 270,
                Opacity = 0.28
            },
            Child = new TextBlock
            {
                Text = standing.PositionLabel ?? standing.Position.ToString(),
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        grid.Children.Add(positionCard);

        var driver = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0),
            ToolTip = string.IsNullOrWhiteSpace(standing.Name) ? null : standing.Name
        };
        var driverLine = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        var driverName = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(standing.Name) ? standing.Code : standing.Name,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        driverName.SetResourceReference(TextBlock.ForegroundProperty,
            standing.Retired ? "TextFillColorTertiaryBrush" : "TextFillColorPrimaryBrush");
        driverLine.Children.Add(driverName);
        if (standing.IsOverallFastest)
        {
            driverLine.Children.Add(new TextBlock
            {
                Text = "●",
                Foreground = new SolidColorBrush(Color.FromRgb(196, 87, 255)),
                FontSize = 9,
                Margin = new Thickness(5, 1, 0, 0),
                ToolTip = "Overall fastest lap"
            });
        }
        var team = new TextBlock
        {
            Text = standing.Team,
            FontSize = 9.5,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        team.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
        driver.Children.Add(driverLine);
        driver.Children.Add(team);
        Grid.SetColumn(driver, 1);
        grid.Children.Add(driver);

        AddValue(grid, standing.Position == 1 ? "LEADER" : standing.GapToLeader ?? "—", 2,
            standing.Position == 1 ? FontWeights.SemiBold : FontWeights.Normal);
        AddValue(grid, standing.Position == 1 ? "—" : standing.IntervalToPositionAhead ?? "—", 3);

        if (rally) AddValue(grid, standing.ResultTime ?? "—", 4);
        else
        {
            var tyre = CreateTyreBadge(standing.Tyre);
            Grid.SetColumn(tyre, 4);
            grid.Children.Add(tyre);
        }

        AddValue(grid, rally ? standing.Category ?? "—" : standing.Lap.ToString(), 5, alignment: TextAlignment.Center);

        var status = CreateStatusBadge(standing);
        Grid.SetColumn(status, 6);
        grid.Children.Add(status);

        if (standing.Retired || standing.Stopped)
            grid.Opacity = 0.62;

        return grid;
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

    private static Border CreateTyreBadge(string? tyre)
    {
        var normalized = string.IsNullOrWhiteSpace(tyre) ? "—" : tyre.Trim().ToUpperInvariant();
        var (background, foreground) = normalized switch
        {
            "SOFT" or "S" => (BrushFrom(205, 45, 62, 0.85), Brushes.White),
            "MEDIUM" or "M" => (BrushFrom(226, 182, 36, 0.9), new SolidColorBrush(Color.FromRgb(35, 35, 35))),
            "HARD" or "H" => (BrushFrom(224, 226, 232, 0.9), new SolidColorBrush(Color.FromRgb(35, 35, 35))),
            "INTERMEDIATE" or "I" => (BrushFrom(40, 156, 87, 0.85), Brushes.White),
            "WET" or "W" => (BrushFrom(35, 115, 196, 0.85), Brushes.White),
            _ => (BrushFrom(92, 96, 106, 0.3), Brushes.White)
        };
        return new Border
        {
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(7, 3, 7, 3),
            Margin = new Thickness(4, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Background = background,
            Child = new TextBlock
            {
                Text = normalized,
                Foreground = foreground,
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold
            }
        };
    }

    private static Border CreateStatusBadge(CompetitorStanding standing)
    {
        var (text, background) = standing switch
        {
            { Retired: true } => ("RET", BrushFrom(199, 45, 70, 0.35)),
            { Stopped: true } => ("STOP", BrushFrom(199, 45, 70, 0.35)),
            { InPit: true } => ("PIT", BrushFrom(33, 132, 188, 0.3)),
            { StatusLabel: "RUN" } => ("RUN", BrushFrom(33, 132, 188, 0.3)),
            { StatusLabel: "DUE" } => ("DUE", BrushFrom(92, 96, 106, 0.3)),
            _ => ("", Brushes.Transparent)
        };
        return new Border
        {
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(7, 3, 7, 3),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = background,
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold
            }
        };
    }

    private static Grid CreateColumns()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
        return grid;
    }

    private static TextBlock AddHeaderLabel(Grid grid, string text, int column, TextAlignment alignment = TextAlignment.Left)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(column == 0 ? 5 : 4, 0, 8, 0),
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
        Grid.SetColumn(label, column);
        grid.Children.Add(label);
        return label;
    }

    private static void AddValue(Grid grid, string text, int column, FontWeight? weight = null,
        TextAlignment alignment = TextAlignment.Left)
    {
        var value = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = weight ?? FontWeights.Normal,
            TextAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(4, 0, 8, 0)
        };
        value.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        Grid.SetColumn(value, column);
        grid.Children.Add(value);
    }

    private static LinearGradientBrush CreatePositionBrush(int position)
    {
        (Color Start, Color End) colors = position is >= 1 and <= 5
            ? PositionColors[position - 1]
            : (Color.FromRgb(52, 55, 63), Color.FromRgb(83, 88, 100));
        return new LinearGradientBrush(colors.Start, colors.End, 35);
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
