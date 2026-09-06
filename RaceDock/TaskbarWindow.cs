// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using RaceDock.Core;
using Wpf.Ui.Appearance;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace RaceDock.App;

public sealed class TaskbarWindow : Window
{
    private static readonly FontFamily TaskbarFont = new("Segoe UI Variable, Microsoft YaHei UI, Yu Gothic UI");
    private readonly Grid _root = new();
    private readonly IdleTaskbarView _idle = new();
    private readonly Border _hoverSurface;
    private readonly ColumnDefinition _contextColumn = new() { Width = GridLength.Auto, MinWidth = 72, MaxWidth = 100 };
    private readonly ColumnDefinition _stripColumn = new() { Width = new GridLength(1, GridUnitType.Star) };
    private readonly UniformGrid _strip = new() { Rows = 1, Columns = 5, Margin = new(2) };
    private readonly Grid _raceContext = new() { Margin = new(6, 2, 4, 2), VerticalAlignment = VerticalAlignment.Center };
    private readonly RowDefinition _contextFirstRow = new() { Height = new GridLength(17) };
    private readonly RowDefinition _contextSecondRow = new() { Height = new GridLength(17) };
    private readonly ScrollingTextBlock _raceText = new() { FontSize = 12, FontWeight = FontWeights.Normal };
    private readonly TextBlock _lapText = new() { FontSize = 12, FontWeight = FontWeights.Normal, Opacity = 0.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _eventHeader = new() { MaxWidth = 150 };
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
    private readonly ClassificationFlyout _flyout;
    private readonly List<TaskbarCell> _cells = [];
    private readonly DispatcherTimer _reattach;
    private TimingSnapshot? _snapshot;
    private UpcomingSession? _nextSession;
    private IReadOnlyList<UpcomingSession> _upcoming = [];
    private RaceAlert? _currentAlert;
    private UserSettings _settings;
    private ChampionshipDisplaySettings _display;

    public TaskbarWindow(UserSettings settings)
    {
        _settings = settings.Normalize();
        _display = _settings.DisplayFor(Championship.Formula1);
        _flyout = new ClassificationFlyout(_settings);
        Width = 520; Height = 40; MinHeight = 40; MaxHeight = 40; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        UseLayoutRounding = true; SnapsToDevicePixels = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
        FontFamily = TaskbarFont;
        _root.Background = Brushes.Transparent;
        _flagCanvas.Children.Add(_flagPole); Canvas.SetLeft(_flagPole, 1); Canvas.SetTop(_flagPole, 1);
        _flagCanvas.Children.Add(_flagShape); _flagCanvas.Children.Add(_secondFlagShape);
        _alert.Child = _flagCanvas;
        _root.ColumnDefinitions.Add(_contextColumn);
        _root.ColumnDefinitions.Add(_stripColumn);
        _raceContext.RowDefinitions.Add(_contextFirstRow);
        _raceContext.RowDefinitions.Add(_contextSecondRow);
        _eventHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _eventHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_raceText, 0); Grid.SetColumn(_alert, 1);
        _eventHeader.Children.Add(_raceText); _eventHeader.Children.Add(_alert);
        Grid.SetRow(_eventHeader, 0); Grid.SetRow(_lapText, 1);
        _raceContext.Children.Add(_eventHeader); _raceContext.Children.Add(_lapText);
        Grid.SetColumn(_raceContext, 0); Grid.SetColumn(_strip, 1);
        _root.Children.Add(_raceContext); _root.Children.Add(_strip);
        Grid.SetColumnSpan(_idle.Element, 2);
        _idle.Element.Visibility = Visibility.Collapsed;
        _root.Children.Add(_idle.Element);
        // The widget lights as one surface, the way a taskbar button does.
        _hoverSurface = new Border { Child = _root, CornerRadius = new CornerRadius(6), SnapsToDevicePixels = true };
        SurfaceMotion.AttachHover(_hoverSurface, preserveHitTarget: true);
        Content = _hoverSurface;
        _host = new(this); SourceInitialized += (_, _) => _host.Attach();
        Loaded += (_, _) =>
        {
            SystemThemeWatcher.Watch(this, WindowBackdropType.None);
            _host.Reattach();
        };
        _host.PositionChanged += visible => { if (!visible) _flyout.Hide(); else if (_flyout.IsVisible) _host.PositionFlyout(_flyout); };
        MouseLeftButtonUp += (_, _) => ShowFlyout(); MouseLeave += (_, _) => { if (_flyout.IsVisible) _flyout.ScheduleClose(); };
        _flyout.PointerEntered += (_, _) => _flyout.CancelClose();
        // The flyout resizes when columns are toggled or the field changes, so it has to be re-centred.
        _flyout.SizeChanged += (_, _) => { if (_flyout.IsVisible) _host.PositionFlyout(_flyout); };
        // Positioning shows the window at the Win32 level, so a hidden flyout must never be positioned:
        // a closing animation that outlives the hide would otherwise put an empty panel back on screen.
        _flyout.SlideTo = offset => { if (_flyout.IsVisible) _host.PositionFlyout(_flyout, offset); };
        _reattach = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => _host.Reattach(), Dispatcher); _reattach.Start();
        ApplySettings(_settings);
    }

    /// <summary>Whether a classification currently owns the widget, which the countdown must not displace.</summary>
    public bool ShowingSnapshot => _snapshot is not null && IsVisible;

    public void ApplySettings(UserSettings settings)
    {
        _settings = settings.Normalize();
        SurfaceMotion.Enabled = _settings.InterfaceAnimations;
        _host.ApplyPosition(_settings.WidgetPosition);
        ApplyDisplay(_snapshot?.Championship ?? Championship.Formula1);
        ApplyTaskbarLayout();
        _flyout.ApplySettings(_settings);
        SetAlert(_currentAlert);
        if (_snapshot is not null) UpdateSnapshot(_snapshot);
        else if (_nextSession is not null)
        {
            _idle.Update(_nextSession, DateTimeOffset.UtcNow, TimeZoneInfo.Local, _settings.UseSingleRowTaskbar);
            ApplyIdleWidth();
        }
    }

    private void ApplyDisplay(Championship championship)
    {
        _display = _settings.DisplayFor(championship);
        _strip.Columns = _display.TaskbarCompetitorCount;
    }

    private void ApplyTaskbarLayout()
    {
        var singleRow = _settings.UseSingleRowTaskbar;
        _contextFirstRow.Height = singleRow ? GridLength.Auto : new GridLength(17);
        _contextSecondRow.Height = singleRow ? new GridLength(0) : new GridLength(17);
        _lapText.Visibility = singleRow ? Visibility.Collapsed : Visibility.Visible;
        _raceContext.Margin = singleRow ? new Thickness(4, 2, 2, 2) : new Thickness(6, 2, 4, 2);
        if (_snapshot is null) return;
        var contextWidth = singleRow ? 150 : 100;
        _contextColumn.Width = GridLength.Auto;
        _contextColumn.MaxWidth = contextWidth;
        _eventHeader.MaxWidth = contextWidth - _raceContext.Margin.Left - _raceContext.Margin.Right;
        ConstrainRaceText(contextWidth);
    }

    private void ConstrainRaceText(double contextWidth)
    {
        var contextMargin = _raceContext.Margin.Left + _raceContext.Margin.Right;
        var alertWidth = _alert.Visibility == Visibility.Visible
            ? _alert.Width + _alert.Margin.Left + _alert.Margin.Right
            : 0;
        _raceText.MaxWidth = Math.Max(0, contextWidth - contextMargin - alertWidth);
    }

    /// <summary>
    /// Shows the next published session in place of a classification. This is the state the widget sits in
    /// between sessions, so it is deliberately narrow and carries no strip, flag, or flyout.
    /// </summary>
    public void ShowNextSession(UpcomingSession session, IReadOnlyList<UpcomingSession> upcoming, DateTimeOffset now)
    {
        _nextSession = session;
        _upcoming = upcoming;
        _snapshot = null;
        _raceContext.Visibility = Visibility.Collapsed;
        _strip.Visibility = Visibility.Collapsed;
        _stripColumn.Width = new GridLength(0);
        _contextColumn.Width = new GridLength(1, GridUnitType.Star);
        _contextColumn.MaxWidth = double.PositiveInfinity;
        _eventHeader.MaxWidth = double.PositiveInfinity;
        _idle.Update(session, now, TimeZoneInfo.Local, _settings.UseSingleRowTaskbar);
        _idle.Element.Visibility = Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(this,
            NextSessionDisplay.Tooltip(session, TimeZoneInfo.Local));
        RefreshFlag();
        ApplyIdleWidth();
        if (!IsVisible) Show();
    }

    private void ApplyIdleWidth() => _host.ApplyWidth(
        _settings.UseSingleRowTaskbar ? _idle.LogicalWidth : IdleLogicalWidth,
        IdleMinimumLogicalWidth);

    // Two short lines, so the widget gives back most of the room a classification needs.
    private const double IdleLogicalWidth = 210, IdleMinimumLogicalWidth = 180;

    public void UpdateSnapshot(TimingSnapshot snapshot)
    {
        if (_nextSession is not null)
        {
            _nextSession = null;
            _idle.Element.Visibility = Visibility.Collapsed;
            _raceContext.Visibility = Visibility.Visible;
            _strip.Visibility = Visibility.Visible;
            _stripColumn.Width = new GridLength(1, GridUnitType.Star);
            _contextColumn.MaxWidth = 100;
            _eventHeader.MaxWidth = 150;
            _host.ApplyWidth(520, 420);
        }
        _snapshot = snapshot;
        ApplyDisplay(snapshot.Championship);
        ApplyTaskbarLayout();
        var eventName = ChampionshipDisplay.CompactEvent(snapshot);
        var progress = FormatSessionProgress(snapshot);
        _raceText.Text = _settings.UseSingleRowTaskbar ? $"{eventName} · {progress}" : eventName;
        _raceText.ToolTip = ChampionshipDisplay.FullEvent(snapshot);
        _lapText.Text = progress;
        UpdateCells(snapshot);
        _flyout.UpdateSnapshot(snapshot);
        RefreshFlag();
    }

    private static bool IsRallySnapshot(TimingSnapshot snapshot) => snapshot.Championship == Championship.WorldRallyChampionship || snapshot.Meeting.Contains("Rally", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SS", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SHAKEDOWN", StringComparison.OrdinalIgnoreCase);
    private static string FormatSessionProgress(TimingSnapshot snapshot)
    {
        if (IsRallySnapshot(snapshot)) return snapshot.Session;
        var session = SessionLabel(snapshot);
        if (!string.IsNullOrWhiteSpace(snapshot.TimeRemaining))
        {
            var time = CompactTime(snapshot.TimeRemaining);
            if (snapshot.Championship == Championship.Imsa)
                return $"{session} {time}";
            if (snapshot.Championship == Championship.GtWorldChallengeEurope)
                return snapshot.CurrentLap > 0 ? $"{session} {snapshot.CurrentLap} · {time}" : $"{session} {time}";
            if (IsTimedSession(snapshot.Session)) return $"{session} {time}";
            if (snapshot.TotalLaps is null && snapshot.CurrentLap > 0) return $"{session} {snapshot.CurrentLap} · {time}";
        }
        return snapshot.CurrentLap > 0
            ? $"{session} {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}"
            : session;
    }

    private static string SessionLabel(TimingSnapshot snapshot)
    {
        if ((snapshot.Championship is Championship.MotoGp or Championship.Moto2 or Championship.Moto3) &&
            !string.IsNullOrWhiteSpace(snapshot.SessionShortName))
            return ShortMotoSessionName(snapshot.SessionShortName!, snapshot.Session);
        return ShortSessionName(snapshot.Session);
    }

    private static string ShortMotoSessionName(string shortName, string fullSession) => shortName.Trim().ToUpperInvariant() switch
    {
        "PR" or "P" or "PRACTICE" => "P",
        "FP1" or "FP2" or "FP3" => shortName.Trim().ToUpperInvariant(),
        "Q" or "QUALIFYING" => "Q",
        "Q1" => "Q1",
        "Q2" => "Q2",
        "RAC" or "RACE" => "RAC",
        "SPR" or "SPRINT" => "SPR",
        "WUP" or "WARMUP" => "WUP",
        _ => ShortSessionName(StripMotoCategory(fullSession))
    };

    private static string StripMotoCategory(string session)
    {
        foreach (var prefix in new[] { "MotoGP ", "Moto2 ", "Moto3 " })
            if (session.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return session[prefix.Length..];
        return session;
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
        "Qualifying 1 Group A" => "Q1A",
        "Qualifying 1 Group B" => "Q1B",
        "Qualifying Group A" => "QGA",
        "Qualifying Group B" => "QGB",
        "Qualifying Duel Quarter-Final 1" => "QF1",
        "Qualifying Duel Quarter-Final 2" => "QF2",
        "Qualifying Duel Quarter-Final 3" => "QF3",
        "Qualifying Duel Quarter-Final 4" => "QF4",
        "Qualifying Duel Semi-Final 1" => "SF1",
        "Qualifying Duel Semi-Final 2" => "SF2",
        "Qualifying Duel Final" => "FIN",
        "Combined Qualifying 1" => "CQ1",
        "Qualifying 2" => "Q2",
        "Qualifying 2 Group A" => "Q2A",
        "Qualifying 2 Group B" => "Q2B",
        "Combined Qualifying 2" => "CQ2",
        "Qualifying 3" => "Q3",
        "Race" => "RAC",
        "Race 1" => "R1",
        "Race 2" => "R2",
        _ => session.ToUpperInvariant()
    };

    private static bool IsTimedSession(string session) =>
        session.Contains("Practice", StringComparison.OrdinalIgnoreCase) ||
        session.Contains("Qualifying", StringComparison.OrdinalIgnoreCase);

    private static string CompactTime(string value) =>
        TimeSpan.TryParse(value, out var remaining)
            ? remaining.ToString(remaining.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss")
            : value;

    public void SetAlert(RaceAlert? alert)
    {
        _currentAlert = alert;
        RefreshFlag();
    }

    private void RefreshFlag()
    {
        AlertKind? transient = _currentAlert?.Kind == AlertKind.Chequered ? AlertKind.Chequered : null;
        var kind = _snapshot is null ? null : TaskbarFlagDisplay.Resolve(_snapshot.TrackCondition, transient);
        var show = kind is not null && _snapshot is not null && !IsRallySnapshot(_snapshot);
        _alert.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (_snapshot is not null) ConstrainRaceText(_settings.UseSingleRowTaskbar ? 150 : 100);
        if (!show || kind is null) return;
        _secondFlagShape.Visibility = kind == AlertKind.DoubleYellow ? Visibility.Visible : Visibility.Collapsed;
        _flagShape.Fill = FlagBrush(kind.Value);
        _secondFlagShape.Fill = _flagShape.Fill;
        var title = kind.Value switch
        {
            AlertKind.RedFlag => "RED FLAG",
            AlertKind.DoubleYellow => "DOUBLE YELLOW",
            AlertKind.Yellow => "YELLOW FLAG",
            _ => "CHEQUERED FLAG"
        };
        var detail = kind == AlertKind.Chequered ? _currentAlert?.Detail : null;
        _alert.ToolTip = string.IsNullOrWhiteSpace(detail) ? title : $"{title}  ·  {detail}";
        System.Windows.Automation.AutomationProperties.SetName(_alert, title);
    }

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

    /// <summary>Redraws the strip by reusing the realized cells instead of rebuilding the visual tree.</summary>
    private void UpdateCells(TimingSnapshot snapshot)
    {
        var duel = snapshot.Championship == Championship.FormulaE &&
            snapshot.Session.Contains("Duel", StringComparison.OrdinalIgnoreCase);
        var standings = snapshot.Competitors.Take(duel ? 2 : _display.TaskbarCompetitorCount).ToList();
        var count = duel ? standings.Count : Math.Max(standings.Count, _display.TaskbarCompetitorCount);

        while (_cells.Count < count)
        {
            var cell = new TaskbarCell();
            _cells.Add(cell);
            _strip.Children.Add(cell.Element);
        }
        while (_cells.Count > count)
        {
            _strip.Children.RemoveAt(_strip.Children.Count - 1);
            _cells.RemoveAt(_cells.Count - 1);
        }

        for (var index = 0; index < count; index++)
        {
            if (index < standings.Count)
                _cells[index].Update(standings[index], snapshot.Championship, _display, standings[index].Code,
                    _settings.UseSingleRowTaskbar);
            else _cells[index].UpdateEmpty(index + 1, _settings.UseSingleRowTaskbar);
        }
    }

    private void ShowFlyout()
    {
        // Between sessions the flyout carries the schedule, which is the only thing there is to show.
        if (_snapshot is null && !PrepareSchedule()) return;
        // Sized and placed before the reveal, so the slide starts from the correct resting point.
        _flyout.Show(); _flyout.UpdateLayout(); _host.PositionFlyout(_flyout); _flyout.Reveal();
    }

    private bool PrepareSchedule()
    {
        if (_nextSession is null) return false;
        _flyout.ShowSchedule(_upcoming, DateTimeOffset.UtcNow);
        return true;
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (IsLoaded) SystemThemeWatcher.UnWatch(this);
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e) { _reattach.Stop(); _flyout.Close(); base.OnClosed(e); }
}
