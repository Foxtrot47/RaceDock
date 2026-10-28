// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using RaceDock.Core.Presentation;

namespace RaceDock.Core;

/// <summary>
/// Turns timing snapshots and schedules into the immutable, front-end-neutral values used by the
/// Plasma widget. Everything here is presentation logic that is useful on any desktop.
/// </summary>
public static class RaceDockPresentationBuilder
{
    private static readonly (string Start, string End)[] PositionColors =
    [
        ("#913E07", "#D3771A"),
        ("#801036", "#CA2A62"),
        ("#0E496B", "#2083B9"),
        ("#4A1473", "#8230C6"),
        ("#105437", "#249465")
    ];

    private static readonly (string Start, string End) NeutralPosition = ("#34373F", "#535864");

    public static RaceDockWidgetState BuildWidget(
        TimingSnapshot? snapshot,
        UserSettings settings,
        IReadOnlyList<UpcomingSession> upcoming,
        RaceAlert? alert,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        bool testMode,
        string? viewMode = null)
    {
        var display = settings.DisplayFor(snapshot?.Championship ?? Championship.Formula1);
        var mode = IsLive(snapshot) ? "live" : "hidden";
        RaceDockIdle? idle = null;

        if (mode == "hidden" && !testMode && settings.ShowNextSessionWhenIdle &&
            NextSessionDisplay.Next(upcoming, now, timeZone) is { } next)
        {
            mode = "idle";
            idle = BuildIdle(next, upcoming, now, timeZone);
        }

        var widget = new RaceDockWidgetState
        {
            Mode = mode,
            ViewMode = viewMode ?? "live",
            Championship = snapshot?.Championship.ToString(),
            ChampionshipShort = snapshot is null ? null : ChampionshipDisplay.ShortName(snapshot.Championship),
            ChampionshipName = snapshot is null ? null : ChampionshipDisplay.FullName(snapshot.Championship),
            Event = snapshot is null ? idle?.Headline : ChampionshipDisplay.CompactEvent(snapshot),
            FullEvent = snapshot is null ? null : ChampionshipDisplay.FullEvent(snapshot),
            Session = snapshot?.Session,
            Progress = snapshot is null ? idle?.Detail : TaskbarSessionDisplay.Progress(snapshot),
            Lap = snapshot is null ? null : TaskbarSessionDisplay.Progress(snapshot),
            TrackCondition = snapshot?.TrackCondition.ToString(),
            TrackLabel = snapshot is null ? null : TrackLabel(snapshot.TrackCondition),
            Connection = snapshot?.ConnectionState.ToString(),
            Accent = ChampionshipAccent(snapshot?.Championship ?? Championship.Unknown),
            Alert = alert is null ? null : BuildAlert(alert),
            Flag = snapshot is null ? null : BuildFlag(snapshot, alert),
            Competitors = snapshot is null ? [] : BuildWidgetCompetitors(snapshot, display, settings),
            Idle = idle,
            Display = new RaceDockDisplay
            {
                SingleRow = settings.UseSingleRowTaskbar,
                CompetitorCount = display.TaskbarCompetitorCount,
                ShowNextSessionWhenIdle = settings.ShowNextSessionWhenIdle,
                Animations = settings.InterfaceAnimations,
                PositionColors = display.FlyoutShowPositionColors,
                ManufacturerLogos = display.TaskbarUseManufacturerLogos,
                WidthHint = mode == "idle" ? 220 : 520
            },
            UpdatedAt = now
        };
        return widget;
    }

    public static RaceDockFlyoutState BuildFlyout(
        TimingSnapshot? snapshot,
        UserSettings settings,
        IReadOnlyList<UpcomingSession> upcoming,
        RaceAlert? alert,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        string? viewMode = null)
    {
        if (IsLive(snapshot))
            return BuildLiveFlyout(snapshot!, settings, now, timeZone, viewMode);

        var schedule = NextSessionDisplay.Schedule(upcoming, now, timeZone);
        if (schedule.Count == 0)
        {
            return new RaceDockFlyoutState
            {
                Mode = "hidden",
                ViewMode = viewMode ?? "live",
                Title = "Next on track",
                EmptyMessage = "No published sessions for the championships you follow.",
                Schedule = [],
                UpdatedAt = now
            };
        }

        return new RaceDockFlyoutState
        {
            Mode = "schedule",
            ViewMode = viewMode ?? "live",
            Title = "Next on track",
            Schedule = [.. schedule.Select((session, index) => BuildScheduleItem(session, now, timeZone, index == 0))],
            Columns = [],
            Rows = [],
            Width = 460,
            Height = Math.Min(620, 150 + (schedule.Count * 64)),
            UpdatedAt = now
        };
    }

