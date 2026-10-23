// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;
using RaceDock.Core;
using RaceDock.Core.Presentation;

/// <summary>
/// Focused tests for the toolkit-neutral presentation values the Plasma widget renders. Every assertion
/// here is a promise the QML side relies on, so a formatting change in Core has to update these first.
/// </summary>
internal static class PresentationTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    public static void WidgetGapAndIntervalCells()
    {
        var snapshot = Snapshot(Championship.Formula1,
            Standing(1, "NOR", gap: "LEAD"),
            Standing(2, "PIA", gap: "+1.234", interval: "+0.456"),
            Standing(3, "LEC", gap: "+2.000"),
            Standing(4, "VER", gap: "+3.000", interval: "+1.000"),
            Standing(5, "RUS", gap: "+4.000", interval: "+1.000"),
            Standing(6, "ANT", gap: "+5.000", interval: "+1.000"));

        var widget = RaceDockPresentationBuilder.BuildWidget(
            snapshot, Settings(), [], null, Now, Utc, testMode: false);

        Equal("live", widget.Mode);
        // The panel never draws more cells than the configured count, even when more cars are running.
        Equal(5, widget.Competitors.Count);
        Equal("NOR", widget.Competitors[0].Code);
        Equal("LEAD", widget.Competitors[0].Interval);
        True(widget.Competitors[0].IsLeader);
        Equal("+1.234", widget.Competitors[1].Gap);
        Equal("+0.456", widget.Competitors[1].Interval);
        // A missing interval is a placeholder, never a stale gap from the row ahead.
        Equal("—", widget.Competitors[2].Interval);
        False(widget.Competitors[2].IsLeader);
        Equal(5, widget.Display.CompetitorCount);
    }

    public static void PositionColorsFollowPodiumAndPoints()
    {
        var snapshot = Snapshot(Championship.Formula1,
            Standing(1, "NOR"), Standing(2, "PIA"), Standing(3, "LEC"),
            Standing(4, "VER"), Standing(5, "RUS"), Standing(6, "ANT"));

        var rows = Flyout(snapshot, Settings()).Rows;
        Equal("#913E07", rows[0].PositionColorStart);
        Equal("#D3771A", rows[0].PositionColorEnd);
        Equal("#105437", rows[4].PositionColorStart);
        Equal("#249465", rows[4].PositionColorEnd);
        // Outside the points the gradient stays neutral instead of inventing a sixth colour.
        Equal("#34373F", rows[5].PositionColorStart);
        Equal("#535864", rows[5].PositionColorEnd);

        var muted = Flyout(snapshot, Settings(new ChampionshipDisplaySettings { FlyoutShowPositionColors = false })).Rows;
        Equal("#34373F", muted[0].PositionColorStart);
        Equal("#535864", muted[0].PositionColorEnd);
    }

    public static void SectorAndMiniSectorColors()
    {
        var sectors = new SectorTimingSet(
            new SectorTiming("29.842", SectorTimingState.OverallBest),
            new SectorTiming("31.104", SectorTimingState.PersonalBest),
            new SectorTiming("35.120", SectorTimingState.Slower));
        IReadOnlyList<IReadOnlyList<MiniSectorState>> mini =
        [
            [MiniSectorState.OverallBest, MiniSectorState.PersonalBest, MiniSectorState.Slower,
             MiniSectorState.NotReached, MiniSectorState.PitLane, MiniSectorState.Other],
            [MiniSectorState.PersonalBest],
            []
        ];
        var snapshot = Snapshot(Championship.Formula1,
            Standing(1, "NOR", currentSectors: sectors, miniSectors: mini));
        var display = new ChampionshipDisplaySettings { FlyoutShowSectorTimes = true };

        var row = Flyout(snapshot, Settings(display)).Rows[0];
        Equal(3, row.Sectors.Count);
        Equal("29.842", row.Sectors[0].Value);
        Equal("overallBest", row.Sectors[0].State);
        Equal("#C457FF", row.Sectors[0].Color);
        Equal("#36BE6B", row.Sectors[1].Color);
        Equal("#E2B624", row.Sectors[2].Color);
        Equal("#C457FF", row.Sectors[0].MiniSectors[0].Color);
        Equal("#36BE6B", row.Sectors[0].MiniSectors[1].Color);
        Equal("#E2B624", row.Sectors[0].MiniSectors[2].Color);
        Equal("notReached", row.Sectors[0].MiniSectors[3].State);
        Equal("#5E6470", row.Sectors[0].MiniSectors[3].Color);
        Equal("#707784", row.Sectors[0].MiniSectors[4].Color);
        Equal("#707784", row.Sectors[0].MiniSectors[5].Color);
        Equal(1, row.Sectors[1].MiniSectors.Count);
        Equal(0, row.Sectors[2].MiniSectors.Count);

        // Best-lap mode reads the completed lap and therefore has no loops in progress.
        var best = Flyout(snapshot, Settings(display with { FlyoutSectorTimeMode = SectorTimeMode.BestLap })).Rows[0];
        Equal(0, best.Sectors[0].MiniSectors.Count);
        Equal("—", best.Sectors[0].Value);
    }

    public static void TyreCompoundsAndWecWheelAges()
    {
        var snapshot = Snapshot(Championship.Formula1,
            Standing(1, "NOR", tyre: "SOFT", tyreAge: 7),
            Standing(2, "PIA", tyre: "MEDIUM", tyreAge: 3),
            Standing(3, "LEC", tyre: "HARD", tyreAge: 12, inPit: true),
            Standing(4, "VER", tyre: "INTERMEDIATE", tyreAge: 2),
            Standing(5, "RUS", tyre: "WET", tyreAge: 1),
            Standing(6, "ANT", tyre: "UNKNOWN"));

        var rows = Flyout(snapshot, Settings()).Rows;
        Equal("S", rows[0].Tyre!.Letter);
        Equal(7, rows[0].Tyre!.Age);
        Equal("#CD2D3E", rows[0].Tyre!.Color);
        Equal("#FFFFFF", rows[0].Tyre!.TextColor);
        Equal("M", rows[1].Tyre!.Letter);
        Equal("#E2B624", rows[1].Tyre!.Color);
        // Medium and hard paint dark text on a bright compound colour.
        Equal("#232323", rows[1].Tyre!.TextColor);
        Equal("H", rows[2].Tyre!.Letter);
        Equal("#E0E2E8", rows[2].Tyre!.Color);
        Equal("#232323", rows[2].Tyre!.TextColor);
        Equal("I", rows[3].Tyre!.Letter);
        Equal("#289C57", rows[3].Tyre!.Color);
        Equal("W", rows[4].Tyre!.Letter);
        Equal("#2373C4", rows[4].Tyre!.Color);
        // No compound and no age means there is nothing to draw.
        Equal<RaceDockTyre?>(null, rows[5].Tyre);
        Equal("PIT", rows[2].Status!.Text);

        var wec = Snapshot(Championship.WorldEnduranceChampionship,
            Standing(1, "VAN", tyres: new CarTyres(
                new WheelTyre(12, "SOFT"), new WheelTyre(13, "SOFT"),
                new WheelTyre(null, "HARD"), new WheelTyre(11, "WET"))));
        var wheel = Flyout(wec, Settings()).Rows[0].Tyre!;
        True(wheel.ShowAges);
        Equal(4, wheel.Wheels.Count);
        Equal("Front left", wheel.Wheels[0].Name);
        Equal(12, wheel.Wheels[0].Age);
        Equal("#CD2D3E", wheel.Wheels[0].Color);
        Equal<int?>(null, wheel.Wheels[2].Age);
        Equal("#E0E2E8", wheel.Wheels[2].Color);
        Equal("#2373C4", wheel.Wheels[3].Color);
    }

    public static void RallySplitsFollowProviderIdentity()
    {
        var snapshot = Snapshot(Championship.WorldRallyChampionship,
            Standing(1, "ROV", splits: [new RallySplitTiming(21, "1:02.3"), new RallySplitTiming(22, "4:11.8")]),
            Standing(2, "EVA", splits: [new RallySplitTiming(22, "4:20.1")]))
            with
        {
            Session = "SS4",
            StageSplitPoints = [new RallySplitPoint(21, 1, 5.2), new RallySplitPoint(22, 2, 12.4)]
        };
        var display = new ChampionshipDisplaySettings { FlyoutShowSplitTimes = true };

        var flyout = Flyout(snapshot, Settings(display));
        Equal(2, flyout.SplitPoints.Count);
        Equal("S1", flyout.SplitPoints[0].Label);
        Equal(21, flyout.SplitPoints[0].Id);
        Equal(5.2, flyout.SplitPoints[0].DistanceKm);
        Equal("SPLITS", flyout.Columns.Single(column => column.Key == "splits").Label);
        Equal("1:02.3", flyout.Rows[0].Splits[0].Value);
        Equal("4:11.8", flyout.Rows[0].Splits[1].Value);
        // A split the crew has not reached yet stays a placeholder in its published slot.
        Equal("—", flyout.Rows[1].Splits[0].Value);
        Equal("4:20.1", flyout.Rows[1].Splits[1].Value);
        // Rally columns are stage-oriented rather than lap-oriented.
        Equal("TIME", flyout.Columns.Single(column => column.Key == "lapTime").Label);
        Equal("CLASS", flyout.Columns.Single(column => column.Key == "category").Label);
    }

    public static void EnergyThresholds()
    {
        var snapshot = Snapshot(Championship.WorldEnduranceChampionship,
            Standing(1, "VAN", energy: 80),
            Standing(2, "EST", energy: 50),
            Standing(3, "CAM", energy: 20),
            Standing(4, "BAM", energy: null));

        var rows = Flyout(snapshot, Settings()).Rows;
        Equal("80%", rows[0].Energy!.Text);
        Equal("good", rows[0].Energy!.Level);
        Equal("#36BE6B", rows[0].Energy!.Color);
        True(rows[0].Energy!.Available);
        Equal("warning", rows[1].Energy!.Level);
        Equal("#E2B624", rows[1].Energy!.Color);
        Equal("low", rows[2].Energy!.Level);
        Equal("#CD2D3E", rows[2].Energy!.Color);
        // No reported energy still occupies the column, but never pretends to be a reading.
        Equal("—", rows[3].Energy!.Text);
        Equal("unavailable", rows[3].Energy!.Level);
        False(rows[3].Energy!.Available);
        Equal<double?>(null, rows[3].Energy!.Percent);
    }

    public static void AlertAndFlagMapping()
    {
        var snapshot = Snapshot(Championship.Formula1, Standing(1, "NOR"));

        var red = RaceDockPresentationBuilder.BuildWidget(
            snapshot with { TrackCondition = TrackCondition.RedFlag }, Settings(), [],
            Alert(AlertKind.RedFlag), Now, Utc, testMode: false);
        Equal("redFlag", red.Alert!.Kind);
        Equal("#CC283E", red.Alert!.Color);
        Equal("redFlag", red.Flag!.Kind);
        Equal("RED FLAG", red.Flag!.Label);
        Equal("#CC283E", red.Flag!.Color);

        var yellow = RaceDockPresentationBuilder.BuildWidget(
            snapshot with { TrackCondition = TrackCondition.FullCourseYellow }, Settings(), [],
            Alert(AlertKind.FullCourseYellow), Now, Utc, testMode: false);
        Equal("#DAA416", yellow.Alert!.Color);
        Equal("FULL COURSE YELLOW", yellow.Flag!.Label);

        // Safety cars are modelled as an alert rather than a taskbar flag: the track is still racing green.
        var safetyCar = RaceDockPresentationBuilder.BuildWidget(
            snapshot with { TrackCondition = TrackCondition.VirtualSafetyCar }, Settings(), [],
            Alert(AlertKind.SafetyCar), Now, Utc, testMode: false);
        Equal("#D27719", safetyCar.Alert!.Color);
        Equal<RaceDockFlag?>(null, safetyCar.Flag);
        Equal("VSC", safetyCar.TrackLabel);
        Equal("VSC ENDING", RaceDockPresentationBuilder.BuildWidget(
            snapshot with { TrackCondition = TrackCondition.VirtualSafetyCarEnding }, Settings(), [],
            Alert(AlertKind.VirtualSafetyCarEnding), Now, Utc, testMode: false).TrackLabel);

        // A chequered flag is transient: the track returns to green while the flag is still shown.
        var chequered = RaceDockPresentationBuilder.BuildWidget(
            snapshot with { TrackCondition = TrackCondition.AllClear }, Settings(), [],
            Alert(AlertKind.Chequered), Now, Utc, testMode: false);
        Equal("chequered", chequered.Flag!.Kind);
        Equal("CHEQUERED", chequered.Flag!.Label);
        Equal("#E8E8E8", chequered.Flag!.Color);
        Equal("Chequered flag", chequered.Flag!.Detail);
        Equal("fastestLap", RaceDockPresentationBuilder.BuildWidget(
            snapshot, Settings(), [], Alert(AlertKind.FastestLap), Now, Utc, testMode: false).Alert!.Kind);
    }

    public static void ScheduleWordingAndCountdowns()
    {
        var upcoming = new[]
        {
            new UpcomingSession(Championship.Formula1, "Italian Grand Prix", "Race", Now.AddHours(2), "Monza"),
            new UpcomingSession(Championship.WorldEnduranceChampionship, "6 Hours of Spa", "Race", Now.AddHours(26), "Spa-Francorchamps")
        };

        var widget = RaceDockPresentationBuilder.BuildWidget(
            null, Settings(), upcoming, null, Now, Utc, testMode: false);
        Equal("idle", widget.Mode);
        True(widget.Idle!.Headline.Contains("Italian", StringComparison.Ordinal));
        // The panel line counts down compactly while the idle "when" field words it out in full.
        Equal("Race · in 2h 00m", widget.Idle!.Detail);
        Equal("in 2 hours", widget.Idle!.When);
        Equal("F1", widget.Idle!.LogoText);
        Equal("#E10A0A", widget.Idle!.Accent);
        Equal(220d, widget.Display.WidthHint);

        // A test scenario must not be replaced by the idle countdown.
        Equal("hidden", RaceDockPresentationBuilder.BuildWidget(
            null, Settings(), upcoming, null, Now, Utc, testMode: true).Mode);
        // Nor may the idle countdown appear when the user turned it off.
        Equal("hidden", RaceDockPresentationBuilder.BuildWidget(
            null, Settings(showNextSessionWhenIdle: false), upcoming, null, Now, Utc, testMode: false).Mode);

        var flyout = RaceDockPresentationBuilder.BuildFlyout(null, Settings(), upcoming, null, Now, Utc);
        Equal("schedule", flyout.Mode);
        Equal(2, flyout.Schedule.Count);
        True(flyout.Schedule[0].Featured);
        False(flyout.Schedule[1].Featured);
        Equal("14:00", flyout.Schedule[0].Time);
        Equal("in 2 hours", flyout.Schedule[0].Lead);
        Equal("in 1 day", flyout.Schedule[1].Lead);
        Equal("F1", flyout.Schedule[0].ChampionshipShort);
        Equal("WEC", flyout.Schedule[1].ChampionshipShort);
        Equal(278d, flyout.Height);

        // Nothing published is a distinct state, not an empty classification.
        var empty = RaceDockPresentationBuilder.BuildFlyout(null, Settings(), [], null, Now, Utc);
        Equal("hidden", empty.Mode);
        True((empty.EmptyMessage ?? "").Length > 0);
        Equal(0, empty.Rows.Count);
    }

    public static void JsonContractIsCamelCaseWithStringEnums()
    {
        var widget = RaceDockPresentationBuilder.BuildWidget(
            Snapshot(Championship.Formula1, Standing(1, "NOR"), Standing(2, "PIA")),
            Settings(), [], null, Now, Utc, testMode: false);
        var json = RaceDockJson.Serialize(widget);
        var root = JsonNode.Parse(json)!.AsObject();

        Equal("live", (string?)root["mode"]);
        // Championship and track values are pre-formatted strings, so they keep their domain casing.
        Equal("Formula1", (string?)root["championship"]);
        Equal("AllClear", (string?)root["trackCondition"]);
        Equal(5, root["display"]!["competitorCount"]!.GetValue<int>());
        Equal("#913E07", (string?)root["competitors"]![0]!["positionColorStart"]);
        Equal("NOR", (string?)root["competitors"]![0]!["code"]);
        // Absent values are omitted rather than serialized as nulls the QML side would have to test.
        False(root.ContainsKey("alert"));
        False(root.ContainsKey("idle"));

        var settings = new UserSettings
        {
            RotationSeconds = 9,
            CompletedSessionRetentionMinutes = 12,
            ChampionshipDisplay = new ChampionshipDisplayProfiles
            {
                Formula1 = new ChampionshipDisplaySettings
                {
                    TaskbarCompetitorCount = 3,
                    FlyoutCompetitorCount = 30,
                    FlyoutShowGaps = true,
                    FlyoutShowTyre = false
                }
            }
        }.Normalize();
        var restored = RaceDockJson.DeserializeSettings(RaceDockJson.SerializeSettings(settings));
        Equal(9, restored.RotationSeconds);
        Equal(12, restored.CompletedSessionRetentionMinutes);
        var display = restored.DisplayFor(Championship.Formula1);
        Equal(3, display.TaskbarCompetitorCount);
        Equal(30, display.FlyoutCompetitorCount);
        True(display.FlyoutShowGaps);
        False(display.FlyoutShowTyre);

        // Out-of-range values are clamped on the way in, never trusted from the wire.
        var clamped = RaceDockJson.DeserializeSettings("""{"rotationSeconds":1,"completedSessionRetentionMinutes":90}""");
        Equal(2, clamped.RotationSeconds);
        Equal(30, clamped.CompletedSessionRetentionMinutes);
    }

    private static TimingSnapshot Snapshot(Championship championship, params CompetitorStanding[] competitors) =>
        new("Synthetic Grand Prix", "Race", "Test Ring", 12, 57, TrackCondition.AllClear, competitors,
            Now, SessionLifecycle.Live, ConnectionState.Connected, Championship: championship);

    private static CompetitorStanding Standing(
        int position,
        string code,
        string? gap = null,
        string? interval = null,
        string? tyre = null,
        int? tyreAge = null,
        CarTyres? tyres = null,
        bool inPit = false,
        double? energy = null,
        SectorTimingSet? currentSectors = null,
        IReadOnlyList<IReadOnlyList<MiniSectorState>>? miniSectors = null,
        IReadOnlyList<RallySplitTiming>? splits = null)
    {
        var standing = new CompetitorStanding(
            $"driver-{position}", position, code, $"{code} Driver", $"Team {code}",
            gap, interval, tyre, 10, inPit, false, false, false,
            StatusLabel: null, Category: "HYPERCAR",
            CurrentSectors: currentSectors, StageSplits: splits,
            CurrentMiniSectors: miniSectors);
        return standing with
        {
            TyreAge = tyre is null ? null : tyreAge,
            CurrentTyres = tyres,
            VirtualEnergyPercent = energy
        };
    }

    private static UserSettings Settings(
        ChampionshipDisplaySettings? display = null,
        bool showNextSessionWhenIdle = true) =>
        new UserSettings
        {
            ShowNextSessionWhenIdle = showNextSessionWhenIdle,
            ChampionshipDisplay = new ChampionshipDisplayProfiles
            {
                Formula1 = display ?? new ChampionshipDisplaySettings(),
                WorldEnduranceChampionship = display ?? new ChampionshipDisplaySettings(),
                WorldRallyChampionship = display ?? new ChampionshipDisplaySettings()
            }
        }.Normalize();

    private static RaceDockFlyoutState Flyout(TimingSnapshot snapshot, UserSettings settings) =>
        RaceDockPresentationBuilder.BuildFlyout(snapshot, settings, [], null, Now, Utc);

    private static RaceAlert Alert(AlertKind kind) =>
        new(kind, 10, kind is AlertKind.RedFlag, kind.ToString(), "Chequered flag", 57, Now, $"test:{kind}");

    private static void True(bool value)
    {
        if (!value) throw new Exception("Expected true.");
    }

    private static void False(bool value)
    {
        if (value) throw new Exception("Expected false.");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected '{expected}', got '{actual}'.");
    }
}
