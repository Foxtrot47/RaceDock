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
    private readonly ColumnDefinition _contextColumn = new() { Width = GridLength.Auto, MinWidth = 72, MaxWidth = 130 };
    private readonly ColumnDefinition _stripColumn = new() { Width = new GridLength(1, GridUnitType.Star) };
    private readonly UniformGrid _strip = new() { Rows = 1, Columns = 5, Margin = new(2) };
    private readonly Grid _raceContext = new() { Margin = new(6, 2, 4, 2), VerticalAlignment = VerticalAlignment.Center };
    private readonly RowDefinition _contextFirstRow = new() { Height = new GridLength(17) };
    private readonly RowDefinition _contextSecondRow = new() { Height = new GridLength(17) };
    private readonly ScrollingTextBlock _raceText = new() { FontSize = 12, FontWeight = FontWeights.Normal };
    private readonly TextBlock _lapText = new() { FontSize = 12, FontWeight = FontWeights.Normal, Opacity = 0.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _eventHeader = new() { MaxWidth = 150 };
    /// <summary>The widget's left edge carries the flag in a column of its own, out of the event text.</summary>
    private const double FlagStripWidth = 11;
    private readonly ColumnDefinition _flagColumn = new() { Width = GridLength.Auto };
    private readonly TextBlock _flagLabel = new()
    {
        FontSize = 9,
        FontWeight = FontWeights.SemiBold,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        LineHeight = 12,
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight
    };
    private readonly Border _flagStrip = new()
    {
        Width = FlagStripWidth,
        Visibility = Visibility.Collapsed,
        VerticalAlignment = VerticalAlignment.Stretch,
        CornerRadius = new CornerRadius(2, 0, 0, 2),
        SnapsToDevicePixels = true
    };
    private readonly NativeTaskbar _host;
    private readonly ClassificationFlyout _flyout;
    private readonly List<TaskbarCell> _cells = [];
    private readonly DispatcherTimer _reattach;
    private TimingSnapshot? _snapshot;
    private TimingSnapshot? _overlaySnapshot;
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
        _flagStrip.Child = _flagLabel;
        _root.ColumnDefinitions.Add(_flagColumn);
        _root.ColumnDefinitions.Add(_contextColumn);
        _root.ColumnDefinitions.Add(_stripColumn);
        _raceContext.RowDefinitions.Add(_contextFirstRow);
        _raceContext.RowDefinitions.Add(_contextSecondRow);
        // The flag reads before the event name, and the column takes no room while none is up.
        _eventHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _eventHeader.Children.Add(_raceText);
        Grid.SetRow(_eventHeader, 0); Grid.SetRow(_lapText, 1);
        _raceContext.Children.Add(_eventHeader); _raceContext.Children.Add(_lapText);
        Grid.SetColumn(_flagStrip, 0); Grid.SetColumn(_raceContext, 1); Grid.SetColumn(_strip, 2);
        _root.Children.Add(_flagStrip); _root.Children.Add(_raceContext); _root.Children.Add(_strip);
        Grid.SetColumnSpan(_idle.Element, 3);
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
        _host.PositionChanged += visible =>
        {
            if (_flyout.IsPinned) return;
            if (!visible) _flyout.Hide();
            else PositionFlyout();
        };
        _flyout.PinChanged += PositionFlyout;
        _flyout.FeedSelectionChanged += feed => OverlayFeedChanged?.Invoke(feed);
        _flyout.SettingsRequested += () => SettingsRequested?.Invoke();
        MouseLeftButtonUp += (_, _) => ShowFlyout(); MouseLeave += (_, _) => { if (_flyout.IsVisible) _flyout.ScheduleClose(); };
        _flyout.PointerEntered += (_, _) => _flyout.CancelClose();
        // The flyout resizes when columns are toggled or the field changes, so it has to be re-centred.
        _flyout.SizeChanged += (_, _) => PositionFlyout();
        // Positioning shows the window at the Win32 level, so a hidden flyout must never be positioned:
        // a closing animation that outlives the hide would otherwise put an empty panel back on screen.
        _flyout.SlideTo = offset => PositionFlyout(offset);
        _reattach = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => _host.Reattach(), Dispatcher); _reattach.Start();
        ApplySettings(_settings);
    }

    /// <summary>Whether a classification currently owns the widget, which the countdown must not displace.</summary>
    public bool ShowingSnapshot => _snapshot is not null && IsVisible;
    public bool IsClosed { get; private set; }
    /// <summary>The feed the flyout is held on, or null while it follows rotation.</summary>
    public Championship? OverlayFeed => _flyout.PinnedFeed;
    public event Action? SettingsRequested;
    public event Action<Championship?>? OverlayFeedChanged;

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
        // Column and layout settings also belong to the panel; redraw what it is currently showing.
        if (_overlaySnapshot is not null) UpdateOverlaySnapshot(_overlaySnapshot);
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
        var contextWidth = ContextWidth;
        _contextColumn.Width = GridLength.Auto;
        _contextColumn.MaxWidth = contextWidth;
        _eventHeader.MaxWidth = contextWidth - _raceContext.Margin.Left - _raceContext.Margin.Right;
        ConstrainRaceText(contextWidth);
    }

    /// <summary>How much room the event block may take. The flag column is inside it, so a flag costs text width.</summary>
    private double ContextWidth => _settings.UseSingleRowTaskbar ? 170 : 130;

    private void ConstrainRaceText(double contextWidth)
    {
        var contextMargin = _raceContext.Margin.Left + _raceContext.Margin.Right;
        _raceText.MaxWidth = Math.Max(0, contextWidth - contextMargin);
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
            NextSessionDisplay.Tooltip(session, DateTimeOffset.UtcNow, TimeZoneInfo.Local));
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
            _contextColumn.MaxWidth = 130;
            _eventHeader.MaxWidth = 150;
            _host.ApplyWidth(520, 420);
        }
        _snapshot = snapshot;
        ApplyDisplay(snapshot.Championship);
        ApplyTaskbarLayout();
        var eventName = ChampionshipDisplay.CompactEvent(snapshot);
        var progress = TaskbarSessionDisplay.Progress(snapshot);
        _raceText.Text = _settings.UseSingleRowTaskbar ? $"{eventName} · {progress}" : eventName;
        _raceText.ToolTip = ChampionshipDisplay.FullEvent(snapshot);
        _lapText.Text = progress;
        UpdateCells(snapshot);
        RefreshFlag();
    }

    /// <summary>Draws a classification on the flyout, which may reject it when held on another feed.</summary>
    public bool UpdateOverlaySnapshot(TimingSnapshot snapshot)
    {
        var accepted = _flyout.UpdateSnapshot(snapshot);
        if (accepted) _overlaySnapshot = snapshot;
        return accepted;
    }

    /// <summary>Publishes the championships the flyout can offer as manual choices.</summary>
    public void UpdateLiveFeeds(IReadOnlyList<LiveFeedStatus> feeds) => _flyout.SetLiveFeeds(feeds);

    /// <summary>Forgets the current choices while the live source is being replaced.</summary>
    public void ResetLiveFeeds() => _flyout.ResetLiveFeeds();

    /// <summary>Returns the panel to rotation, for a source change it should not stay pinned against.</summary>
    public void ResetOverlayFeed() => _flyout.ResetFeedSelection();

    private static bool IsRallySnapshot(TimingSnapshot snapshot) => snapshot.Championship == Championship.WorldRallyChampionship || snapshot.Meeting.Contains("Rally", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SS", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SHAKEDOWN", StringComparison.OrdinalIgnoreCase);
    public void SetAlert(RaceAlert? alert)
    {
        _currentAlert = alert;
        RefreshFlag();
    }

    /// <summary>
    /// Paints the widget's edge from the session's flag and writes its code down the strip. A clear or unknown track
    /// shows nothing at all, so the edge only ever occupies room while it has something to say.
    /// </summary>
    private void RefreshFlag()
    {
        if (_snapshot is not null) ConstrainRaceText(ContextWidth);
        AlertKind? transient = _currentAlert?.Kind == AlertKind.Chequered ? AlertKind.Chequered : null;
        // A finished session shows the chequered tiles from its lifecycle, so the replay sources — which keep their own
        // alert arbiter — report the end of a session exactly as the live ones do.
        var badge = _snapshot is null || IsRallySnapshot(_snapshot)
            ? new TaskbarFlagBadge(TaskbarFlagStrip.None, null)
            : TaskbarFlagDisplay.Badge(_snapshot.TrackCondition, transient,
                finished: _snapshot.Lifecycle == SessionLifecycle.Ended);
        if (badge.Strip == TaskbarFlagStrip.None)
        {
            _flagStrip.Visibility = Visibility.Collapsed;
            return;
        }
        var title = FlagTitle(badge.Strip);
        _flagStrip.Background = badge.Strip == TaskbarFlagStrip.Finished ? ChequeredFlag.Brush(4) : StripBrush(badge.Strip);
        // Join the letters as strings: joining the code binds to the params overload and writes it on one line.
        _flagLabel.Text = badge.Code is { } code
            ? string.Join(Environment.NewLine, code.ToCharArray().Select(letter => letter.ToString()))
            : "";
        _flagLabel.Foreground = LetterBrush(badge.Strip);
        _flagStrip.Visibility = Visibility.Visible;
        var detail = badge.Strip == TaskbarFlagStrip.Finished ? _currentAlert?.Detail : null;
        _flagStrip.ToolTip = string.IsNullOrWhiteSpace(detail) ? title : $"{title}  \u00b7  {detail}";
        System.Windows.Automation.AutomationProperties.SetName(_flagStrip, title);
    }

    private string FlagTitle(TaskbarFlagStrip strip) => strip switch
    {
        TaskbarFlagStrip.Caution => CautionTitle(_snapshot!.TrackCondition),
        TaskbarFlagStrip.RedFlag => "RED FLAG",
        _ => "CHEQUERED FLAG"
    };

    private static string CautionTitle(TrackCondition condition) => condition switch
    {
        TrackCondition.DoubleYellow => "DOUBLE YELLOW",
        TrackCondition.FullCourseYellow => "FULL COURSE YELLOW",
        TrackCondition.SafetyCar => "SAFETY CAR",
        TrackCondition.VirtualSafetyCar or TrackCondition.VirtualSafetyCarEnding => "VIRTUAL SAFETY CAR",
        _ => "YELLOW FLAG"
    };

    /// <summary>
    /// The strip's colours are the flag's own: amber caution, red stopped, chequered finished. They are fixed rather
    /// than theme-derived because the taskbar is a surface the app does not control.
    /// </summary>
    private static readonly Color CautionColour = Color.FromRgb(0xE3, 0xB3, 0x41);
    private static readonly Color RedFlagColour = Color.FromRgb(0xF8, 0x51, 0x49);

    private static System.Windows.Media.Brush StripBrush(TaskbarFlagStrip state) => state switch
    {
        TaskbarFlagStrip.Caution => Frozen(CautionColour),
        TaskbarFlagStrip.RedFlag => Frozen(RedFlagColour),
        TaskbarFlagStrip.Finished => ChequeredFlag.Brush(4),
        _ => Brushes.Transparent
    };

    /// <summary>
    /// Caution letters are black or white by contrast against the amber. The red strip takes white letters, which
    /// read better on that red than the ratio predicts.
    /// </summary>
    private static System.Windows.Media.Brush LetterBrush(TaskbarFlagStrip state)
    {
        if (state == TaskbarFlagStrip.RedFlag) return Brushes.White;
        return TaskbarContrast.PrefersBlackText(CautionColour.R, CautionColour.G, CautionColour.B) ? Brushes.Black : Brushes.White;
    }

    private static SolidColorBrush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }

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

    private void PositionFlyout() => PositionFlyout(0);

    private void PositionFlyout(double offset)
    {
        if (_flyout.IsVisible && !_flyout.IsPinned) _host.PositionFlyout(_flyout, offset);
    }

    public void UpdateOverlaySchedule(IReadOnlyList<UpcomingSession> upcoming, DateTimeOffset now)
    {
        // A pinned panel holding a feed keeps it; only a panel pinned on the schedule follows the calendar.
        if (!_flyout.IsPinned || _flyout.PinnedFeed is not null) return;
        _overlaySnapshot = null;
        _flyout.ShowSchedule(upcoming, now);
    }

    private void ShowFlyout()
    {
        // Between sessions the flyout carries the schedule, which is the only thing there is to show.
        if (_snapshot is null)
        {
            // A pinned panel keeps whatever it is holding; the schedule belongs to the widget underneath.
            if (_flyout.IsPinned || !PrepareSchedule()) return;
            _overlaySnapshot = null;
        }
        // A flyout that was held on another feed would otherwise show it until the next rotation.
        else UpdateOverlaySnapshot(_snapshot);
        // Sized and placed before the reveal, so the slide starts from the correct resting point.
        _flyout.Show(); _flyout.UpdateLayout(); PositionFlyout(); _flyout.Reveal();
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

    protected override void OnClosed(EventArgs e)
    {
        IsClosed = true;
        _reattach.Stop();
        _flyout.ClosePermanently();
        base.OnClosed(e);
    }
}