    private static RaceDockFlyoutState BuildLiveFlyout(
        TimingSnapshot snapshot,
        UserSettings settings,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        string? viewMode = null)
    {
        var display = settings.DisplayFor(snapshot.Championship);
        var rally = IsRally(snapshot);
        var endurance = IsEndurance(snapshot.Championship);
        var lapTimeMode = ChampionshipDisplay.LapTimeModeFor(snapshot);
        var splitPoints = snapshot.StageSplitPoints ?? [];
        var columns = BuildColumns(display, splitPoints.Count, rally, endurance, lapTimeMode);
        var rows = BuildRows(snapshot, display, columns, rally, endurance, lapTimeMode);
        var driverWidth = DriverColumnWidth(snapshot, display);
        var width = 42 + driverWidth + columns.Where(column => column.Visible).Sum(column => column.Width) + 26;
        var height = Math.Min(85 + (Math.Min(12, rows.Count) * 42), 780);

        return new RaceDockFlyoutState
        {
            Mode = "live",
            ViewMode = viewMode ?? "live",
            Title = string.IsNullOrWhiteSpace(snapshot.Meeting) ? "Live classification" : ChampionshipDisplay.FullEvent(snapshot),
            Championship = snapshot.Championship.ToString(),
            ChampionshipShort = ChampionshipDisplay.ShortName(snapshot.Championship),
            ChampionshipName = ChampionshipDisplay.FullName(snapshot.Championship),
            Accent = ChampionshipAccent(snapshot.Championship),
            Event = ChampionshipDisplay.ShortEventName(snapshot.Championship, snapshot.Meeting),
            Session = snapshot.Session,
            Circuit = snapshot.Circuit,
            Progress = SessionProgress(snapshot),
            Connection = snapshot.ConnectionState.ToString(),
            TrackCondition = snapshot.TrackCondition.ToString(),
            TrackLabel = TrackLabel(snapshot.TrackCondition),
            TrackColor = TrackColor(snapshot.TrackCondition),
            ShowManufacturerLogos = display.FlyoutUseManufacturerLogos,
            Width = width,
            Height = height,
            DriverColumnWidth = driverWidth,
            Columns = columns,
            Rows = rows,
            SplitPoints = [.. splitPoints.Select(point => new RaceDockSplitPoint
            {
                Id = point.SplitPointId,
                Number = point.Number,
                DistanceKm = point.DistanceKm,
                Label = $"S{point.Number}"
            })],
            UpdatedAt = now
        };
    }

    private static IReadOnlyList<RaceDockColumn> BuildColumns(
        ChampionshipDisplaySettings display,
        int splitCount,
        bool rally,
        bool endurance,
        LapTimeMode lapTimeMode)
    {
        var columns = new List<RaceDockColumn>();
        foreach (var column in display.FlyoutColumnOrder)
        {
            var visible = IsColumnVisible(display, column);
            var (label, align, width) = column switch
            {
                FlyoutColumn.Gap => ("GAP", "left", 78d),
                FlyoutColumn.Interval => ("INTERVAL", "left", 78d),
                FlyoutColumn.LapTime => (rally ? "TIME" : lapTimeMode == LapTimeMode.LastLap ? "LAST" : "BEST", "left", 78d),
                FlyoutColumn.Sectors => ("S1  S2  S3", "center", 210d),
                FlyoutColumn.Splits => ("SPLITS", "center", Math.Max(70, splitCount * 70d)),
                FlyoutColumn.Tyre => ("TYRES", "center", 60d),
                FlyoutColumn.Category => (rally || endurance ? "CLASS" : "LAP", "center", 58d),
                FlyoutColumn.Status => ("STATUS", "center", 64d),
                FlyoutColumn.Energy => ("ENERGY", "center", 72d),
                _ => (column.ToString().ToUpperInvariant(), "left", 64d)
            };
            columns.Add(new RaceDockColumn
            {
                Key = ToCamelCase(column.ToString()),
                Label = label,
                Align = align,
                Width = width,
                Visible = visible
            });
        }
        return columns;
    }

