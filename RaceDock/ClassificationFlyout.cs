// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RaceDock.Core;
using Wpf.Ui.Appearance;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

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
    private readonly SymbolIcon _connectionGlyph = new();
    private readonly TextBlock _trackText = new();
    private readonly Border _trackBadge = new();
    private readonly Border _chequeredBadge = new() { Visibility = Visibility.Collapsed };
    private Grid _sessionHeader = null!;
    private StackPanel _badges = null!;
    private Grid _columnHeader = null!;
    private ScrollingTextBlock _gapHeader = null!;
    private ScrollingTextBlock _intervalHeader = null!;
    private ScrollingTextBlock _lapTimeHeader = null!;
    private Grid _sectorHeader = null!;
    private Grid _splitHeader = null!;
    private ScrollingTextBlock _tyreHeader = null!;
    private ScrollingTextBlock _energyHeader = null!;
    private ScrollingTextBlock _categoryHeader = null!;
    private ScrollingTextBlock _statusHeader = null!;
    private readonly DispatcherTimer _closeTimer;
    private readonly List<ClassificationRow> _standingRows = [];
    private readonly FeedSelection _selection = new();
    private readonly FlyoutFeedSwitcher _feedSwitcher = new();
    private IReadOnlyList<LiveFeedStatus> _liveFeeds = [];
    private bool _hasLiveFeedDirectory;
    private UserSettings _settings;
    private ChampionshipDisplaySettings _display;
    private TimingSnapshot? _snapshot;
    private readonly ScheduleView _schedule = new();
    private FrameworkElement _classification = null!;
    private double _driverColumnWidth = MinimumDriverColumn;
    private readonly TranslateTransform _slide = new();
    private readonly Border _panel;
    private readonly Border _body = new();
    private readonly FlyoutOverlayControls _overlayControls = new();
    private readonly FlyoutOverlayControls _scheduleOverlayControls = new();
    private bool _allowClose;
    public bool IsPinned { get; private set; }
    public event Action? PinChanged;
    public event Action? SettingsRequested;
    /// <summary>Null means the flyout follows the rotation again.</summary>
    public event Action<Championship?>? FeedSelectionChanged;
    private bool _blurred;
    private int _driverSignature;
    private int _visibleRowCount;
    private int _motionVersion;
    private IReadOnlyList<FlyoutColumn>? _headerOrder;

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
        // Deliberately not a layered window. WPF backs AllowsTransparency with UpdateLayeredWindow,
        // which does not survive alongside the accent blur below: the surface keeps compositing
        // while the content intermittently stops painting, leaving an empty pane. Transparency now
        // comes from the composition target instead, and the rounded corners from DWM.
        AllowsTransparency = false;
        Background = Brushes.Transparent;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        SetResourceReference(FontFamilyProperty, "MotorsportFontFamily");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);

        var root = new Border
        {
            // Matches the radius DWM rounds the window to, so the stroke follows the blurred edge.
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            BorderThickness = new Thickness(1)
        };
        _panel = root;
        // Opaque until the blur is confirmed. If the acrylic surface cannot be applied the panel
        // keeps painting this, so the classification is never left unreadable over the desktop.
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

        _classification = layout;
        _body.Child = layout;
        root.Child = _body;
        Content = root;
        _schedule.AddHeaderControl(_scheduleOverlayControls.Element);
        _scheduleOverlayControls.AttachDragHandle(_schedule.Header);
        _overlayControls.PinChanged += SetPinned;
        _scheduleOverlayControls.PinChanged += SetPinned;
        _feedSwitcher.SelectionChanged += OnFeedSelected;
        _overlayControls.DragRequested += DragOverlay;
        _scheduleOverlayControls.DragRequested += DragOverlay;
        _overlayControls.SettingsRequested += () => SettingsRequested?.Invoke();
        _scheduleOverlayControls.SettingsRequested += () => SettingsRequested?.Invoke();

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            if (IsPinned) return;
            var motionVersion = ++_motionVersion;
            SurfaceMotion.PlayClose(this, _slide, () =>
            {
                if (IsPinned || motionVersion != _motionVersion) return;
                if (!IsMouseOver) Hide();
                else CancelClose();
            }, fade: !_blurred);
        };
        MouseEnter += (_, _) =>
        {
            PointerEntered?.Invoke(this, EventArgs.Empty);
            CancelClose();
        };
        MouseLeave += (_, _) => ScheduleClose();
        // The blur covers the whole window, so a panel sliding inside it would leave the surface
        // behind. The window itself is moved instead, and this publishes each step of that travel.
        _slide.Changed += (_, _) => SlideTo?.Invoke(_slide.Y);
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                // A feed picked in the hover flyout lasts only for as long as it stays open.
                ResetTemporarySelection();
                return;
            }
            _scrollClock.Reset();
            // Updates are skipped while hidden, so the retained snapshot is drawn on the way back in.
            RefreshFromSnapshot();
        };
        ApplicationThemeManager.Changed += OnApplicationThemeChanged;
    }

    public void ApplySettings(UserSettings settings)
    {
        _settings = settings.Normalize();
        ApplySurface();
    }

    /// <summary>
    /// Matches the window surface to the settings. Before the handle exists this settles on the
    /// opaque fallback, and <see cref="OnSourceInitialized"/> runs it again once there is a window.
    /// </summary>
    private void ApplySurface()
    {
        _blurred = _settings.FlyoutAcrylicBlur && WindowBlur.TryApply(this, _settings.FlyoutBlurTint);
        if (!_blurred) WindowBlur.Disable(this);

        // The blur sits behind the window, so it only shows once the panel stops painting over it.
        if (_blurred) _panel.Background = Brushes.Transparent;
        else _panel.SetResourceReference(Border.BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
    }

    private void OnApplicationThemeChanged(ApplicationTheme theme, Color accent)
    {
        if (Dispatcher.CheckAccess()) ApplySurface();
        else Dispatcher.BeginInvoke(ApplySurface);
    }

    /// <summary>
    /// Moves the window to <c>rest + offset</c> device-independent pixels, towards the taskbar.
    /// Supplied by the owner, which is what knows where the flyout rests.
    /// </summary>
    public Action<double>? SlideTo { get; set; }

    /// <summary>The championship the flyout is held on, or null while it follows rotation or waits for one.</summary>
    public Championship? PinnedFeed => _selection.PinnedChampionship;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        NativeNoActivate.Set(hwnd);

        // Without a layered window the client area is cleared to an opaque colour unless the
        // composition target is told otherwise, which would paint over the blur.
        var source = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
        if (source?.CompositionTarget is not null)
            source.CompositionTarget.BackgroundColor = Colors.Transparent;

        // Corners are rounded by Windows now that the window is not layered. Where that is
        // unsupported the panel must square off to match, or its curve would expose the window
        // behind it at each corner.
        if (!WindowBlur.TryRoundCorners(this)) _panel.CornerRadius = new CornerRadius(0);

        ApplySurface();
    }

    /// <summary>
    /// Shows what is published next for the championships the user follows. This is what the flyout carries
    /// while nothing is running, in place of a classification it has no data for.
    /// </summary>
    public void ShowSchedule(IReadOnlyList<UpcomingSession> sessions, DateTimeOffset now)
    {
        _snapshot = null;
        if (!ReferenceEquals(_body.Child, _schedule.Element)) _body.Child = _schedule.Element;
        Width = ScheduleView.Width;
        _schedule.Update(sessions, now, TimeZoneInfo.Local);
        RefreshFeedBar();
    }

    /// <summary>
    /// Draws a classification. Returns false when the flyout is held on another feed, so a pinned panel is
    /// never pulled away by the source's rotation.
    /// </summary>
    public bool UpdateSnapshot(TimingSnapshot snapshot)
    {
        // A panel that was released from its feed (for example by a source-mode change) holds the next one
        // it is offered rather than falling back into rotation while still pinned.
        if (IsPinned && _selection.IsFollowingRotation) _selection.Freeze(snapshot.Championship);
        if (!_selection.TryAccept(snapshot)) return false;
        _snapshot = snapshot;
        // Timing takes the flyout back from the schedule as soon as a session reports in.
        _body.Child = _classification;
        RefreshFeedBar();
        // A closed flyout is redrawn when it opens, so keeping it current while hidden is pure cost.
        if (!IsVisible) return true;
        _display = _settings.DisplayFor(snapshot.Championship);
        _visibleRowCount = Math.Min(snapshot.Competitors.Count, _display.FlyoutCompetitorCount);
        UpdateDriverColumnWidth(snapshot);
        UpdateSplitHeader(snapshot.StageSplitPoints);
        ApplyDisplay();
        _title.Text = string.IsNullOrWhiteSpace(snapshot.Meeting) ? "Live classification" : ChampionshipDisplay.FullEvent(snapshot);
        if (snapshot.StageNumber is > 0 and var stage) _title.Text += $" · Stage {stage}";
        var rally = IsRallySnapshot(snapshot);
        var endurance = IsEnduranceSnapshot(snapshot);
        var lapTimeMode = ChampionshipDisplay.LapTimeModeFor(snapshot);
        _lapTimeHeader.Text = rally ? "TIME" : lapTimeMode == LapTimeMode.LastLap ? "LAST" : "BEST";
        _categoryHeader.Text = rally || endurance ? "CLASS" : PlayoffFormat(snapshot) ? "PO" : "LAP";
        _sessionDetails.Text = string.Join("  ·  ", new[]
        {
            snapshot.Session,
            snapshot.Circuit,
            SessionProgress(snapshot)
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        UpdateConnectionBadge(snapshot.ConnectionState);
        UpdateTrackBadge(snapshot.TrackCondition);
        UpdateChequeredBadge(snapshot.Lifecycle == SessionLifecycle.Ended);

        UpdateRows(snapshot, new ClassificationRowFormat(rally, endurance, lapTimeMode, PlayoffFormat(snapshot)));
        return true;
    }

    /// <summary>Publishes which championships the source can currently show and which of them have ended.</summary>
    public void SetLiveFeeds(IReadOnlyList<LiveFeedStatus> feeds)
    {
        _hasLiveFeedDirectory = true;
        // A feed that just ended changes only its retention flag, so compare the whole status, not the series.
        if (feeds.SequenceEqual(_liveFeeds)) return;
        _liveFeeds = feeds;
        RefreshFeedBar();
    }

    /// <summary>Forgets the directory while a source is being replaced, so a pin never flashes an ended tab.</summary>
    public void ResetLiveFeeds()
    {
        _hasLiveFeedDirectory = false;
        _liveFeeds = [];
        RefreshFeedBar();
    }

    /// <summary>Drops a held feed, for a source change the flyout should simply follow.</summary>
    public void ResetFeedSelection()
    {
        _selection.FollowRotation();
        RefreshFeedBar();
        FeedSelectionChanged?.Invoke(null);
    }

    private void OnFeedSelected(Championship? championship)
    {
        if (championship is { } feed) _selection.Freeze(feed);
        else _selection.FollowRotation();
        RefreshFeedBar();
        FeedSelectionChanged?.Invoke(_selection.IsFollowingRotation ? null : _selection.PinnedChampionship);
    }

    /// <summary>Restores rotation after a temporary pick in the hover flyout.</summary>
    private void ResetTemporarySelection()
    {
        if (IsPinned || _selection.IsFollowingRotation) return;
        _selection.FollowRotation();
        RefreshFeedBar();
        FeedSelectionChanged?.Invoke(null);
    }

    private void RefreshFeedBar()
    {
        var selected = _selection.IsFollowingRotation ? null : _selection.PinnedChampionship;
        var feeds = _liveFeeds.Select(feed => feed.Championship).ToList();
        var finished = _liveFeeds.Where(feed => feed.Retained).Select(feed => feed.Championship).ToHashSet();
        // The strip follows the panel, not only the directory: a pin outlives its feed's retention window, and the
        // source can drop a feed a moment before the next rotation replaces what is on screen. Either way the
        // classification the reader is looking at keeps its own tab.
        if (_hasLiveFeedDirectory)
            foreach (var shown in new[] { selected, OnScreenFeed() }.OfType<Championship>())
            {
                if (!feeds.Contains(shown)) feeds.Add(shown);
                finished.Add(shown);
            }
        _feedSwitcher.AllowAuto = !IsPinned;
        _feedSwitcher.SetFeeds(feeds, finished);
        _feedSwitcher.SetSelected(selected);
        _feedSwitcher.Element.Visibility = ShouldShowFeedBar() ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The championship whose classification the panel is drawing, independent of how it was chosen.</summary>
    private Championship? OnScreenFeed() =>
        _snapshot is { Championship: not Championship.Unknown } snapshot ? snapshot.Championship : null;

    private bool ShouldShowFeedBar()
    {
        if (ReferenceEquals(_body.Child, _schedule.Element)) return false;
        // The hover flyout offers a choice only when there is one; a pinned panel always shows what it is on.
        return _liveFeeds.Count >= 2 || IsPinned;
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
            row.ApplyDisplay(_display, widths, snapshot.StageSplitPoints);
            row.Update(standings[index], snapshot.Championship, _display, format);
        }
    }

    /// <summary>Redraws a retained classification when the flyout becomes visible again.</summary>
    private void RefreshFromSnapshot()
    {
        if (_snapshot is { } snapshot) UpdateSnapshot(snapshot);
        // TaskbarWindow prepares schedules immediately before Show. Rebuilding the list again from this
        // visibility callback would clear the newly visible tree and make cached logos appear to flicker.
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
            signature.Add(ChampionshipDisplay.FlyoutDriverName(snapshot.Championship, standing));
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
            var name = ChampionshipDisplay.FlyoutDriverName(snapshot.Championship, standing);
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
        _gapHeader.Visibility = _display.FlyoutShowGaps ? Visibility.Visible : Visibility.Collapsed;
        _intervalHeader.Visibility = _display.FlyoutShowIntervals ? Visibility.Visible : Visibility.Collapsed;
        _lapTimeHeader.Visibility = _display.FlyoutShowLapTime ? Visibility.Visible : Visibility.Collapsed;
        _sectorHeader.Visibility = _display.FlyoutShowSectorTimes ? Visibility.Visible : Visibility.Collapsed;
        _splitHeader.Visibility = _display.FlyoutShowSplitTimes ? Visibility.Visible : Visibility.Collapsed;
        _tyreHeader.Visibility = _display.FlyoutShowTyre ? Visibility.Visible : Visibility.Collapsed;
        _energyHeader.Visibility = _display.FlyoutShowEnergy ? Visibility.Visible : Visibility.Collapsed;
        _categoryHeader.Visibility = _display.FlyoutShowCategoryOrLap ? Visibility.Visible : Visibility.Collapsed;
        _statusHeader.Visibility = _display.FlyoutShowStatus ? Visibility.Visible : Visibility.Collapsed;
        ApplyHeaderOrder();
        ApplyColumnWidths(_columnHeader);
        // Every column keeps its full width, so the window grows to fit rather than truncating values.
        // Once the classification scrolls, the scrollbar needs its own space or it would clip the last column.
        var scrollbar = _visibleRowCount > MaximumVisibleRows ? SystemParameters.VerticalScrollBarWidth : 0;
        Width = ColumnWidths().Sum(column => column.Value) + ChromeWidth + scrollbar;
    }

    /// <summary>Shows the flyout and plays it into place.</summary>
    public void Reveal()
    {
        CancelClose();
        Show();
        if (!IsPinned) SurfaceMotion.PlayOpen(this, _slide, fade: !_blurred);
    }

    private void SetPinned(bool pinned)
    {
        if (IsPinned == pinned) return;
        // Change the anchoring mode before cancelling motion: cancellation also fires SlideTo.
        IsPinned = pinned;
        CancelClose();
        if (pinned && _selection.IsFollowingRotation) _selection.Freeze(_snapshot?.Championship);
        else if (!pinned) _selection.FollowRotation();
        RefreshFeedBar();
        _overlayControls.SetPinned(pinned);
        _scheduleOverlayControls.SetPinned(pinned);
        PinChanged?.Invoke();
        FeedSelectionChanged?.Invoke(_selection.IsFollowingRotation ? null : _selection.PinnedChampionship);
        if (!pinned && !IsMouseOver) ScheduleClose();
    }

    private void DragOverlay()
    {
        if (IsPinned && System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void Dismiss()
    {
        CancelClose();
        Hide();
        SetPinned(false);
    }

    public void ClosePermanently()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Dismiss();
        }
        base.OnClosing(e);
    }

    public void ScheduleClose()
    {
        // Closing a flyout that is not open would animate a hidden window, and the motion drives the
        // native repositioning that makes it visible again.
        if (!IsVisible || IsPinned) return;
        _closeTimer.Stop();
        _closeTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _closeTimer.Stop();
        ApplicationThemeManager.Changed -= OnApplicationThemeChanged;
        base.OnClosed(e);
    }

    public void CancelClose()
    {
        _motionVersion++;
        _closeTimer.Stop();
        SurfaceMotion.CancelClose(this, _slide);
    }

    private static bool IsRallySnapshot(TimingSnapshot snapshot) => snapshot.Championship == Championship.WorldRallyChampionship || snapshot.Meeting.Contains("Rally", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SS", StringComparison.OrdinalIgnoreCase) || snapshot.Session.StartsWith("SHAKEDOWN", StringComparison.OrdinalIgnoreCase);
    private static bool IsEnduranceSnapshot(TimingSnapshot snapshot) => snapshot.Championship is
        Championship.GtWorldChallengeEurope or Championship.WorldEnduranceChampionship
            or Championship.EuropeanLeMansSeries or Championship.MichelinLeMansCup or Championship.Imsa;

    /// <summary>
    /// Only the Cup feed publishes playoff eligibility, so the class-or-lap column reports it there and
    /// keeps reporting a class or lap count everywhere else.
    /// </summary>
    private static bool PlayoffFormat(TimingSnapshot snapshot) => snapshot.Championship is
        Championship.NascarCup or Championship.NascarXfinity or Championship.NascarTrucks;

    private static string SessionProgress(TimingSnapshot snapshot)
    {
        if (IsRallySnapshot(snapshot)) return snapshot.Session;
        if (!string.IsNullOrWhiteSpace(snapshot.TimeRemaining))
        {
            var endurance = IsEnduranceSnapshot(snapshot);
            if (IsTimedSession(snapshot))
            {
                if (endurance && snapshot.CurrentLap > 0)
                    return $"Lap {snapshot.CurrentLap} · {snapshot.TimeRemaining} remaining";
                return $"Time remaining {snapshot.TimeRemaining}";
            }
            if (snapshot.TotalLaps is null && snapshot.CurrentLap > 0)
                return $"Lap {snapshot.CurrentLap} · {snapshot.TimeRemaining} remaining";
        }
        if (snapshot.TotalLaps is > 0) return $"Lap {snapshot.CurrentLap}/{snapshot.TotalLaps}";
        // A timed session without a lap target or a countdown (NASCAR practice and qualifying publish neither)
        // has no progress value to report here, so the line carries the session and circuit alone.
        if (IsTimedSession(snapshot) || snapshot.CurrentLap <= 0) return "";
        return $"Lap {snapshot.CurrentLap}";
    }

    private static bool IsTimedSession(TimingSnapshot snapshot) =>
        IsEnduranceSnapshot(snapshot) ||
        snapshot.Session.Contains("Practice", StringComparison.OrdinalIgnoreCase) ||
        snapshot.Session.Contains("Qualifying", StringComparison.OrdinalIgnoreCase);

    private Grid CreateSessionHeader()
    {
        var header = new Grid { Margin = new Thickness(2, 1, 2, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Live-series tabs share the top row with the settings and pin buttons: tabs left, controls right.
        _feedSwitcher.Element.Visibility = Visibility.Collapsed;
        _feedSwitcher.Element.Margin = new Thickness(0, 0, 8, 0);
        _feedSwitcher.Element.HorizontalAlignment = HorizontalAlignment.Left;
        _feedSwitcher.Element.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(_feedSwitcher.Element, 0);
        Grid.SetColumn(_feedSwitcher.Element, 0);
        header.Children.Add(_feedSwitcher.Element);

        _overlayControls.Element.Margin = new Thickness(0, 1, 0, 0);
        _overlayControls.Element.HorizontalAlignment = HorizontalAlignment.Right;
        _overlayControls.Element.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetRow(_overlayControls.Element, 0);
        Grid.SetColumn(_overlayControls.Element, 1);
        header.Children.Add(_overlayControls.Element);

        var copy = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 4, 0, 0)
        };
        Grid.SetRow(copy, 1);
        _overlayControls.AttachDragHandle(copy);
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
            // Centred on the title and session line, clear of the settings and pin buttons.
            VerticalAlignment = VerticalAlignment.Center
        };
        ConfigureBadge(_trackBadge, _trackText);
        _connectionGlyph.FontSize = 15;
        _connectionGlyph.VerticalAlignment = VerticalAlignment.Center;
        ConfigureChequeredBadge();
        _trackBadge.Margin = new Thickness(0, 0, 6, 0);
        _badges.Children.Add(_chequeredBadge);
        _badges.Children.Add(_trackBadge);
        _badges.Children.Add(_connectionGlyph);
        Grid.SetColumn(_badges, 1);
        Grid.SetRow(_badges, 1);
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
        _sectorHeader = CreateSectorHeader(5);
        header.Children.Add(_sectorHeader);
        _splitHeader = new Grid();
        Grid.SetColumn(_splitHeader, 6);
        header.Children.Add(_splitHeader);
        _tyreHeader = AddHeaderLabel(header, "TIRES", 7, TextAlignment.Center);
        _categoryHeader = AddHeaderLabel(header, "LAP", 8, TextAlignment.Center);
        _statusHeader = AddHeaderLabel(header, "STATUS", 9, TextAlignment.Center);
        _energyHeader = AddHeaderLabel(header, "ENERGY", 10, TextAlignment.Center);
        return header;
    }

    /// <summary>Shows a connection problem as a single tinted glyph. A healthy feed earns no badge.</summary>
    private void UpdateConnectionBadge(ConnectionState state)
    {
        if (state is ConnectionState.Connected)
        {
            _connectionGlyph.Visibility = Visibility.Collapsed;
            return;
        }
        _connectionGlyph.Visibility = Visibility.Visible;
        (_connectionGlyph.Symbol, var tint) = state switch
        {
            ConnectionState.Connecting => (SymbolRegular.ArrowSync20, Color.FromRgb(96, 165, 224)),
            ConnectionState.Stale => (SymbolRegular.Warning24, Color.FromRgb(232, 174, 58)),
            ConnectionState.Faulted => (SymbolRegular.ErrorCircle24, Color.FromRgb(226, 82, 104)),
            _ => (SymbolRegular.PlugDisconnected24, Color.FromRgb(158, 163, 173))
        };
        _connectionGlyph.Foreground = new SolidColorBrush(tint);
        _connectionGlyph.ToolTip = state.ToString();
    }

    /// <summary>A chequered strip, sized and rounded like the status badges it sits beside.</summary>
    private void ConfigureChequeredBadge()
    {
        _chequeredBadge.Width = 72;
        _chequeredBadge.Height = 24;
        _chequeredBadge.CornerRadius = new CornerRadius(7);
        _chequeredBadge.VerticalAlignment = VerticalAlignment.Center;
        _chequeredBadge.Background = ChequeredFlag.Brush(4);
        _chequeredBadge.ToolTip = "Chequered flag";
        System.Windows.Automation.AutomationProperties.SetName(_chequeredBadge, "Chequered flag");
    }

    private void UpdateChequeredBadge(bool finished) =>
        _chequeredBadge.Visibility = finished ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateTrackBadge(TrackCondition condition)
    {
        if (condition is TrackCondition.AllClear)
        {
            _trackBadge.Visibility = Visibility.Collapsed;
            return;
        }
        _trackBadge.Visibility = Visibility.Visible;
        (_trackText.Text, _trackBadge.Background) = condition switch
        {
            TrackCondition.Yellow => ("YELLOW", BrushFrom(218, 164, 22, 0.36)),
            TrackCondition.FullCourseYellow => ("FULL COURSE YELLOW", BrushFrom(218, 164, 22, 0.36)),
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
        badge.Height = 24;
        badge.Padding = new Thickness(8, 0, 8, 0);
        badge.VerticalAlignment = VerticalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextAlignment = TextAlignment.Center;
        text.Foreground = Brushes.White;
        text.FontSize = 9.5;
        text.FontWeight = FontWeights.SemiBold;
        badge.Child = text;
    }



    private Grid CreateColumns()
    {
        var grid = new Grid();
        for (var index = 0; index < 2 + ChampionshipDisplaySettings.DefaultColumnOrder.Count; index++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        ApplyColumnWidths(grid);
        return grid;
    }

    private void ApplyColumnWidths(Grid grid)
    {
        var widths = ColumnWidths();
        for (var index = 0; index < widths.Count; index++) grid.ColumnDefinitions[index].Width = widths[index];
    }

    /// <summary>Moves the header labels to match the configured order, skipped unless the order changed.</summary>
    private void ApplyHeaderOrder()
    {
        var order = _display.FlyoutColumnOrder;
        if (ReferenceEquals(order, _headerOrder)) return;
        _headerOrder = order;
        for (var index = 0; index < order.Count; index++) Grid.SetColumn(HeaderFor(order[index]), index + 2);
    }

    private IReadOnlyList<GridLength> ColumnWidths()
    {
        var widths = new List<GridLength>(2 + _display.FlyoutColumnOrder.Count)
        {
            new(42),
            new(_driverColumnWidth)
        };
        foreach (var column in _display.FlyoutColumnOrder)
            widths.Add(IsColumnVisible(column) ? new GridLength(ColumnWidth(column)) : new GridLength(0));
        return widths;
    }

    private bool IsColumnVisible(FlyoutColumn column) => column switch
    {
        FlyoutColumn.Gap => _display.FlyoutShowGaps,
        FlyoutColumn.Interval => _display.FlyoutShowIntervals,
        FlyoutColumn.LapTime => _display.FlyoutShowLapTime,
        FlyoutColumn.Sectors => _display.FlyoutShowSectorTimes,
        FlyoutColumn.Splits => _display.FlyoutShowSplitTimes,
        FlyoutColumn.Energy => _display.FlyoutShowEnergy,
        FlyoutColumn.Tyre => _display.FlyoutShowTyre,
        FlyoutColumn.Category => _display.FlyoutShowCategoryOrLap,
        _ => _display.FlyoutShowStatus
    };

    private double ColumnWidth(FlyoutColumn column) => column switch
    {
        FlyoutColumn.Gap or FlyoutColumn.Interval or FlyoutColumn.LapTime => 78,
        FlyoutColumn.Sectors => 210,
        FlyoutColumn.Splits => Math.Max(70, (_snapshot?.StageSplitPoints.Count ?? 0) * 70),
        FlyoutColumn.Energy => 72,
        FlyoutColumn.Tyre => 60,
        FlyoutColumn.Category => 58,
        _ => 64
    };

    private FrameworkElement HeaderFor(FlyoutColumn column) => column switch
    {
        FlyoutColumn.Gap => _gapHeader,
        FlyoutColumn.Interval => _intervalHeader,
        FlyoutColumn.LapTime => _lapTimeHeader,
        FlyoutColumn.Sectors => _sectorHeader,
        FlyoutColumn.Splits => _splitHeader,
        FlyoutColumn.Energy => _energyHeader,
        FlyoutColumn.Tyre => _tyreHeader,
        FlyoutColumn.Category => _categoryHeader,
        _ => _statusHeader
    };

    private void UpdateSplitHeader(IReadOnlyList<RallySplitPoint> points)
    {
        if (_splitHeader.Children.Count == points.Count &&
            _splitHeader.Children.OfType<FrameworkElement>().Select(item => item.Tag).SequenceEqual(points.Select(point => (object)point.SplitPointId)))
            return;

        _splitHeader.Children.Clear();
        _splitHeader.ColumnDefinitions.Clear();
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            _splitHeader.ColumnDefinitions.Add(new ColumnDefinition());
            var label = AddHeaderLabel(_splitHeader, $"S{point.Number}", index, TextAlignment.Center);
            label.Tag = point.SplitPointId;
            label.ToolTip = $"Split {point.Number} · {point.DistanceKm:0.##} km";
        }
    }

    private Grid CreateSectorHeader(int column)
    {
        var header = new Grid();
        Grid.SetColumn(header, column);
        foreach (var label in new[] { "S1", "S2", "S3" })
        {
            header.ColumnDefinitions.Add(new ColumnDefinition());
            AddHeaderLabel(header, label, header.ColumnDefinitions.Count - 1, TextAlignment.Center);
        }
        return header;
    }

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
