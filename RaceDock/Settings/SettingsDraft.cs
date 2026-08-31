// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RaceDock.Core;

namespace RaceDock.App.Settings;

internal enum ChampionshipStatusKind { Idle, PreSession, Live, RedFlag, Ended }

internal sealed class ChampionshipStatusItem : INotifyPropertyChanged
{
    private bool _isChampionshipEnabled;
    private bool _isVisible = true;
    private bool _hasTiming;
    private ChampionshipStatusKind _statusKind = ChampionshipStatusKind.Idle;
    private string _eventName = "No active session";
    private string _sessionName = "";
    private string _progressText = "";
    private string _upcomingEvent = "";
    private string _upcomingDetail = "";
    private string _upcomingSummary = "";

    public ChampionshipStatusItem(Championship championship, string displayName, string iconSource, string description)
    {
        Championship = championship;
        DisplayName = displayName;
        IconSource = iconSource;
        Description = description;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the user toggles visibility; the draft applies the change so hiding also disables the championship.</summary>
    public event EventHandler<bool>? VisibilityChangeRequested;

    public Championship Championship { get; }
    public string DisplayName { get; }
    public string IconSource { get; }
    public string Description { get; }
    public string NavigationKey => $"Championship:{Championship}";
    public bool IsChampionshipEnabled
    {
        get => _isChampionshipEnabled;
        private set
        {
            if (_isChampionshipEnabled == value) return;
            _isChampionshipEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChampionshipEnabled)));
        }
    }
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            VisibilityChangeRequested?.Invoke(this, value);
            // Nothing applied the request, so push the stored value back to the bound toggle.
            if (_isVisible != value) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }

    public bool HasTiming => _hasTiming;
    public ChampionshipStatusKind StatusKind => _statusKind;
    public string EventName => _eventName;
    public string SessionName => _sessionName;
    public string ProgressText => _progressText;

    public string StatusLabel => _statusKind switch
    {
        ChampionshipStatusKind.Live => "LIVE",
        ChampionshipStatusKind.PreSession => "UPCOMING",
        ChampionshipStatusKind.RedFlag => "RED FLAG",
        ChampionshipStatusKind.Ended => "FINISHED",
        _ => ""
    };

    /// <summary>The next published event, shown only while nothing is on track.</summary>
    public bool HasUpcoming => !_hasTiming && _upcomingEvent.Length > 0;
    public string UpcomingEvent => _upcomingEvent;
    public string UpcomingDetail => _upcomingDetail;
    public string UpcomingSummary => _upcomingSummary;

    public string StatusTooltip
    {
        get
        {
            if (_hasTiming) return string.Join(" \u00b7 ", new[] { _eventName, _sessionName, _progressText }.Where(part => !string.IsNullOrWhiteSpace(part)));
            return HasUpcoming ? $"{_upcomingEvent} \u00b7 {_upcomingDetail}" : _eventName;
        }
    }

    public void SetUpcoming(UpcomingSession? session, DateTimeOffset now)
    {
        if (session is null)
        {
            SetStatus(ref _upcomingEvent, "", nameof(UpcomingEvent));
            SetStatus(ref _upcomingDetail, "", nameof(UpcomingDetail));
            SetStatus(ref _upcomingSummary, "", nameof(UpcomingSummary));
        }
        else
        {
            var local = session.StartUtc.ToLocalTime();
            var lead = ChampionshipCalendars.DescribeTimeUntil(session.StartUtc, now);
            // Within a day the lead time says more than a calendar date, so it replaces it.
            var imminent = session.StartUtc - now < TimeSpan.FromDays(1);
            // Meetings without a published session time carry a date only, so a clock time would be invented.
            var when = imminent ? lead
                : session.StartUtc.TimeOfDay == TimeSpan.Zero ? local.ToString("ddd d MMM")
                : local.ToString("ddd d MMM, HH:mm");
            var detail = new[] { session.SessionName, when, imminent ? null : lead }
                .Where(part => !string.IsNullOrWhiteSpace(part));
            var eventName = ChampionshipDisplay.ShortEventName(session.Championship, session.EventName);
            SetStatus(ref _upcomingEvent, eventName, nameof(UpcomingEvent));
            SetStatus(ref _upcomingDetail, string.Join(" \u00b7 ", detail), nameof(UpcomingDetail));
            SetStatus(ref _upcomingSummary, $"{when} \u00b7 {eventName}", nameof(UpcomingSummary));
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasUpcoming)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusTooltip)));
    }

    public void Update(bool enabled) => IsChampionshipEnabled = enabled;

    public void SetVisible(bool visible)
    {
        if (_isVisible == visible) return;
        _isVisible = visible;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
    }

    public void UpdateSnapshot(TimingSnapshot? snapshot)
    {
        SetTimingAvailability(snapshot is not null);
        var eventName = snapshot is null || string.IsNullOrWhiteSpace(snapshot.Meeting)
            ? "No active session"
            : ChampionshipDisplay.ShortEventName(snapshot.Championship, snapshot.Meeting);
        var sessionName = snapshot is null || string.IsNullOrWhiteSpace(snapshot.Session) ? "" : snapshot.Session;
        SetStatusKind(ClassifyStatus(snapshot));
        SetStatus(ref _eventName, eventName, nameof(EventName));
        SetStatus(ref _sessionName, sessionName, nameof(SessionName));
        SetStatus(ref _progressText, snapshot is null ? "" : FormatProgress(snapshot), nameof(ProgressText));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusTooltip)));
    }

    private void SetTimingAvailability(bool hasTiming)
    {
        if (_hasTiming == hasTiming) return;
        _hasTiming = hasTiming;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasTiming)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasUpcoming)));
    }

    private void SetStatusKind(ChampionshipStatusKind statusKind)
    {
        if (_statusKind == statusKind) return;
        _statusKind = statusKind;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusKind)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusLabel)));
    }

    private static ChampionshipStatusKind ClassifyStatus(TimingSnapshot? snapshot)
    {
        if (snapshot is null) return ChampionshipStatusKind.Idle;
        if (snapshot.TrackCondition == TrackCondition.RedFlag) return ChampionshipStatusKind.RedFlag;
        return snapshot.Lifecycle switch
        {
            SessionLifecycle.PreSession => ChampionshipStatusKind.PreSession,
            SessionLifecycle.Live => ChampionshipStatusKind.Live,
            SessionLifecycle.Ended => ChampionshipStatusKind.Ended,
            _ => ChampionshipStatusKind.Idle
        };
    }

    private static string FormatProgress(TimingSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.TimeRemaining)) return $"Time {snapshot.TimeRemaining}";
        if (snapshot.TotalLaps is > 0) return $"Lap {snapshot.CurrentLap}/{snapshot.TotalLaps}";
        if (snapshot.CurrentLap > 0) return $"Lap {snapshot.CurrentLap}";
        return snapshot.Lifecycle switch
        {
            SessionLifecycle.PreSession => "Pre-session",
            SessionLifecycle.Live => "Live",
            SessionLifecycle.Ended => "Completed",
            _ => ""
        };
    }

    private void SetStatus(ref string field, string value, string propertyName)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class SettingsDraft : INotifyPropertyChanged
{
    private const string ChampionshipLogoRoot = "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/";
    private readonly IReadOnlyDictionary<Championship, ChampionshipStatusItem> _statusByChampionship;
    private readonly ObservableCollection<FlyoutColumnItem> _flyoutColumns = [];
    private ChampionshipDisplayProfiles _profiles = new();
    private bool _syncingProfile;
    private bool _enableFormula1;
    private bool _enableFormula2;
    private bool _enableFormula3;
    private bool _enableMotoGp;
    private bool _enableMoto2;
    private bool _enableMoto3;
    private bool _enableGtWorldChallengeEurope;
    private bool _enableWrc;
    private bool _enableImsa;
    private bool _enableFormulaE;
    private bool _enableDtm;
    private bool _enableIndyCar;
    private bool _autoStartOnWindowsBoot;
    private bool _interfaceAnimations;
    private bool _flyoutAcrylicBlur;
    private int _flyoutBlurTint;
    private int _rotationSeconds;
    private int _completedSessionRetentionMinutes;
    private TaskbarWidgetPosition _widgetPosition;
    private Championship _selectedChampionship;
    private int _taskbarCompetitorCount;
    private bool _taskbarUseManufacturerLogos;
    private int _flyoutCompetitorCount;
    private bool _flyoutShowTeamNames;
    private bool _flyoutUseManufacturerLogos;
    private bool _flyoutShowPositionColors;
    private bool _flyoutShowGaps;
    private bool _flyoutShowIntervals;
    private bool _flyoutShowLapTime;
    private bool _flyoutShowTyre;
    private bool _flyoutShowCategoryOrLap;
    private bool _flyoutShowStatus;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsDraft()
    {
        ChampionshipStatuses =
        [
            Status(Championship.Formula1, "Formula 1", "f1.png", "World championship single-seaters"),
            Status(Championship.Formula2, "Formula 2", "f2.png", "Junior single-seater feeder series"),
            Status(Championship.Formula3, "Formula 3", "f3.png", "Junior single-seater feeder series"),
            Status(Championship.FormulaE, "Formula E", "formulae.png", "All-electric street racing"),
            Status(Championship.Dtm, "DTM", "dtm.png", "German GT3 touring car series"),
            Status(Championship.IndyCar, "INDYCAR", "indycar.png", "North American open-wheel racing"),
            Status(Championship.MotoGp, "MotoGP", "motogp.png", "Premier class motorcycle racing"),
            Status(Championship.Moto2, "Moto2", "moto2-sidebar.png", "Intermediate Grand Prix motorcycle class"),
            Status(Championship.Moto3, "Moto3", "moto3-sidebar.png", "Entry Grand Prix motorcycle class"),
            Status(Championship.GtWorldChallengeEurope, "GT World Challenge Europe", "gtwc.png", "European GT3 endurance and sprint racing"),
            Status(Championship.WorldRallyChampionship, "World Rally Championship", "wrc.png", "World championship rallying"),
            Status(Championship.Imsa, "IMSA WeatherTech", "imsa.png", "North American sportscar endurance racing")
        ];
        _statusByChampionship = ChampionshipStatuses.ToDictionary(status => status.Championship);
        DashboardChampionshipStatuses = [];
        foreach (var status in ChampionshipStatuses)
            status.VisibilityChangeRequested += (_, visible) => SetChampionshipVisible(status, visible);
    }

    public IReadOnlyList<ChampionshipStatusItem> ChampionshipStatuses { get; }
    public ObservableCollection<ChampionshipStatusItem> DashboardChampionshipStatuses { get; }
    /// <summary>Attaches the published schedule to each championship; a series without a date simply keeps none.</summary>
    public void SetUpcomingSessions(IReadOnlyList<UpcomingSession> sessions, DateTimeOffset now)
    {
        foreach (var status in ChampionshipStatuses)
            status.SetUpcoming(sessions.FirstOrDefault(session => session.Championship == status.Championship), now);
    }

    public void UpdateTimingStatus(TimingSnapshot snapshot)
    {
        if (snapshot.Championship == Championship.Unknown || !_statusByChampionship.TryGetValue(snapshot.Championship, out var status)) return;
        status.UpdateSnapshot(snapshot.Lifecycle == SessionLifecycle.OffSession ? null : snapshot);
        RefreshDashboardChampionshipStatuses();
    }

    public void ClearTimingStatuses()
    {
        foreach (var status in ChampionshipStatuses) status.UpdateSnapshot(null);
        RefreshDashboardChampionshipStatuses();
    }

    public bool EnableFormula1 { get => _enableFormula1; set => SetStream(Championship.Formula1, ref _enableFormula1, value); }
    public bool EnableFormula2 { get => _enableFormula2; set => SetStream(Championship.Formula2, ref _enableFormula2, value); }
    public bool EnableFormula3 { get => _enableFormula3; set => SetStream(Championship.Formula3, ref _enableFormula3, value); }
    public bool EnableMotoGp { get => _enableMotoGp; set => SetStream(Championship.MotoGp, ref _enableMotoGp, value); }
    public bool EnableMoto2 { get => _enableMoto2; set => SetStream(Championship.Moto2, ref _enableMoto2, value); }
    public bool EnableMoto3 { get => _enableMoto3; set => SetStream(Championship.Moto3, ref _enableMoto3, value); }
    public bool EnableGtWorldChallengeEurope { get => _enableGtWorldChallengeEurope; set => SetStream(Championship.GtWorldChallengeEurope, ref _enableGtWorldChallengeEurope, value); }
    public bool EnableWrc { get => _enableWrc; set => SetStream(Championship.WorldRallyChampionship, ref _enableWrc, value); }
    public bool EnableImsa { get => _enableImsa; set => SetStream(Championship.Imsa, ref _enableImsa, value); }
    public bool EnableFormulaE { get => _enableFormulaE; set => SetStream(Championship.FormulaE, ref _enableFormulaE, value); }
    public bool EnableDtm { get => _enableDtm; set => SetStream(Championship.Dtm, ref _enableDtm, value); }
    public bool EnableIndyCar { get => _enableIndyCar; set => SetStream(Championship.IndyCar, ref _enableIndyCar, value); }
    public bool AutoStartOnWindowsBoot { get => _autoStartOnWindowsBoot; set => Set(ref _autoStartOnWindowsBoot, value); }
    public bool InterfaceAnimations { get => _interfaceAnimations; set => Set(ref _interfaceAnimations, value); }
    public bool FlyoutAcrylicBlur { get => _flyoutAcrylicBlur; set => Set(ref _flyoutAcrylicBlur, value); }
    public int FlyoutBlurTint { get => _flyoutBlurTint; set => Set(ref _flyoutBlurTint, value); }
    public int RotationSeconds { get => _rotationSeconds; set => Set(ref _rotationSeconds, value); }
    public int CompletedSessionRetentionMinutes { get => _completedSessionRetentionMinutes; set => Set(ref _completedSessionRetentionMinutes, value); }
    public TaskbarWidgetPosition WidgetPosition { get => _widgetPosition; set => Set(ref _widgetPosition, value); }

    public Championship SelectedChampionship
    {
        get => _selectedChampionship;
        set
        {
            if (_selectedChampionship == value) return;
            if (!_syncingProfile) StoreCurrentProfile();
            Set(ref _selectedChampionship, value);
            OnPropertyChanged(nameof(SelectedChampionshipPresentation));
            OnPropertyChanged(nameof(SelectedChampionshipPageLogo));
            OnPropertyChanged(nameof(SelectedChampionshipStatus));
            OnPropertyChanged(nameof(SelectedChampionshipResetLabel));
            OnPropertyChanged(nameof(SelectedChampionshipEnabled));
            if (_syncingProfile) return;
            _syncingProfile = true;
            LoadProfile(_profiles.For(value));
            _syncingProfile = false;
        }
    }

    public ChampionshipSettingsPresentation SelectedChampionshipPresentation =>
        ChampionshipSettingsPresentation.For(SelectedChampionship);

    public ChampionshipStatusItem SelectedChampionshipStatus =>
        _statusByChampionship.TryGetValue(SelectedChampionship, out var status) ? status : _statusByChampionship[Championship.Formula1];

    public string SelectedChampionshipResetLabel => $"Reset {SelectedChampionshipStatus.DisplayName} settings";

    public string SelectedChampionshipPageLogo => SelectedChampionship switch
    {
        Championship.Formula1 => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/f1-wide.png",
        Championship.Formula2 => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/f2-wide.png",
        Championship.Formula3 => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/f3-wide.png",
        Championship.MotoGp => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/motogp-wide.png",
        Championship.Moto2 => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/moto2.png",
        Championship.Moto3 => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/moto3.png",
        Championship.GtWorldChallengeEurope => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/gtwc-wide.png",
        Championship.WorldRallyChampionship => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/wrc-wide.png",
        Championship.Imsa => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/imsa-wide.png",
        Championship.FormulaE => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/formulae-full.png",
        Championship.Dtm => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/dtm-wide.png",
        Championship.IndyCar => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/indycar-wide.png",
        _ => "pack://application:,,,/RaceDock;component/Assets/ChampionshipLogos/f1.png"
    };

    public bool SelectedChampionshipEnabled
    {
        get => SelectedChampionship switch
        {
            Championship.Formula1 => EnableFormula1,
            Championship.Formula2 => EnableFormula2,
            Championship.Formula3 => EnableFormula3,
            Championship.MotoGp => EnableMotoGp,
            Championship.Moto2 => EnableMoto2,
            Championship.Moto3 => EnableMoto3,
            Championship.GtWorldChallengeEurope => EnableGtWorldChallengeEurope,
            Championship.WorldRallyChampionship => EnableWrc,
            Championship.Imsa => EnableImsa,
            Championship.FormulaE => EnableFormulaE,
            Championship.Dtm => EnableDtm,
            Championship.IndyCar => EnableIndyCar,
            _ => false
        };
        set => SetChampionshipEnabled(SelectedChampionship, value);
    }

    private void SetChampionshipEnabled(Championship championship, bool value)
    {
        switch (championship)
        {
            case Championship.Formula1: EnableFormula1 = value; break;
            case Championship.Formula2: EnableFormula2 = value; break;
            case Championship.Formula3: EnableFormula3 = value; break;
            case Championship.MotoGp: EnableMotoGp = value; break;
            case Championship.Moto2: EnableMoto2 = value; break;
            case Championship.Moto3: EnableMoto3 = value; break;
            case Championship.GtWorldChallengeEurope: EnableGtWorldChallengeEurope = value; break;
            case Championship.WorldRallyChampionship: EnableWrc = value; break;
            case Championship.Imsa: EnableImsa = value; break;
            case Championship.FormulaE: EnableFormulaE = value; break;
            case Championship.Dtm: EnableDtm = value; break;
            case Championship.IndyCar: EnableIndyCar = value; break;
        }
    }

    public int TaskbarCompetitorCount { get => _taskbarCompetitorCount; set => Set(ref _taskbarCompetitorCount, value); }
    public bool TaskbarUseManufacturerLogos { get => _taskbarUseManufacturerLogos; set => Set(ref _taskbarUseManufacturerLogos, value); }

    public int FlyoutCompetitorCount { get => _flyoutCompetitorCount; set => Set(ref _flyoutCompetitorCount, value); }
    public bool FlyoutShowTeamNames { get => _flyoutShowTeamNames; set => Set(ref _flyoutShowTeamNames, value); }
    public bool FlyoutUseManufacturerLogos { get => _flyoutUseManufacturerLogos; set => Set(ref _flyoutUseManufacturerLogos, value); }
    public bool FlyoutShowPositionColors { get => _flyoutShowPositionColors; set => Set(ref _flyoutShowPositionColors, value); }
    public bool FlyoutShowGaps { get => _flyoutShowGaps; set => Set(ref _flyoutShowGaps, value); }
    public bool FlyoutShowIntervals { get => _flyoutShowIntervals; set => Set(ref _flyoutShowIntervals, value); }
    public bool FlyoutShowLapTime { get => _flyoutShowLapTime; set => Set(ref _flyoutShowLapTime, value); }
    public bool FlyoutShowTyre { get => _flyoutShowTyre; set => Set(ref _flyoutShowTyre, value); }
    public bool FlyoutShowCategoryOrLap { get => _flyoutShowCategoryOrLap; set => Set(ref _flyoutShowCategoryOrLap, value); }
    public bool FlyoutShowStatus { get => _flyoutShowStatus; set => Set(ref _flyoutShowStatus, value); }

    /// <summary>The movable classification columns of the championship on screen, in the order they appear.</summary>
    public ObservableCollection<FlyoutColumnItem> FlyoutColumns => _flyoutColumns;

    internal bool IsColumnVisible(FlyoutColumn column) => column switch
    {
        FlyoutColumn.Gap => FlyoutShowGaps,
        FlyoutColumn.Interval => FlyoutShowIntervals,
        FlyoutColumn.LapTime => FlyoutShowLapTime,
        FlyoutColumn.Tyre => FlyoutShowTyre,
        FlyoutColumn.Category => FlyoutShowCategoryOrLap,
        _ => FlyoutShowStatus
    };

    internal void SetColumnVisible(FlyoutColumn column, bool value)
    {
        switch (column)
        {
            case FlyoutColumn.Gap: FlyoutShowGaps = value; break;
            case FlyoutColumn.Interval: FlyoutShowIntervals = value; break;
            case FlyoutColumn.LapTime: FlyoutShowLapTime = value; break;
            case FlyoutColumn.Tyre: FlyoutShowTyre = value; break;
            case FlyoutColumn.Category: FlyoutShowCategoryOrLap = value; break;
            default: FlyoutShowStatus = value; break;
        }
    }

    /// <summary>Moves a column one place towards the front or the back of the classification.</summary>
    public void MoveColumn(FlyoutColumnItem item, int offset)
    {
        var index = _flyoutColumns.IndexOf(item);
        if (index < 0) return;
        // Unsupported columns are hidden from the page, so a move has to step over them to land somewhere visible.
        var target = index + offset;
        while (target >= 0 && target < _flyoutColumns.Count && !_flyoutColumns[target].IsSupported)
            target += offset;
        if (target < 0 || target >= _flyoutColumns.Count) return;
        _flyoutColumns.Move(index, target);
        UpdateColumnMoveState();
        OnPropertyChanged(nameof(FlyoutColumns));
    }

    private void RebuildFlyoutColumns(IReadOnlyList<FlyoutColumn> order)
    {
        var presentation = SelectedChampionshipPresentation;
        _flyoutColumns.Clear();
        foreach (var column in order)
            _flyoutColumns.Add(CreateColumnItem(column, presentation));
        UpdateColumnMoveState();
        OnPropertyChanged(nameof(FlyoutColumns));
    }

    private FlyoutColumnItem CreateColumnItem(FlyoutColumn column, ChampionshipSettingsPresentation presentation) => column switch
    {
        FlyoutColumn.Gap => new(this, column, "Leader gap", presentation.LeaderGapDescription, true),
        FlyoutColumn.Interval => new(this, column, "Interval", presentation.IntervalDescription, true),
        FlyoutColumn.LapTime => new(this, column, presentation.LapTimeTitle, presentation.LapTimeDescription, true),
        FlyoutColumn.Tyre => new(this, column, presentation.ResultTitle, presentation.ResultDescription, presentation.ShowResultSetting),
        FlyoutColumn.Category => new(this, column, presentation.CategoryTitle, presentation.CategoryDescription, true),
        _ => new(this, column, "Status", presentation.StatusDescription, true)
    };

    /// <summary>Numbers the list and greys out the move buttons at its ends, counting only the columns the page shows.</summary>
    private void UpdateColumnMoveState()
    {
        var supported = _flyoutColumns.Where(item => item.IsSupported).ToList();
        foreach (var item in _flyoutColumns)
        {
            item.CanMoveUp = supported.Count > 0 && !ReferenceEquals(item, supported[0]);
            item.CanMoveDown = supported.Count > 0 && !ReferenceEquals(item, supported[^1]);
        }
        for (var position = 0; position < supported.Count; position++)
            supported[position].Position = position + 1;
    }

    public bool HasEnabledStream => EnableFormula1 || EnableFormula2 || EnableFormula3 || EnableMotoGp || EnableMoto2 ||
        EnableMoto3 || EnableGtWorldChallengeEurope || EnableWrc || EnableImsa || EnableFormulaE || EnableDtm || EnableIndyCar;

    public void Load(UserSettings settings)
    {
        var normalized = settings.Normalize();
        _syncingProfile = true;
        foreach (var status in ChampionshipStatuses)
            status.SetVisible(normalized.IsChampionshipVisible(status.Championship));
        EnableFormula1 = normalized.EnableFormula1;
        EnableFormula2 = normalized.EnableFormula2;
        EnableFormula3 = normalized.EnableFormula3;
        EnableMotoGp = normalized.EnableMotoGp;
        EnableMoto2 = normalized.EnableMoto2;
        EnableMoto3 = normalized.EnableMoto3;
        EnableGtWorldChallengeEurope = normalized.EnableGtWorldChallengeEurope;
        EnableWrc = normalized.EnableWrc;
        EnableImsa = normalized.EnableImsa;
        EnableFormulaE = normalized.EnableFormulaE;
        EnableDtm = normalized.EnableDtm;
        EnableIndyCar = normalized.EnableIndyCar;
        AutoStartOnWindowsBoot = normalized.AutoStartOnWindowsBoot;
        InterfaceAnimations = normalized.InterfaceAnimations;
        FlyoutAcrylicBlur = normalized.FlyoutAcrylicBlur;
        FlyoutBlurTint = normalized.FlyoutBlurTint;
        RotationSeconds = normalized.RotationSeconds;
        CompletedSessionRetentionMinutes = normalized.CompletedSessionRetentionMinutes;
        WidgetPosition = normalized.WidgetPosition;
        _profiles = normalized.ChampionshipDisplay!;
        SelectedChampionship = Championship.Formula1;
        LoadProfile(_profiles.For(SelectedChampionship));
        _syncingProfile = false;
        RefreshDashboardChampionshipStatuses();
        OnPropertyChanged(nameof(HasEnabledStream));
        OnPropertyChanged(nameof(ChampionshipStatuses));
    }

    public void ResetGeneral()
    {
        var defaults = new UserSettings();
        AutoStartOnWindowsBoot = defaults.AutoStartOnWindowsBoot;
        InterfaceAnimations = defaults.InterfaceAnimations;
        FlyoutAcrylicBlur = defaults.FlyoutAcrylicBlur;
        FlyoutBlurTint = defaults.FlyoutBlurTint;
        RotationSeconds = defaults.RotationSeconds;
        CompletedSessionRetentionMinutes = defaults.CompletedSessionRetentionMinutes;
        WidgetPosition = defaults.WidgetPosition;
    }

    /// <summary>Restores the taskbar and flyout profile of the championship currently on screen, plus its power switch.</summary>
    public void ResetSelectedChampionship()
    {
        var championship = SelectedChampionship;
        SetChampionshipEnabled(championship, new UserSettings().IsChampionshipEnabled(championship));
        _profiles = _profiles.With(championship, new ChampionshipDisplaySettings());
        _syncingProfile = true;
        LoadProfile(_profiles.For(championship));
        _syncingProfile = false;
    }

    public void ResetChampionshipVisibility()
    {
        var defaults = new UserSettings();
        foreach (var status in ChampionshipStatuses)
            SetChampionshipVisible(status, defaults.IsChampionshipVisible(status.Championship));
    }

    public UserSettings BuildSettings()
    {
        StoreCurrentProfile();
        var formula1 = _profiles.For(Championship.Formula1);
        return new UserSettings
        {
            EnableFormula1 = EnableFormula1,
            EnableFormula2 = EnableFormula2,
            EnableFormula3 = EnableFormula3,
            EnableMotoGp = EnableMotoGp,
            EnableMoto2 = EnableMoto2,
            EnableMoto3 = EnableMoto3,
            EnableGtWorldChallengeEurope = EnableGtWorldChallengeEurope,
            EnableWrc = EnableWrc,
            EnableImsa = EnableImsa,
            EnableFormulaE = EnableFormulaE,
            EnableDtm = EnableDtm,
            EnableIndyCar = EnableIndyCar,
            HiddenChampionships = [.. ChampionshipStatuses.Where(status => !status.IsVisible).Select(status => status.Championship)],
            RotationSeconds = RotationSeconds,
            CompletedSessionRetentionMinutes = CompletedSessionRetentionMinutes,
            DriverCount = formula1.TaskbarCompetitorCount,
            ShowPositionColors = formula1.FlyoutShowPositionColors,
            UseManufacturerLogos = formula1.FlyoutUseManufacturerLogos,
            AutoStartOnWindowsBoot = AutoStartOnWindowsBoot,
            InterfaceAnimations = InterfaceAnimations,
            FlyoutAcrylicBlur = FlyoutAcrylicBlur,
            FlyoutBlurTint = FlyoutBlurTint,
            WidgetPosition = WidgetPosition,
            ChampionshipDisplay = _profiles.Normalize()
        }.Normalize();
    }

    private void LoadProfile(ChampionshipDisplaySettings display)
    {
        TaskbarCompetitorCount = display.TaskbarCompetitorCount;
        TaskbarUseManufacturerLogos = display.TaskbarUseManufacturerLogos;
        FlyoutCompetitorCount = display.FlyoutCompetitorCount;
        FlyoutShowTeamNames = display.FlyoutShowTeamNames;
        FlyoutUseManufacturerLogos = display.FlyoutUseManufacturerLogos;
        FlyoutShowPositionColors = display.FlyoutShowPositionColors;
        FlyoutShowGaps = display.FlyoutShowGaps;
        FlyoutShowIntervals = display.FlyoutShowIntervals;
        FlyoutShowLapTime = display.FlyoutShowLapTime;
        FlyoutShowTyre = display.FlyoutShowTyre;
        FlyoutShowCategoryOrLap = display.FlyoutShowCategoryOrLap;
        FlyoutShowStatus = display.FlyoutShowStatus;
        RebuildFlyoutColumns(display.FlyoutColumnOrder);
    }

    private void StoreCurrentProfile()
    {
        var display = new ChampionshipDisplaySettings
        {
            TaskbarCompetitorCount = TaskbarCompetitorCount,
            TaskbarUseManufacturerLogos = TaskbarUseManufacturerLogos,
            FlyoutCompetitorCount = FlyoutCompetitorCount,
            FlyoutShowTeamNames = FlyoutShowTeamNames,
            FlyoutUseManufacturerLogos = FlyoutUseManufacturerLogos,
            FlyoutShowPositionColors = FlyoutShowPositionColors,
            FlyoutShowGaps = FlyoutShowGaps,
            FlyoutShowIntervals = FlyoutShowIntervals,
            FlyoutShowLapTime = FlyoutShowLapTime,
            FlyoutShowTyre = FlyoutShowTyre,
            FlyoutShowCategoryOrLap = FlyoutShowCategoryOrLap,
            FlyoutShowStatus = FlyoutShowStatus,
            FlyoutColumnOrder = [.. _flyoutColumns.Select(item => item.Column)]
        }.Normalize();
        _profiles = _profiles.With(SelectedChampionship, display);
    }

    private void SetStream(Championship championship, ref bool field, bool value, [CallerMemberName] string? name = null)
    {
        if (!Set(ref field, value, name)) return;
        _statusByChampionship[championship].Update(value);
        RefreshDashboardChampionshipStatuses();
        OnPropertyChanged(nameof(HasEnabledStream));
        if (championship == SelectedChampionship) OnPropertyChanged(nameof(SelectedChampionshipEnabled));
    }

    private void RefreshDashboardChampionshipStatuses()
    {
        var visible = ChampionshipStatuses.Where(status => status.IsVisible && (status.IsChampionshipEnabled || status.HasTiming)).ToList();
        // Timing updates arrive several times a second, so only rebuild the cards when the set itself changes.
        if (DashboardChampionshipStatuses.SequenceEqual(visible)) return;
        DashboardChampionshipStatuses.Clear();
        foreach (var status in visible) DashboardChampionshipStatuses.Add(status);
    }

    private static ChampionshipStatusItem Status(Championship championship, string displayName, string iconFile, string description) =>
        new(championship, displayName, ChampionshipLogoRoot + iconFile, description);

    /// <summary>Applies a visibility change; hiding a championship disables it so nothing runs while it is out of sight.</summary>
    private void SetChampionshipVisible(ChampionshipStatusItem status, bool visible)
    {
        status.SetVisible(visible);
        if (!visible) SetChampionshipEnabled(status.Championship, false);
        RefreshDashboardChampionshipStatuses();
        OnPropertyChanged(nameof(ChampionshipStatuses));
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