    private static IReadOnlyList<RaceDockRow> BuildRows(
        TimingSnapshot snapshot,
        ChampionshipDisplaySettings display,
        IReadOnlyList<RaceDockColumn> columns,
        bool rally,
        bool endurance,
        LapTimeMode lapTimeMode)
    {
        var splitPoints = snapshot.StageSplitPoints ?? [];
        return [.. snapshot.Competitors
            .OrderBy(standing => standing.Position)
            .Take(display.FlyoutCompetitorCount)
            .Select(standing => BuildRow(snapshot, standing, display, columns, splitPoints, rally, endurance, lapTimeMode))];
    }

    private static RaceDockRow BuildRow(
        TimingSnapshot snapshot,
        CompetitorStanding standing,
        ChampionshipDisplaySettings display,
        IReadOnlyList<RaceDockColumn> columns,
        IReadOnlyList<RallySplitPoint> splitPoints,
        bool rally,
        bool endurance,
        LapTimeMode lapTimeMode)
    {
        var (start, end) = PositionColorsFor(standing.Position, display.FlyoutShowPositionColors);
        var lapTime = rally || lapTimeMode == LapTimeMode.BestLap
            ? standing.ResultTime
            : standing.LastLapTime ?? standing.ResultTime;
        var visible = columns.Where(column => column.Visible).Select(column => column.Key).ToHashSet(StringComparer.Ordinal);

        return new RaceDockRow
        {
            Position = standing.Position,
            PositionLabel = standing.PositionLabel ?? standing.Position.ToString(CultureInfo.InvariantCulture),
            Code = standing.Code,
            Name = ChampionshipDisplay.FlyoutDriverName(snapshot.Championship, standing),
            Team = standing.Team,
            ManufacturerLogo = ManufacturerLogos.ResolveAsset(standing.Manufacturer ?? standing.Team),
            Gap = visible.Contains("gap") ? (standing.Position == 1 ? "LEADER" : standing.GapToLeader ?? "—") : null,
            Interval = visible.Contains("interval") ? (standing.Position == 1 ? "LEADER" : standing.IntervalToPositionAhead ?? "—") : null,
            LapTime = visible.Contains("lapTime") ? lapTime ?? "—" : null,
            Category = visible.Contains("category")
                ? endurance || rally ? ChampionshipDisplay.CompactCategory(snapshot.Championship, standing.Category) : standing.Lap.ToString(CultureInfo.InvariantCulture)
                : null,
            Lap = standing.Lap,
            PositionColorStart = start,
            PositionColorEnd = end,
            Tyre = visible.Contains("tyre") ? BuildTyre(standing, snapshot.Championship) : null,
            Status = visible.Contains("status") ? BuildStatus(standing) : null,
            Energy = visible.Contains("energy") ? BuildEnergy(standing.VirtualEnergyPercent) : null,
            Sectors = visible.Contains("sectors") ? BuildSectors(standing, display) : [],
            Splits = visible.Contains("splits") ? BuildSplits(standing, splitPoints) : [],
            IsOverallFastest = standing.IsOverallFastest,
            InPit = standing.InPit,
            Retired = standing.Retired,
            Stopped = standing.Stopped,
            Opacity = standing.Retired || standing.Stopped ? 0.62 : 1
        };
    }

    private static IReadOnlyList<RaceDockSector> BuildSectors(
        CompetitorStanding standing,
        ChampionshipDisplaySettings display)
    {
        var bestLap = display.FlyoutSectorTimeMode == SectorTimeMode.BestLap;
        var sectors = bestLap ? standing.BestLapSectors : standing.CurrentSectors;
        var mini = bestLap ? null : standing.CurrentMiniSectors;
        var result = new List<RaceDockSector>(3);
        for (var index = 0; index < 3; index++)
        {
            var sector = sectors?.At(index);
            result.Add(new RaceDockSector
            {
                Value = sector?.Value ?? "—",
                State = ToCamelCase((sector?.State ?? SectorTimingState.None).ToString()),
                Color = SectorColor(sector?.State ?? SectorTimingState.None),
                MiniSectors = mini is not null && index < mini.Count
                    ? [.. mini[index].Select(state => new RaceDockMiniSector
                    {
                        State = ToCamelCase(state.ToString()),
                        Color = MiniSectorColor(state)
                    })]
                    : []
            });
        }
        return result;
    }

    private static IReadOnlyList<RaceDockSplit> BuildSplits(
        CompetitorStanding standing,
        IReadOnlyList<RallySplitPoint> points)
    {
        var byId = (standing.StageSplits ?? []).ToDictionary(split => split.SplitPointId);
        return [.. points.Select(point => new RaceDockSplit
        {
            Id = point.SplitPointId,
            Value = byId.TryGetValue(point.SplitPointId, out var split) ? split.Value : "—"
        })];
    }

    private static RaceDockTyre? BuildTyre(CompetitorStanding standing, Championship championship)
    {
        if (championship == Championship.WorldEnduranceChampionship)
        {
            WheelTyre?[] wheels = [standing.CurrentTyres?.FrontLeft, standing.CurrentTyres?.FrontRight, standing.CurrentTyres?.RearLeft, standing.CurrentTyres?.RearRight];
            string[] names = ["Front left", "Front right", "Rear left", "Rear right"];
            return new RaceDockTyre
            {
                ShowAges = true,
                Compound = "",
                Wheels = [.. wheels.Select((wheel, index) => new RaceDockTyreWheel
                {
                    Name = names[index],
                    Compound = wheel?.Compound,
                    Age = wheel?.AgeInLaps,
                    Color = TyreColor(wheel?.Compound)
                })]
            };
        }

        var normalized = NormalizeTyre(standing.Tyre);
        var letter = CompoundLetter(normalized);
        if (letter.Length == 0 && standing.TyreAge is null) return null;
        return new RaceDockTyre
        {
            Letter = letter,
            Compound = normalized,
            Age = standing.TyreAge,
            Color = TyreColor(normalized),
            TextColor = normalized is "MEDIUM" or "M" or "P" or "HARD" or "H" ? "#232323" : "#FFFFFF"
        };
    }

    private static RaceDockStatus BuildStatus(CompetitorStanding standing)
    {
        var (text, tone, color) = standing switch
        {
            { Retired: true } => ("RET", "critical", "#59C72D46"),
            { Stopped: true } => ("STOP", "critical", "#59C72D46"),
            { InPit: true } => ("PIT", "information", "#4D2184BC"),
            { StatusLabel: "RUN" } => ("RUN", "information", "#4D2184BC"),
            { StatusLabel: "OUT" } => ("OUT", "information", "#4D2184BC"),
            { StatusLabel: "FIN" } => ("FIN", "success", "#481E8A58"),
            { StatusLabel: "N/S" } => ("N/S", "neutral", "#4D5C606A"),
            { StatusLabel: "N/C" } => ("N/C", "neutral", "#4D5C606A"),
            { StatusLabel: "DSQ" } => ("DSQ", "critical", "#59C72D46"),
            { StatusLabel: "DUE" } => ("DUE", "neutral", "#4D5C606A"),
            _ => ("", "transparent", "transparent")
        };
        return new RaceDockStatus { Text = text, Tone = tone, Color = color };
    }

    private static RaceDockEnergy BuildEnergy(double? percent)
    {
        var level = EnergyDisplay.Level(percent);
        var available = level != EnergyLevel.Unavailable;
        return new RaceDockEnergy
        {
            Text = available ? percent!.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "—",
            Percent = available ? percent : null,
            Level = ToCamelCase(level.ToString()),
            Color = level switch
            {
                EnergyLevel.Good => "#36BE6B",
                EnergyLevel.Warning => "#E2B624",
                EnergyLevel.Low => "#CD2D3E",
                _ => "#7E8798"
            },
            Available = available
        };
    }

    private static IReadOnlyList<RaceDockCompetitor> BuildWidgetCompetitors(
        TimingSnapshot snapshot,
        ChampionshipDisplaySettings display,
        UserSettings settings)
    {
        var duel = snapshot.Championship == Championship.FormulaE &&
            snapshot.Session.Contains("Duel", StringComparison.OrdinalIgnoreCase);
        var count = duel ? 2 : display.TaskbarCompetitorCount;
        return [.. snapshot.Competitors
            .OrderBy(standing => standing.Position)
            .Take(count)
            .Select(standing =>
            {
                var (start, end) = PositionColorsFor(standing.Position, true);
                return new RaceDockCompetitor
                {
                    Position = standing.Position,
                    PositionLabel = standing.PositionLabel ?? standing.Position.ToString(CultureInfo.InvariantCulture),
                    Code = standing.Code,
                    Name = standing.Name,
                    Team = standing.Team,
                    Gap = standing.GapToLeader,
                    Interval = standing.Position == 1 ? "LEAD" : standing.IntervalToPositionAhead ?? "—",
                    Status = BuildStatus(standing).Text,
                    Category = standing.Category,
                    Manufacturer = standing.Manufacturer,
                    ManufacturerLogo = ManufacturerLogos.ResolveAsset(standing.Manufacturer ?? standing.Team),
                    PositionColorStart = start,
                    PositionColorEnd = end,
                    IsLeader = standing.Position == 1,
                    IsFastest = standing.IsOverallFastest,
                    InPit = standing.InPit,
                    Retired = standing.Retired,
                    Stopped = standing.Stopped
                };
            })];
    }

    private static RaceDockIdle BuildIdle(
        UpcomingSession session,
        IReadOnlyList<UpcomingSession> upcoming,
        DateTimeOffset now,
        TimeZoneInfo timeZone)
    {
        return new RaceDockIdle
        {
            Headline = NextSessionDisplay.Headline(session),
            Detail = NextSessionDisplay.Detail(session, now, timeZone),
            Event = ChampionshipDisplay.ShortEventName(session.Championship, session.EventName),
            Session = session.SessionName ?? "",
            When = NextSessionDisplay.RowLead(session, now, timeZone),
            Tooltip = NextSessionDisplay.Tooltip(session, timeZone),
            Championship = session.Championship.ToString(),
            ChampionshipShort = ChampionshipDisplay.ShortName(session.Championship),
            Accent = ChampionshipAccent(session.Championship),
            LogoText = ChampionshipDisplay.ShortName(session.Championship)
        };
    }

    private static RaceDockScheduleItem BuildScheduleItem(
        UpcomingSession session,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        bool featured)
    {
        return new RaceDockScheduleItem
        {
            Championship = session.Championship.ToString(),
            ChampionshipShort = ChampionshipDisplay.ShortName(session.Championship),
            ChampionshipName = ChampionshipDisplay.FullName(session.Championship),
            Accent = ChampionshipAccent(session.Championship),
            Event = ChampionshipDisplay.ShortEventName(session.Championship, session.EventName),
            Session = session.SessionName,
            Venue = ChampionshipDisplay.ShortVenue(session.Venue),
            Subtitle = NextSessionDisplay.RowSubtitle(session),
            Time = NextSessionDisplay.RowTime(session, timeZone),
            Date = NextSessionDisplay.RowDate(session, timeZone),
            Lead = NextSessionDisplay.RowLead(session, now, timeZone),
            Tooltip = NextSessionDisplay.Tooltip(session, timeZone),
            LogoText = ChampionshipDisplay.ShortName(session.Championship),
            Featured = featured
        };
    }

    private static RaceDockAlert BuildAlert(RaceAlert alert) => new()
    {
        Kind = ToCamelCase(alert.Kind.ToString()),
        Title = alert.Title,
        Detail = alert.Detail,
        Persistent = alert.Persistent,
        Color = alert.Kind switch
        {
            AlertKind.RedFlag => "#CC283E",
            AlertKind.Yellow or AlertKind.DoubleYellow or AlertKind.FullCourseYellow => "#DAA416",
            AlertKind.SafetyCar or AlertKind.VirtualSafetyCar or AlertKind.VirtualSafetyCarEnding => "#D27719",
            AlertKind.Chequered => "#E8E8E8",
            AlertKind.FastestLap => "#C457FF",
            _ => "#4D9AA0A8"
        }
    };

    private static RaceDockFlag? BuildFlag(TimingSnapshot snapshot, RaceAlert? alert)
    {
        // A feed-dump replay publishes snapshots without their original alert history, so an ended
        // snapshot is the only signal that the session reached the chequered flag.
        var transient = alert?.Kind == AlertKind.Chequered || snapshot.Lifecycle == SessionLifecycle.Ended
            ? AlertKind.Chequered
            : (AlertKind?)null;
        if (TaskbarFlagDisplay.Resolve(snapshot.TrackCondition, transient) is not { } kind) return null;
        var (label, color) = kind switch
        {
            AlertKind.RedFlag => ("RED FLAG", "#CC283E"),
            AlertKind.DoubleYellow => ("DOUBLE YELLOW", "#DAA416"),
            AlertKind.Yellow => ("YELLOW", "#DAA416"),
            AlertKind.FullCourseYellow => ("FULL COURSE YELLOW", "#DAA416"),
            AlertKind.Chequered => ("CHEQUERED", "#E8E8E8"),
            _ => ("FLAG", "#DAA416")
        };
        return new RaceDockFlag
        {
            Kind = ToCamelCase(kind.ToString()),
            Label = label,
            Detail = kind == AlertKind.Chequered ? alert?.Detail : null,
            Color = color
        };
    }

    private static bool IsLive(TimingSnapshot? snapshot) =>
        snapshot is not null &&
        snapshot.Competitors.Count > 0 &&
        snapshot.Lifecycle is SessionLifecycle.Live or SessionLifecycle.PreSession or SessionLifecycle.Ended;

    private static bool IsRally(TimingSnapshot snapshot) =>
        snapshot.Championship == Championship.WorldRallyChampionship ||
        snapshot.Meeting.Contains("Rally", StringComparison.OrdinalIgnoreCase) ||
        snapshot.Session.StartsWith("SS", StringComparison.OrdinalIgnoreCase) ||
        snapshot.Session.StartsWith("SHAKEDOWN", StringComparison.OrdinalIgnoreCase);

    private static bool IsEndurance(Championship championship) => championship is
        Championship.GtWorldChallengeEurope or Championship.WorldEnduranceChampionship or
        Championship.EuropeanLeMansSeries or Championship.MichelinLeMansCup or Championship.Imsa;

    private static bool IsColumnVisible(ChampionshipDisplaySettings display, FlyoutColumn column) => column switch
    {
        FlyoutColumn.Gap => display.FlyoutShowGaps,
        FlyoutColumn.Interval => display.FlyoutShowIntervals,
        FlyoutColumn.LapTime => display.FlyoutShowLapTime,
        FlyoutColumn.Sectors => display.FlyoutShowSectorTimes,
        FlyoutColumn.Splits => display.FlyoutShowSplitTimes,
        FlyoutColumn.Energy => display.FlyoutShowEnergy,
        FlyoutColumn.Tyre => display.FlyoutShowTyre,
        FlyoutColumn.Category => display.FlyoutShowCategoryOrLap,
        _ => display.FlyoutShowStatus
    };

    private static string SessionProgress(TimingSnapshot snapshot)
    {
        if (IsRally(snapshot)) return snapshot.Session;
        if (!string.IsNullOrWhiteSpace(snapshot.TimeRemaining))
        {
            var endurance = IsEndurance(snapshot.Championship);
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
        return $"Lap {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString(CultureInfo.InvariantCulture) ?? "—"}";
    }

    private static string TrackLabel(TrackCondition condition) => condition switch
    {
        TrackCondition.Yellow => "YELLOW",
        TrackCondition.FullCourseYellow => "FULL COURSE YELLOW",
        TrackCondition.DoubleYellow => "DOUBLE YELLOW",
        TrackCondition.SafetyCar => "SAFETY CAR",
        TrackCondition.VirtualSafetyCar => "VSC",
        TrackCondition.VirtualSafetyCarEnding => "VSC ENDING",
        TrackCondition.RedFlag => "RED FLAG",
        _ => ""
    };

    private static string TrackColor(TrackCondition condition) => condition switch
    {
        TrackCondition.Yellow or TrackCondition.FullCourseYellow or TrackCondition.DoubleYellow => "#DAA416",
        TrackCondition.SafetyCar or TrackCondition.VirtualSafetyCar or TrackCondition.VirtualSafetyCarEnding => "#D27719",
        TrackCondition.RedFlag => "#CC283E",
        _ => "#5C606A"
    };

    private static double DriverColumnWidth(TimingSnapshot snapshot, ChampionshipDisplaySettings display)
    {
        var widest = 0;
        foreach (var standing in snapshot.Competitors)
        {
            widest = Math.Max(widest, ChampionshipDisplay.FlyoutDriverName(snapshot.Championship, standing).Length);
            if (display.FlyoutShowTeamNames) widest = Math.Max(widest, standing.Team.Length);
        }
        var logo = display.FlyoutUseManufacturerLogos ? 32 : 0;
        return Math.Clamp((widest * 7.0) + logo + 20, 150, 260);
    }

    private static (string Start, string End) PositionColorsFor(int position, bool enabled) =>
        enabled && position is >= 1 and <= 5 ? PositionColors[position - 1] : NeutralPosition;

    private static string ChampionshipAccent(Championship championship) => championship switch
    {
        Championship.Formula1 => "#E10A0A",
        Championship.Formula2 => "#125FA9",
        Championship.Formula3 => "#A61652",
        Championship.FormulaE => "#00677A",
        Championship.MotoGp => "#BE2432",
        Championship.Moto2 => "#8E481D",
        Championship.Moto3 => "#7F3E64",
        Championship.WorldRallyChampionship => "#9B4A09",
        Championship.GtWorldChallengeEurope => "#1D7268",
        Championship.WorldEnduranceChampionship => "#007189",
        Championship.EuropeanLeMansSeries => "#F04E00",
        Championship.MichelinLeMansCup => "#2F5CA6",
        Championship.Imsa => "#22589C",
        Championship.Dtm => "#755B00",
        Championship.IndyCar => "#A43621",
        _ => "#7E8798"
    };

    private static string SectorColor(SectorTimingState state) => state switch
    {
        SectorTimingState.OverallBest => "#C457FF",
        SectorTimingState.PersonalBest => "#36BE6B",
        SectorTimingState.Slower => "#E2B624",
        _ => "#707784"
    };

    private static string MiniSectorColor(MiniSectorState state) => state switch
    {
        MiniSectorState.OverallBest => "#C457FF",
        MiniSectorState.PersonalBest => "#36BE6B",
        MiniSectorState.Slower => "#E2B624",
        MiniSectorState.PitLane or MiniSectorState.Other => "#707784",
        _ => "#5E6470"
    };

    private static string NormalizeTyre(string? tyre)
    {
        var normalized = tyre?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(normalized) || normalized == "UNKNOWN" ? "" : normalized;
    }

    private static string CompoundLetter(string normalized) => normalized switch
    {
        "SOFT" or "O" or "S" => "S",
        "MEDIUM" or "P" or "M" => "M",
        "HARD" or "H" => "H",
        "INTERMEDIATE" or "I" => "I",
        "WET" or "W" => "W",
        _ => ""
    };

    private static string TyreColor(string? compound) => compound?.Trim().ToUpperInvariant() switch
    {
        "SOFT" or "S" or "O" => "#CD2D3E",
        "MEDIUM" or "M" or "P" => "#E2B624",
        "HARD" or "H" => "#E0E2E8",
        "INTERMEDIATE" or "I" => "#289C57",
        "WET" or "W" => "#2373C4",
        _ => "#5C606A"
    };

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
