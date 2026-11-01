// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;
using RaceDock.Core;

var tests = new (string Name, Action Run)[]
{
    ("deep merge and mixed primitive fields", DeepMerge),
    ("leader gaps, intervals, ordering and missing values", TimingParsing),
    ("F1 timed sessions use best-lap deltas", F1TimedSessionDeltas),
    ("F1 driver swaps replace the car's driver", F1DriverSwapReplacesCarDriver),
    ("F1 sectors preserve current and best-lap timing", F1SectorTiming),
    ("F1 mini-sectors follow the lap in progress", F1MiniSectors),
    ("tied F1 times produce one leader", TiedF1Times),
    ("hidden F1 substitutions are excluded", HiddenF1Substitution),
    ("retired and fastest lap detection", FastestAndRetired),
    ("numeric F1 status does not override flags", NumericF1Status),
    ("track status mappings and alert priority", AlertPriority),
    ("full-course yellow maps consistently across series", FullCourseYellowAcrossSeries),
    ("taskbar flags follow the displayed track state", TaskbarFlagsFollowSnapshot),
    ("duplicate alerts and injectable expiry clock", DeduplicationAndExpiry),
    ("suppressed alerts are not consumed by higher priorities", SuppressedAlertsStayVisibleLater),
    ("repeated track conditions are separate occurrences", RepeatedTrackConditionsAreNewOccurrences),
    ("render throttling keeps newest immediate state", Throttling),
    ("DPI geometry and Explorer recovery", GeometryAndRecovery),
    ("stale and off-session transitions", FreshnessAndVisibility),
    ("pre-session F1 grid remains hidden", PreSessionF1Grid),
    ("chequered flag does not outlive its session", ChequeredFlagDoesNotOutliveItsSession),
    ("archived F1 startup data remains hidden", ArchivedF1Startup),
    ("a recording opens with the state its source already held", FeedDumpOpensWithTheConnectedState),
    ("scenario timeline integration", ScenarioIntegration),
    ("active feeds rotate at the display interval", FeedRotation),
    ("completed classifications remain visible for the configured interval", CompletedClassificationRetention),
    ("off-session feeds clear retained live classifications", OffSessionClearsRetention),
    ("live feeds publish individually while the rotation continues", FeedDirectoryPublications),
    ("the live feed directory tracks starts ends and retention", FeedDirectoryLifecycle),
    ("a pinned feed ignores every other championship", PinnedFeedSelection),
    ("pinning the schedule adopts the first classification", PinnedScheduleSelection),
    ("WRC categories follow championship eligibility", WrcCategoryEligibility),
    ("WRC season discovery excludes ERC and finds the next rally", WrcSeasonDiscovery),
    ("WRC provider keeps event rally and itinerary ids distinct", WrcProviderIdentifiers),
    ("WRC live split points and times retain provider identity", WrcSplitParsing),
    ("WRC standings align cumulative times to stage splits", WrcSplitStandings),
    ("WRC stage selection requires provider activity", WrcStageSelection),
    ("WRC stage scheduling uses bounded provider reconciliation", WrcStageScheduling),
    ("WRC stage fingerprints ignore provider row ordering", WrcStageFingerprintOrdering),
    ("WRC shakedown standings use actual best runs", WrcShakedownTiming),
    ("WRC missing shakedown data is not a feed failure", WrcOptionalShakedown),
    ("championship labels use compact taskbar and full flyout names", ChampionshipLabels),
    ("shared-entry flyouts identify cars without changing driver identity", SharedEntryNumbers),
    ("taskbar progress follows session formats across championships", TaskbarSessionProgress),
    ("taskbar session labels normalize provider names without guessing parts", TaskbarSessionLabels),
    ("taskbar clocks parse minutes seconds and endurance hours", TaskbarSessionClocks),
    ("sponsored event titles have stable display names", EventNameShortening),
    ("practice and qualifying report best laps, races report last laps", SessionLapTimeMode),
    ("stored classification column orders are repaired", FlyoutColumnOrderNormalization),
    ("manufacturer logos are on by default and a saved choice still wins", LogoDefaultsFollowTheUser),
    ("energy sits beside lap time in the default column order", EnergyFollowsLapTime),
    ("user settings persist and normalize safe ranges", UserSettingsPersistence),
    ("hidden championships stay disabled and out of sight", ChampionshipVisibility),
    ("the experimental flyout blur stays off until asked for", FlyoutBlurSettings),
    ("a feed dump records payloads verbatim per source", FeedDumpCapture),
    ("GTWC feed dumps replay sectors with paused transport controls", FeedDumpReplayTests.GtwcTransport),
    ("WEC feed dumps replay energy tyres and sectors paused", FeedDumpReplayTests.WecTransport),
    ("NASCAR feed dumps replay each series through recorded flags", FeedDumpReplayTests.NascarTransport),
    ("F2, F3, and F1 Academy SignalR dumps replay through the live hub parser", FeedDumpReplayTests.FiaTransport),
    ("feed-dump routing preserves F1 and rejects invalid captures", FeedDumpReplayTests.FormatValidation),
    ("the feed server selects live recordings and honours explicit dumps", FeedServerTests.CatalogSelectsLiveRecordings),
    ("the feed-server flag resolves without disturbing replay", FeedServerTests.FeedServerFlag),
    ("the feed server advertises streams the application can enable", FeedServerTests.StreamAdvertisement),
    ("the feed server replays Formula 1 through the live adapter", FeedServerTests.F1Transport),
    ("the feed server replays GTWC, WEC, and NASCAR over their HTTP endpoints", FeedServerTests.HttpPollTransports),
    ("the feed server streams every championship at once", FeedServerTests.ConcurrentStreams),
    ("an indexed array patch keeps the fields it leaves out", IndexedArrayPatchMerge),
    ("the application log records structured rotating entries", AppLogStructuredRecords),
    ("a failing application log never throws into its caller", AppLogNeverThrows),
    ("feed drift is logged once per source and field", FeedDriftDeduplication),
    ("a fatal session is announced once on the next launch", CrashMarkerAnnouncesOnce),
    ("unreadable settings are kept aside rather than lost", SettingsVersioningAndRecovery),
    ("legacy settings import into the writable data folder", LegacySettingsImport),
    ("only a startup launch is treated as automatic", StartupLaunchDetection),
    ("championship calendars select the next published event", ChampionshipCalendarSelection),
    ("the Formula 1 calendar includes practice sessions", F1CalendarIncludesPractice),
    ("the schedule service queries only enabled championships", UpcomingScheduleAggregation),
    ("event schedules preserve next-session queries and isolate caches", EventScheduleAggregation),
    ("changelog entries drive release notes and update checks", ReleaseNotesAndUpdateCheck),
    ("the idle widget names the next session and when it starts", NextSessionPresentation),
    ("Run values resolve the executable with or without the startup flag", RunValueExecutablePaths),
    ("GTWC schedule names match live timing", GtwcMeetingNames),
    ("GTWC discovery filters support sessions", GtwcDiscoveryFiltering),
    ("GTWC standings map teams manufacturers and classes", GtwcStandings),
    ("GTWC recorded sectors keep current and best laps distinct", GtwcSectorTiming),
    ("GTWC flags and statuses follow Swiss Timing values", GtwcFlagsAndStatuses),
    ("GTWC client uppercases ids and busts caches", GtwcClientPaths),
    ("GTWC source publishes an active classification", GtwcSourceLifecycle),
    ("GTWC rediscovers the schedule after its unit stops answering", GtwcScheduleRefreshAfterUnitFailure),
    ("GTWC production handler has bounded connection lifetime", GtwcHandlerBounds),
    ("IMSA SockJS frames preserve DDP messages", ImsaSockJsProtocol),
    ("IMSA standings map endurance timing", ImsaStandings),
    ("IMSA flags statuses and clocks map provider values", ImsaFlagsAndClock),
    ("Al Kamel session selection rejects stale placeholders", AlkamelSessionWindow),
    ("IMSA source subscribes and publishes an active classification", ImsaSourceLifecycle),
    ("Formula E practice maps Al Kamel timing", FormulaEStandings),
    ("Formula E duels publish only the active pairing", FormulaEDuelPairing),
    ("Formula E future races remain pre-session", FormulaEFutureRace),
    ("Formula E source subscribes and publishes an active classification", FormulaESourceLifecycle),
    ("Formula E source refreshes session discovery between duels", FormulaESourceRefreshesBetweenDuels),
    ("DTM bootstrap maps live race timing", DtmBootstrapTiming),
    ("DTM qualifying derives standings from valid laps", DtmQualifyingTiming),
    ("DTM discovery selects only the running session", DtmSessionSelection),
    ("Griiip endurance bootstrap maps driver and class timing", GriiipEnduranceBootstrapTiming),
    ("WEC recorded sectors progress and retain observed best laps", WecSectorTiming),
    ("WEC tyre ages remain per-wheel and independent of total laps", WecTyreAges),
    ("WEC virtual energy uses reported percentages and defaults on", WecEnergy),
    ("WEC sector history rejects mismatches and resets between sessions", WecSectorValidation),
    ("Griiip endurance discovery selects each running series", GriiipSessionSelection),
    ("INDYCAR practice maps official timing", IndyCarPracticeTiming),
    ("INDYCAR source publishes captured practice timing", IndyCarSourceLifecycle),
    ("INDYCAR source stays hidden while the feed is cold", IndyCarColdFeedLifecycle),
    ("INDYCAR source rejects stale active data", IndyCarStaleFeedLifecycle),
    ("NASCAR Cup race maps field timing and stages", NascarCupRaceTiming),
    ("NASCAR Cup practice and qualifying publish no lap count or clock", NascarCupTimedSessionTiming),
    ("NASCAR Cup flags and lifecycle reject what the feed cannot prove", NascarCupFlagsAndLifecycle),
    ("NASCAR pit road follows is_on_track while the session runs", NascarPitRoad),
    ("NASCAR Cup calendar selects the next published race", NascarCupCalendarSelection),
    ("F1 Academy calendar reads the next round and keeps unconfirmed sessions date-only", F1AcademyCalendar),
    ("NASCAR weekend feed adds published practice and qualifying", NascarWeekendCalendarSessions),
    ("NASCAR Cup settings stay hidden until chosen", NascarCupSettings),
    ("NASCAR Cup source publishes live timing", NascarCupSourceLifecycle),
    ("NASCAR Cup source hides empty and stale feeds", NascarCupSourceHiddenAndStaleFeeds),
    ("NASCAR feed identifies its series", NascarSeriesIdentifiers),
    ("NASCAR feed serves every subscriber from one request", NascarFeedServesEverySubscriberFromOneRequest),
    ("NASCAR feed sleeps to the lead of the next race", NascarFeedSleepsToTheNextRaceLead),
    ("NASCAR feed polls a date-only round through its whole day", NascarFeedPollsDateOnlyRacesAllDay),
    ("NASCAR feed stops requesting without subscribers", NascarFeedStopsRequestingWithoutSubscribers),
    ("NASCAR feed isolates a failing source and a failed request", NascarFeedIsolatesFailures),
    ("NASCAR feed idle delay follows the next published window", NascarFeedIdleDelay),
    ("NASCAR race-list times are US Eastern and races are identified by id", NascarRaceListTimesAndIdentity),
    ("NASCAR calendar fetches the weekend of the selected race id", NascarCalendarWeekendByRaceId),
    ("NASCAR feed fetches the weekend of the selected race id", NascarFeedWeekendByRaceId),
    ("NASCAR pre-session flag waits for green before starting", NascarPreSessionWaitsForGreen),
    ("NASCAR feed wakes from an idle sleep for a newly enabled series", NascarFeedWakesForNewSeries),
    ("NASCAR recorded race and qualifying transitions map lifecycle flags and stages", NascarRecordedTransitions),
    ("NASCAR recorded race replays one start and one chequered alert", NascarRecordedRaceAlerts),
    ("a cancelled schedule caller leaves the shared read running", NascarScheduleCacheSurvivesCallerCancellation),
    ("NASCAR schedule reads are shared, expire, and never cache a failure", NascarScheduleCache),
    ("published event titles fit the schedule and countdown widths", ChampionshipEventNames),
    ("F3 qualifying publishes aborted live data", F3QualifyingSnapshot),
    ("F1 Academy slot-keyed and car-keyed lines share one standing", F1AcademyLinesConvergeOnCarNumber),
    ("F1 Academy array timing publishes live data", F1AcademyLiveTiming),
    ("MotoGP lite payload maps Moto3 timing", MotoGpLitePayload),
    ("MotoGP source filters categories and publishes lifecycle", MotoGpSourceLifecycle),
    ("empty MotoGP gateway responses stay connected and hidden", MotoGpEmptyResponse),
    ("F3 standings include roster teams", F3RosterTeams),
    ("F2 standings include roster teams", F2RosterTeams),
    ("full course yellow is its own track status", FullCourseYellowStatus),
    ("F1 practice timing derives missing live gaps", F1PracticeTiming),
    ("a new session clears the timing the last one left behind", NewSessionClearsPreviousTiming),
    ("F1 qualifying follows the active session part", F1QualifyingParts),
    ("WRC stage timing preserves live API values", WrcStageTiming),
    ("ended feeds clear composite selection", EndedFeedsClearSelection),
    ("WRC final results settle a stale running stage", WrcFinalResultsEndStage),
    ("WRC1 completion ignores lower-category lag", Wrc1Completion),
    ("WRC source stays hidden before the next rally", WrcSourceIdleLifecycle),
    ("WRC source schedules idle polling around the next stage", WrcSourceStageSchedule),
    ("WRC source faults and hides when event metadata fails", WrcSourceFailureLifecycle),
    ("WRC source publishes proven stage timing once", WrcSourceStageLifecycle),
    ("WRC running stage hides once the WRC1 field is done", WrcRunningStageHidesWhenWrc1Done),
    ("WRC stage settles when a WRC1 crew retires on the road", WrcRetiredWrc1CrewSettlesStage),
    ("WRC completed to running reopens the stage", WrcCompletedToRunningReopens),
    ("WRC poll failure recovers with confirmation delay", WrcFailureRecoveryConfirmation),
    ("WRC production handler has bounded connection lifetime", WrcHandlerBounds),
    ("flag letters are chosen by contrast against their strip", FlagLetterContrast),
};
var failures = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
if (failures == 0 && args is ["--gtwc-dump", var capturePath]) FeedDumpReplayTests.ActualCapture(capturePath);
if (failures == 0 && args is ["--wec-dump", var wecCapturePath]) FeedDumpReplayTests.ActualWecCapture(wecCapturePath);
return failures == 0 ? 0 : 1;

static void DeepMerge()
{
    var d = JsonNode.Parse("{\"a\":{\"x\":1,\"y\":2},\"b\":true}")!.AsObject(); var s = JsonNode.Parse("{\"a\":{\"y\":\"3\",\"z\":4},\"b\":0}")!.AsObject();
    JsonSupport.DeepMerge(d, s); Equal(1, JsonSupport.Int(d["a"]!["x"])); Equal(3, JsonSupport.Int(d["a"]!["y"])); Equal(4, JsonSupport.Int(d["a"]!["z"])); False(JsonSupport.Bool(d["b"]));
}

static void TimingParsing()
{
    var (_, _, processor) = Fixture(); processor.ProcessInitial(JsonNode.Parse("""{"DriverList":{"1":{"Tla":"AAA"},"2":{"Tla":"BBB"},"3":{"Tla":"CCC"}},"TimingData":{"Lines":{"1":{"Position":"2","GapToLeader":{"Value":"+1.2"},"IntervalToPositionAhead":0.4},"2":{"Position":1,"GapToLeader":0},"3":{"Position":"3","GapToLeader":"-","IntervalToPositionAhead":{"Value":"+2.0"}}}}}""")!.AsObject());
    Equal("BBB", processor.Current.Competitors[0].Code); Equal("LEAD", processor.Current.Competitors[0].GapToLeader); Equal("+1.2", processor.Current.Competitors[1].GapToLeader); Equal("0.4", processor.Current.Competitors[1].IntervalToPositionAhead); Equal(null, processor.Current.Competitors[2].GapToLeader);
}

/// <summary>A driver change arrives as a DriverList update for the car number; a car that stopped keeps its driver.</summary>
static void F1DriverSwapReplacesCarDriver()
{
    var (_, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
    {"DriverList":{"31":{"Tla":"OCO","FullName":"Esteban OCON","TeamName":"Haas F1 Team","RacingNumber":"31"},
                   "87":{"Tla":"BEA","FullName":"Oliver BEARMAN","TeamName":"Haas F1 Team","RacingNumber":"87"}},
     "TimingData":{"Lines":{"31":{"Position":"22","Retired":true,"Stopped":true,"ShowPosition":true,"NumberOfLaps":4},
                            "87":{"Position":"21","NumberOfLaps":9,"ShowPosition":true}}}}
    """)!.AsObject());
    processor.ProcessDelta("DriverList", JsonNode.Parse("""
    {"50":{"Tla":"HIR","FullName":"Ryo HIRAKAWA","RacingNumber":"50"}}
    """)!, new DateTimeOffset(2026, 10, 31, 17, 32, 58, TimeSpan.Zero));
    processor.ProcessDelta("TimingData", JsonNode.Parse("""
    {"Lines":{"50":{"Position":"20","NumberOfLaps":1,"ShowPosition":true}}}
    """)!, new DateTimeOffset(2026, 10, 31, 17, 33, 0, TimeSpan.Zero));

    var byNumber = processor.Current.Competitors.ToDictionary(c => c.DriverId);
    Equal("HIR", byNumber["50"].Code);
    Equal("Ryo HIRAKAWA", byNumber["50"].Name);
    Equal("OCO", byNumber["31"].Code);
    True(byNumber["31"].Retired);
    Equal("BEA", byNumber["87"].Code);
}

static void F1TimedSessionDeltas()
{
    var (_, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"SessionInfo":{"Name":"Practice 1","Type":"Practice"},
         "DriverList":{"44":{"Tla":"HAM"},"16":{"Tla":"LEC"},"30":{"Tla":"LAW"}},
         "TimingData":{"Lines":{
           "44":{"Position":1,"BestLapTime":{"Value":"1:36.066"}},
           "16":{"Position":2,"GapToLeader":"22L","IntervalToPositionAhead":"22L","BestLapTime":{"Value":"1:36.725"}},
           "30":{"Position":3,"GapToLeader":"+1.492","IntervalToPositionAhead":"+1.492","BestLapTime":{"Value":"1:36.833"}}}}}
        """)!.AsObject());

    Equal("+0.659", processor.Current.Competitors[1].GapToLeader);
    Equal("+0.659", processor.Current.Competitors[1].IntervalToPositionAhead);
    Equal("+0.767", processor.Current.Competitors[2].GapToLeader);
    Equal("+0.108", processor.Current.Competitors[2].IntervalToPositionAhead);
}

static void F1SectorTiming()
{
    var (_, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"SessionInfo":{"Name":"Practice 1","Type":"Practice"},
         "DriverList":{"44":{"Tla":"HAM"}},
         "TimingData":{"Lines":{"44":{
           "Position":1,"NumberOfLaps":5,
           "LastLapTime":{"Value":"1:36.066"},
           "BestLapTime":{"Value":"1:36.066","Lap":5},
           "Sectors":[
             {"Value":"29.842","OverallFastest":true,"PersonalFastest":true},
             {"Value":"31.104","PersonalFastest":true},
             {"Value":"35.120"}]}}}}
        """)!.AsObject());

    var standing = processor.Current.Competitors.Single();
    Equal("29.842", standing.CurrentSectors?.Sector1?.Value);
    Equal(SectorTimingState.OverallBest, standing.CurrentSectors?.Sector1?.State);
    Equal(SectorTimingState.PersonalBest, standing.CurrentSectors?.Sector2?.State);
    Equal(SectorTimingState.Slower, standing.CurrentSectors?.Sector3?.State);
    Equal("35.120", standing.BestLapSectors?.Sector3?.Value);

    processor.ProcessDelta("TimingData", JsonNode.Parse("""
        {"Lines":{"44":{"NumberOfLaps":6,"LastLapTime":{"Value":"1:37.000"},
          "Sectors":[{"Value":"30.000"},{"Value":"31.500"},{"Value":"35.500"}]}}}
        """)!, DateTimeOffset.UtcNow);
    standing = processor.Current.Competitors.Single();
    Equal("30.000", standing.CurrentSectors?.Sector1?.Value);
    Equal("29.842", standing.BestLapSectors?.Sector1?.Value);

    processor.ProcessDelta("TimingData", JsonNode.Parse("""
        {"Lines":{"44":{"BestLapTime":{"Value":"1:35.900"}}}}
        """)!, DateTimeOffset.UtcNow);
    Equal<SectorTimingSet?>(null, processor.Current.Competitors.Single().BestLapSectors);
}

static void F1MiniSectors()
{
    var (_, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"SessionInfo":{"Name":"Race","Type":"Race"},
         "DriverList":{"63":{"Tla":"RUS"},"44":{"Tla":"HAM"}},
         "TimingData":{"Lines":{
           "63":{"Position":1,"NumberOfLaps":3,"Sectors":[
             {"Value":"27.171","Segments":[{"Status":2049},{"Status":2048},{"Status":2051}]},
             {"Value":"","Segments":[{"Status":2048},{"Status":0},{"Status":0}]},
             {"Value":"","Segments":[{"Status":0},{"Status":0}]}]},
           "44":{"Position":2,"NumberOfLaps":3,"Sectors":[{"Value":"28.000"},{"Value":""},{"Value":""}]}}}}
        """)!.AsObject());

    var leader = processor.Current.Competitors[0];
    Equal(3, leader.CurrentMiniSectors!.Count);
    Equal("PersonalBest,Slower,OverallBest", string.Join(',', leader.CurrentMiniSectors[0]));
    Equal("Slower,NotReached,NotReached", string.Join(',', leader.CurrentMiniSectors[1]));
    Equal(2, leader.CurrentMiniSectors[2].Count);
    Equal<IReadOnlyList<IReadOnlyList<MiniSectorState>>?>(null, processor.Current.Competitors[1].CurrentMiniSectors);

    // The feed patches one loop at a time by index, then resets the lap's loops when the next lap starts.
    processor.ProcessDelta("TimingData", JsonNode.Parse("""
        {"Lines":{"63":{"Sectors":{"1":{"Segments":{"1":{"Status":2064}}}}}}}
        """)!, DateTimeOffset.UtcNow);
    Equal("Slower,PitLane,NotReached", string.Join(',', processor.Current.Competitors[0].CurrentMiniSectors![1]));
    processor.ProcessDelta("TimingData", JsonNode.Parse("""
        {"Lines":{"63":{"Sectors":{"0":{"Segments":{"0":{"Status":0},"1":{"Status":0},"2":{"Status":7}}}}}}}
        """)!, DateTimeOffset.UtcNow);
    Equal("NotReached,NotReached,Other", string.Join(',', processor.Current.Competitors[0].CurrentMiniSectors![0]));
}

static void TiedF1Times()
{
    var (_, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"DriverList":{"12":{"Tla":"ANT"},"63":{"Tla":"RUS"}},
         "TimingData":{"Lines":{
           "12":{"Position":1,"ShowPosition":true,"GapToLeader":"LEADER","BestLapTime":{"Value":"1:18.500"}},
           "63":{"Position":1,"ShowPosition":true,"GapToLeader":"LEADER","BestLapTime":{"Value":"1:18.500"}}}}}
        """)!.AsObject());
    Equal(2, processor.Current.Competitors.Count);
    Equal(1, processor.Current.Competitors.Count(x => x.GapToLeader == "LEAD"));
    Equal("+0.000", processor.Current.Competitors[1].GapToLeader);
    Equal("+0.000", processor.Current.Competitors[1].IntervalToPositionAhead);
}

static void HiddenF1Substitution()
{
    var (_, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"DriverList":{
           "1":{"Tla":"AAA","TeamName":"Other"},"2":{"Tla":"BBB","TeamName":"Other"},"3":{"Tla":"CCC","TeamName":"Other"},
           "37":{"Tla":"FOR","FullName":"Leonardo Fornaroli","TeamName":"McLaren"},
           "81":{"Tla":"PIA","FullName":"Oscar Piastri","TeamName":"McLaren"},
           "4":{"Tla":"NOR","FullName":"Lando Norris","TeamName":"McLaren"}},
         "TimingData":{"Lines":{
           "1":{"Position":1},"2":{"Position":2},"3":{"Position":3},
           "37":{"Position":4,"ShowPosition":true,"NumberOfLaps":6},
           "81":{"Position":5,"ShowPosition":false,"NumberOfLaps":44},
           "4":{"Position":5,"ShowPosition":true,"NumberOfLaps":5}}}}
        """)!.AsObject());
    var mclaren = processor.Current.Competitors.Where(x => x.Team == "McLaren").Select(x => x.Code).ToList();
    Equal(5, processor.Current.Competitors.Count);
    Equal("AAA", processor.Current.Competitors[0].Code);
    Equal(2, mclaren.Count);
    Equal("FOR", mclaren[0]);
    Equal("NOR", mclaren[1]);
}

static void TaskbarFlagsFollowSnapshot()
{
    // The widget's edge carries the flag as a code, and a caution covers the safety car periods as well.
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.Caution, "YEL"), TaskbarFlagDisplay.Badge(TrackCondition.Yellow));
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.Caution, "YEL"), TaskbarFlagDisplay.Badge(TrackCondition.DoubleYellow));
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.Caution, "FCY"), TaskbarFlagDisplay.Badge(TrackCondition.FullCourseYellow));
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.Caution, "SC"), TaskbarFlagDisplay.Badge(TrackCondition.SafetyCar));
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.Caution, "VSC"), TaskbarFlagDisplay.Badge(TrackCondition.VirtualSafetyCar));
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.Caution, "VSC"), TaskbarFlagDisplay.Badge(TrackCondition.VirtualSafetyCarEnding));
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.RedFlag, "RED"), TaskbarFlagDisplay.Badge(TrackCondition.RedFlag));
    // A clear or unknown track prints nothing and takes no room.
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.None, null), TaskbarFlagDisplay.Badge(TrackCondition.AllClear));
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.None, null), TaskbarFlagDisplay.Badge(TrackCondition.Unknown));
    // Only the selected session's lifecycle can put the finish tiles on the taskbar.
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.Finished, null), TaskbarFlagDisplay.Badge(TrackCondition.AllClear, finished: true));
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.Finished, null), TaskbarFlagDisplay.Badge(TrackCondition.Unknown, finished: true));
    // A caution still wins over the finish: a race that ends under yellow says why it ended.
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.Caution, "YEL"), TaskbarFlagDisplay.Badge(TrackCondition.Yellow, finished: true));
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.RedFlag, "RED"), TaskbarFlagDisplay.Badge(TrackCondition.RedFlag, finished: true));
}

static void FastestAndRetired()
{
    var (_, alerts, p) = Fixture(); p.ProcessInitial(BaseState());
    p.ProcessDelta("TimingData", JsonNode.Parse("""{"Lines":{"1":{"Retired":"true","Status":68,"BestLapTime":{"Value":"1:20.500","OverallFastest":"true"}}}}""")!, DateTimeOffset.UtcNow);
    True(p.Current.Competitors.Single(x => x.DriverId == "1").Retired); Equal(AlertKind.FastestLap, alerts.Current?.Kind); True(p.Current.Competitors.Single(x => x.DriverId == "1").IsOverallFastest);
}

static void NumericF1Status()
{
    var (_, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"DriverList":{"1":{"Tla":"NOR"},"63":{"Tla":"RUS"},"77":{"Tla":"BOT"}},
         "TimingData":{"Lines":{
           "1":{"Position":1,"Status":64,"Stopped":false,"Retired":false},
           "63":{"Position":2,"Status":608,"Stopped":false,"Retired":false},
           "77":{"Position":3,"Status":28,"Stopped":true,"Retired":true}}}}
        """)!.AsObject());
    False(processor.Current.Competitors.Single(x => x.Code == "NOR").Stopped);
    False(processor.Current.Competitors.Single(x => x.Code == "RUS").Stopped);
    True(processor.Current.Competitors.Single(x => x.Code == "BOT").Stopped);
    True(processor.Current.Competitors.Single(x => x.Code == "BOT").Retired);
}

static void AlertPriority()
{
    var (clock, alerts, p) = Fixture(); p.ProcessInitial(BaseState());
    Track(p, "2"); Equal(AlertKind.Yellow, alerts.Current?.Kind); Track(p, "4"); Equal(AlertKind.SafetyCar, alerts.Current?.Kind);
    alerts.Accept(new(AlertKind.FastestLap, 50, false, "FAST", null, 2, clock.UtcNow, "f1", TimeSpan.FromSeconds(6))); Equal(AlertKind.SafetyCar, alerts.Current?.Kind);
    Track(p, "5"); Equal(AlertKind.RedFlag, alerts.Current?.Kind); Track(p, "1"); Equal(null, alerts.Current);
    p.ProcessDelta("RaceControlMessages", JsonNode.Parse("""{"Messages":{"7":{"Flag":"CHEQUERED","Message":"Finished"}}}""")!, clock.UtcNow); Equal(AlertKind.Chequered, alerts.Current?.Kind);
}

static void DeduplicationAndExpiry()
{
    var clock = new FakeClock(); var arbiter = new AlertArbiter(clock); var changes = 0; arbiter.VisibleAlertChanged += _ => changes++;
    var alert = new RaceAlert(AlertKind.FastestLap, 50, false, "FAST", null, 1, clock.UtcNow, "same", TimeSpan.FromSeconds(6)); arbiter.Accept(alert); arbiter.Accept(alert); Equal(1, changes);
    clock.Advance(TimeSpan.FromSeconds(5)); arbiter.Tick(); True(arbiter.Current is not null); clock.Advance(TimeSpan.FromSeconds(1)); arbiter.Tick(); Equal(null, arbiter.Current);
}

static void SuppressedAlertsStayVisibleLater()
{
    var clock = new FakeClock();
    var arbiter = new AlertArbiter(clock);
    // A chequered flag that arrives during a red flag loses the priority race and must not be consumed by it.
    arbiter.ApplyTrackCondition(TrackCondition.RedFlag, null, 1, clock.UtcNow);
    Equal(AlertKind.RedFlag, arbiter.Current?.Kind);
    RaceAlert chequered = new(AlertKind.Chequered, 90, false, "CHEQUERED FLAG", null, 1, clock.UtcNow, "rc:7", TimeSpan.FromSeconds(6));
    arbiter.Accept(chequered);
    Equal(AlertKind.RedFlag, arbiter.Current?.Kind);
    arbiter.ApplyTrackCondition(TrackCondition.AllClear, null, 1, clock.UtcNow);
    Equal<RaceAlert?>(null, arbiter.Current);
    arbiter.Accept(chequered);
    Equal(AlertKind.Chequered, arbiter.Current?.Kind);
}

static void RepeatedTrackConditionsAreNewOccurrences()
{
    var clock = new FakeClock();
    var arbiter = new AlertArbiter(clock);
    // F1-style code 2 is a yellow; the same condition resubmitted by every message is one occurrence.
    arbiter.ApplyTrackCondition(TrackCondition.Yellow, "YELLOW", null, clock.UtcNow);
    Equal(AlertKind.Yellow, arbiter.Current?.Kind);
    arbiter.ApplyTrackCondition(TrackCondition.Yellow, "YELLOW", null, clock.UtcNow);
    Equal(AlertKind.Yellow, arbiter.Current?.Kind);
    // Green clears it, and the next yellow later in the session must be shown again.
    arbiter.ApplyTrackCondition(TrackCondition.AllClear, null, null, clock.UtcNow);
    Equal<RaceAlert?>(null, arbiter.Current);
    arbiter.ApplyTrackCondition(TrackCondition.Yellow, "YELLOW", null, clock.UtcNow);
    Equal(AlertKind.Yellow, arbiter.Current?.Kind);
    var alerts = 0;
    arbiter.VisibleAlertChanged += alert => { if (alert is not null) alerts++; };
    arbiter.ApplyTrackCondition(TrackCondition.Yellow, "YELLOW", null, clock.UtcNow);
    Equal(0, alerts);
}

static void Throttling()
{
    var clock = new FakeClock(); var gate = new RenderThrottle(clock, TimeSpan.FromMilliseconds(250)); var first = TimingSnapshot.Hidden(clock.UtcNow); gate.Offer(first); Equal(first, gate.TakeIfDue());
    var newer = first with { Meeting = "new" }; gate.Offer(first with { Meeting = "old" }); gate.Offer(newer); Equal(null, gate.TakeIfDue()); clock.Advance(TimeSpan.FromMilliseconds(250)); Equal("new", gate.TakeIfDue()?.Meeting);
}

static void GeometryAndRecovery()
{
    var mapped = TaskbarGeometry.MapWindowRegion(new(100, 900, 2020, 948), new(8, 4, 436, 44)); Equal(new PixelRect(108, 904, 536, 944), mapped);
    var g = TaskbarGeometry.CalculateLeftChild(new(0, 0, 1920, 48), 96)!.Value; Equal(8, g.X); Equal(520, g.Width); Equal(40, g.Height); Equal(4, g.Y);
    var afterWidgets = TaskbarGeometry.CalculateLeftChild(new(0, 0, 1920, 48), 96, [new(0, 0, 52, 48)])!.Value; Equal(56, afterWidgets.X);
    var skipsHostedApp = TaskbarGeometry.CalculateLeftChild(new(0, 0, 2400, 48), 96, [new(0, 0, 52, 48), new(56, 0, 500, 48)])!.Value; Equal(504, skipsHostedApp.X);
    var adaptive = TaskbarGeometry.CalculateLeftChild(new(0, 0, 2560, 48), 96, [new(20, 0, 411, 48), new(926, 0, 1414, 48)])!.Value; Equal(415, adaptive.X); Equal(511, adaptive.Width);
    var right = TaskbarGeometry.CalculateChild(new(0, 0, 1920, 48), 96, TaskbarWidgetPosition.RightOfStart)!.Value; Equal(996, right.X); Equal(520, right.Width); Equal(4, right.Y);
    var blockedRight = TaskbarGeometry.CalculateChild(new(0, 0, 1920, 48), 96, TaskbarWidgetPosition.RightOfStart, [new(1300, 0, 1920, 48)]); Equal<(int X, int Y, int Width, int Height)?>(null, blockedRight);
    var noSafeGap = TaskbarGeometry.CalculateLeftChild(new(0, 0, 1280, 48), 96, [new(0, 0, 240, 48)]); Equal<(int X, int Y, int Width, int Height)?>(null, noSafeGap);
    var hi = TaskbarGeometry.CalculateLeftChild(new(0, 0, 3840, 96), 192)!.Value; Equal(16, hi.X); Equal(1040, hi.Width); Equal(80, hi.Height);
    True(TaskbarRecovery.NeedsReattach(10, 0, false)); True(TaskbarRecovery.NeedsReattach(10, 10, true)); False(TaskbarRecovery.NeedsReattach(10, 10, false));
}

static void FreshnessAndVisibility()
{
    var (clock, _, p) = Fixture(); Equal(SessionLifecycle.OffSession, p.Current.Lifecycle); p.ProcessInitial(BaseState()); Equal(SessionLifecycle.Live, p.Current.Lifecycle);
    clock.Advance(TimeSpan.FromSeconds(13)); p.MarkStale(TimeSpan.FromSeconds(12)); Equal(ConnectionState.Stale, p.Current.ConnectionState); Equal(SessionLifecycle.OffSession, p.Current.Lifecycle);
    p.ProcessInitial(new JsonObject()); Equal(SessionLifecycle.OffSession, p.Current.Lifecycle);
    p.ProcessInitial(BaseState());
    p.ProcessDelta("SessionStatus", new JsonObject { ["Status"] = "Finalised", ["Started"] = "Finished" }, clock.UtcNow);
    Equal(SessionLifecycle.Ended, p.Current.Lifecycle);
}

static void PreSessionF1Grid()
{
    var (clock, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"SessionInfo":{"Name":"Practice 1","Type":"Practice","Meeting":{"Name":"Italian Grand Prix"}},
         "SessionStatus":{"Status":"Inactive","Started":"Inactive"},
         "ExtrapolatedClock":{"Remaining":"01:00:00","Extrapolating":false},
         "DriverList":{"4":{"Tla":"NOR"},"5":{"Tla":"BOR"}},
         "TimingData":{"Lines":{"4":{"Position":1,"NumberOfLaps":1,"Sectors":[{"Value":"38.623"}]},"5":{"Position":2}}}}
        """)!.AsObject());
    Equal(SessionLifecycle.PreSession, processor.Current.Lifecycle);
    Equal("NOR", processor.Current.Competitors[0].Code);
    Equal("BOR", processor.Current.Competitors[1].Code);

    processor.ProcessDelta("SessionStatus", new JsonObject { ["Status"] = "Started", ["Started"] = "Started" }, clock.UtcNow);
    Equal(SessionLifecycle.PreSession, processor.Current.Lifecycle);
    processor.ProcessDelta("TimingData", new JsonObject
    {
        ["Lines"] = new JsonObject
        {
            ["4"] = new JsonObject { ["BestLapTime"] = new JsonObject { ["Value"] = "1:22.410" } }
        }
    }, clock.UtcNow);
    Equal(SessionLifecycle.Live, processor.Current.Lifecycle);
    Equal(1, processor.Current.Competitors.Count);
    Equal("NOR", processor.Current.Competitors[0].Code);

    var (_, _, raceProcessor) = Fixture();
    var race = BaseState();
    race["SessionStatus"] = new JsonObject { ["Status"] = "Started", ["Started"] = "Started" };
    raceProcessor.ProcessInitial(race);
    Equal(SessionLifecycle.Live, raceProcessor.Current.Lifecycle);
}

/// <summary>
/// A chequered flag closes the session it was shown in: F1 publishes "Started" for the session that follows, and the
/// alert raised at the previous finish must not keep it closed or silence the alerts of the session now running.
/// </summary>
static void ChequeredFlagDoesNotOutliveItsSession()
{
    var (clock, alerts, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"SessionInfo":{"Key":11399,"Name":"Practice 3","Type":"Practice","Meeting":{"Name":"Mexico City Grand Prix"}},
         "SessionStatus":{"Status":"Started","Started":"Started"},
         "DriverList":{"11":{"Tla":"PER"},"27":{"Tla":"HUL"}},
         "TimingData":{"Lines":{"11":{"Position":1,"NumberOfLaps":12,"BestLapTime":{"Value":"1:18.000"}},
                                "27":{"Position":2,"NumberOfLaps":11,"BestLapTime":{"Value":"1:18.400"}}}}}
        """)!.AsObject());
    Equal(SessionLifecycle.Live, processor.Current.Lifecycle);
    processor.ProcessDelta("RaceControlMessages", JsonNode.Parse("""
        {"Messages":{"40":{"Utc":"2026-10-31T18:30:00","Lap":20,"Category":"Flag","Flag":"CHEQUERED","Message":"CHEQUERED FLAG"}}}
        """)!, clock.UtcNow);
    Equal(AlertKind.Chequered, alerts.Current!.Kind);
    Equal(SessionLifecycle.Ended, processor.Current.Lifecycle);

    // The next session is a race, so the chequered alert is the only thing that could still keep it closed.
    processor.ProcessDelta("SessionInfo", JsonNode.Parse("""
        {"Key":11404,"Name":"Race","Path":"2026/2026-11-01_Mexico_City_Grand_Prix/2026-11-01_Race/"}
        """)!, clock.UtcNow);
    processor.ProcessDelta("SessionStatus", new JsonObject { ["Status"] = "Started", ["Started"] = "Started" }, clock.UtcNow);
    processor.ProcessDelta("TimingData", new JsonObject
    {
        ["Lines"] = new JsonObject
        {
            ["11"] = new JsonObject { ["Position"] = 1, ["NumberOfLaps"] = 1 },
            ["27"] = new JsonObject { ["Position"] = 2, ["NumberOfLaps"] = 1 }
        }
    }, clock.UtcNow);
    True(alerts.Current?.Kind != AlertKind.Chequered);
    Equal(SessionLifecycle.Live, processor.Current.Lifecycle);

    // Alerts of the running session are heard again: a caution is no longer outranked by the old finish.
    Track(processor, "2");
    Equal(AlertKind.Yellow, alerts.Current!.Kind);
}

static void ArchivedF1Startup()
{
    var (_, _, processor) = Fixture();
    var state = BaseState();
    state["SessionInfo"]!["SessionStatus"] = "Finalised";
    state["SessionStatus"] = new JsonObject { ["Status"] = "Ends", ["Started"] = "Finished" };
    state["SessionData"] = new JsonObject
    {
        ["StatusSeries"] = new JsonArray
        {
            new JsonObject { ["Utc"] = "2026-07-18T10:30:00Z", ["SessionStatus"] = "Started" },
            new JsonObject { ["Utc"] = "2026-07-18T11:30:00Z", ["SessionStatus"] = "Finished" },
            new JsonObject { ["Utc"] = "2026-07-18T11:44:28Z", ["SessionStatus"] = "Ends" }
        }
    };
    processor.ProcessInitial(state);
    Equal(SessionLifecycle.Ended, processor.Current.Lifecycle);
}


static void ScenarioIntegration()
{
    var (clock, alerts, p) = Fixture(); var s = new ScenarioTimingSource(p, clock); s.StartAsync(CancellationToken.None).GetAwaiter().GetResult(); s.Pause();
    if (p.Current.Competitors[0].Code != "NOR") throw new Exception($"initial leader expected NOR, got {p.Current.Competitors[0].Code}");
    s.Step(); Equal(null, alerts.Current); s.Step();
    if (p.Current.Competitors[0].Code != "NOR") throw new Exception($"first gap update leader expected NOR, got {p.Current.Competitors[0].Code}; field={string.Join(',', p.Current.Competitors.Select(x => $"{x.Position}:{x.Code}"))}");
    s.Step(); Equal("LEC", p.Current.Competitors[1].Code); s.Step(); Equal(AlertKind.FastestLap, alerts.Current?.Kind);
    s.Trigger(ScenarioCommand.Yellow); Equal(AlertKind.Yellow, alerts.Current?.Kind); s.Trigger(ScenarioCommand.SafetyCar); Equal(AlertKind.SafetyCar, alerts.Current?.Kind); s.Trigger(ScenarioCommand.Green); Equal(null, alerts.Current);
    s.Trigger(ScenarioCommand.RedFlag); Equal(AlertKind.RedFlag, alerts.Current?.Kind); s.Trigger(ScenarioCommand.Green); Equal(null, alerts.Current); s.Trigger(ScenarioCommand.Disconnect); Equal(ConnectionState.Disconnected, p.Current.ConnectionState); s.Trigger(ScenarioCommand.Reconnect); Equal(ConnectionState.Connected, p.Current.ConnectionState); s.Trigger(ScenarioCommand.Chequered); Equal(AlertKind.Chequered, alerts.Current?.Kind);
    s.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void FeedRotation()
{
    var clock = new FakeClock();
    var first = new FakeTimingSource(Snapshot("F1"));
    var second = new FakeTimingSource(Snapshot("WRC"));
    var source = new CompositeLiveTimingSource([() => first, () => second], clock, TimeSpan.FromMilliseconds(25));
    List<string> meetings = [];
    source.SnapshotReceived += snapshot => meetings.Add(snapshot.Meeting);
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    Thread.Sleep(90);
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    True(meetings.Count >= 4);
    Equal("F1", meetings[0]);
    True(meetings.Zip(meetings.Skip(1)).Any(pair => pair.First != pair.Second));
}

static void CompletedClassificationRetention()
{
    var clock = new FakeClock();
    var feed = new FakeTimingSource(Snapshot("British Grand Prix"));
    var source = new CompositeLiveTimingSource([() => feed], clock, TimeSpan.FromMilliseconds(20), TimeSpan.FromMinutes(6));
    List<TimingSnapshot> snapshots = [];
    source.SnapshotReceived += snapshots.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

    feed.Publish(Snapshot("British Grand Prix", SessionLifecycle.Ended));
    clock.Advance(TimeSpan.FromMinutes(5));
    Thread.Sleep(30);
    Equal("British Grand Prix", snapshots[^1].Meeting);
    clock.Advance(TimeSpan.FromMinutes(1));
    Thread.Sleep(30);
    Equal(SessionLifecycle.OffSession, snapshots[^1].Lifecycle);

    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void OffSessionClearsRetention()
{
    var clock = new FakeClock();
    var feed = new FakeTimingSource(Snapshot("Xfinity"));
    var source = new CompositeLiveTimingSource([() => feed], clock, TimeSpan.FromMilliseconds(20), TimeSpan.FromMinutes(5));
    List<TimingSnapshot> snapshots = [];
    source.SnapshotReceived += snapshots.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

    feed.Publish(TimingSnapshot.Hidden(clock.UtcNow));
    Thread.Sleep(30);
    Equal(SessionLifecycle.OffSession, snapshots[^1].Lifecycle);

    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void FeedDirectoryPublications()
{
    var clock = new FakeClock();
    var first = new FakeTimingSource(FeedSnapshot("F1", Championship.Formula1));
    var second = new FakeTimingSource(FeedSnapshot("WRC", Championship.WorldRallyChampionship));
    var source = new CompositeLiveTimingSource([() => first, () => second], clock, TimeSpan.FromMilliseconds(25));
    List<string> rotated = [];
    List<string> feeds = [];
    List<int> directories = [];
    source.SnapshotReceived += snapshot => { lock (rotated) rotated.Add(snapshot.Meeting); };
    source.FeedSnapshotReceived += snapshot => { lock (feeds) feeds.Add(snapshot.Meeting); };
    source.LiveFeedsChanged += list => { lock (directories) directories.Add(list.Count); };
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

    // A change to the feed the rotation is not showing still reaches the directory consumer.
    first.Publish(FeedSnapshot("F1 Updated", Championship.Formula1));
    Thread.Sleep(40);

    lock (feeds) True(feeds.Contains("F1 Updated"));
    lock (directories) True(directories.Contains(2));
    lock (rotated) True(rotated.Count >= 2);
    Equal(2, source.LiveFeeds.Count);
    Equal(Championship.Formula1, source.LiveFeeds[0].Championship);
    Equal(Championship.WorldRallyChampionship, source.LiveFeeds[1].Championship);

    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void FeedDirectoryLifecycle()
{
    var clock = new FakeClock();
    var first = new FakeTimingSource(FeedSnapshot("British Grand Prix", Championship.Formula1));
    var second = new FakeTimingSource(FeedSnapshot("Rally Finland", Championship.WorldRallyChampionship));
    var source = new CompositeLiveTimingSource([() => first, () => second], clock,
        TimeSpan.FromMilliseconds(20), TimeSpan.FromMinutes(5));
    List<int> changes = [];
    source.LiveFeedsChanged += list => { lock (changes) changes.Add(list.Count); };
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    Equal(2, source.LiveFeeds.Count);

    // One feed leaving the track removes only its own entry.
    second.Publish(TimingSnapshot.Hidden(clock.UtcNow));
    Thread.Sleep(30);
    Equal(1, source.LiveFeeds.Count);
    Equal(Championship.Formula1, source.LiveFeeds[0].Championship);

    // A finished session stays selectable for the retention window, then leaves the directory.
    first.Publish(FeedSnapshot("British Grand Prix", Championship.Formula1, SessionLifecycle.Ended));
    Thread.Sleep(30);
    Equal(1, source.LiveFeeds.Count);
    True(source.LiveFeeds[0].Retained);
    clock.Advance(TimeSpan.FromMinutes(5));
    Thread.Sleep(45);
    Equal(0, source.LiveFeeds.Count);
    lock (changes) True(changes.Contains(0));

    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void PinnedFeedSelection()
{
    var selection = new FeedSelection();
    True(selection.IsFollowingRotation);
    True(selection.TryAccept(FeedSnapshot("F1", Championship.Formula1)));

    selection.Freeze(Championship.Formula1);
    False(selection.IsFollowingRotation);
    True(selection.TryAccept(FeedSnapshot("F1", Championship.Formula1)));
    False(selection.TryAccept(FeedSnapshot("WRC", Championship.WorldRallyChampionship)));
    Equal<Championship?>(Championship.Formula1, selection.PinnedChampionship);

    selection.FollowRotation();
    True(selection.IsFollowingRotation);
    True(selection.TryAccept(FeedSnapshot("WRC", Championship.WorldRallyChampionship)));

    // An unknown or null freeze waits for the first real classification instead of binding to Unknown.
    selection.Freeze(Championship.Unknown);
    True(selection.TryAccept(FeedSnapshot("F1", Championship.Formula1)));
    Equal<Championship?>(Championship.Formula1, selection.PinnedChampionship);
}

static void PinnedScheduleSelection()
{
    var selection = new FeedSelection();
    selection.Freeze(null);
    False(selection.TryAccept(TimingSnapshot.Hidden(DateTimeOffset.UtcNow)));
    True(selection.TryAccept(FeedSnapshot("Spa", Championship.Formula1)));
    False(selection.TryAccept(FeedSnapshot("Rally Finland", Championship.WorldRallyChampionship)));
    Equal<Championship?>(Championship.Formula1, selection.PinnedChampionship);
}

static void WrcCategoryEligibility()
{
    static WrcEntry Entry(string group, string eligibility, string eventClass) =>
        new(1, 1, "AAA", "Driver", "Team", group, eligibility, eventClass, "Entry");

    Equal("WRC2", WrcTiming.RallyCategory(Entry("Rally2", "WRC2 (DC/CC)", "RC2")));
    Equal("RC2", WrcTiming.RallyCategory(Entry("Rally2", "", "RC2")));
    Equal("WRC3", WrcTiming.RallyCategory(Entry("Rally3", "WRC3", "RC3")));
    Equal("RC3", WrcTiming.RallyCategory(Entry("Rally3", "", "RC3")));
    Equal("WRC1", WrcTiming.RallyCategory(Entry("Rally1", "", "RC1")));
}

static void WrcSeasonDiscovery()
{
    using var handler = new RouteHttpHandler(new Dictionary<string, Queue<HttpResponseMessage>>
    {
        ["seasons.json"] = Responses(FixtureText("seasons.json")),
        ["season-detail.json?seasonId=47"] = Responses(FixtureText("season-detail.json"))
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var seasons = WrcTiming.OrderSeasons(api.GetSeasonsAsync(CancellationToken.None).GetAwaiter().GetResult(), 2026);
    Equal(47, seasons[0].SeasonId);
    False(seasons.Any(season => season.Name.Contains("European", StringComparison.OrdinalIgnoreCase)));
    var events = api.GetSeasonEventsAsync(47, CancellationToken.None).GetAwaiter().GetResult();
    var selection = WrcTiming.SelectEvent(events, new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));
    Equal<WrcCalendarEvent?>(null, selection.Current);
    Equal(644, selection.Next?.EventId);
}

static void WrcProviderIdentifiers()
{
    using var handler = new RouteHttpHandler(new Dictionary<string, Queue<HttpResponseMessage>>
    {
        ["events/644.json"] = Responses(FixtureText("event-finland.json")),
        ["events/644/rallies/712/entries.json"] = Responses(FixtureText("entries-finland.json")),
        ["events/644/stages.json"] = Responses(FixtureText("stages-pre-event.json"))
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var detail = api.GetEventAsync(644, CancellationToken.None).GetAwaiter().GetResult();
    var rally = WrcTiming.SelectMainRally(detail.Rallies);
    Equal(644, detail.EventId);
    Equal(712, rally.RallyId);
    Equal(1461, rally.ItineraryId);
    Equal(700, WrcTiming.SelectMainRally([detail.Rallies[0]]).RallyId);
    var entries = api.GetEntriesAsync(detail.EventId, rally.RallyId, CancellationToken.None).GetAwaiter().GetResult();
    Equal(3, entries.Count);
    var stages = api.GetStagesAsync(detail.EventId, CancellationToken.None).GetAwaiter().GetResult();
    Equal(new DateTimeOffset(2026, 7, 30, 16, 5, 0, TimeSpan.Zero), stages[0].StartUtc);
    True(handler.Paths.Contains("events/644/rallies/712/entries.json"));
}

static void WrcSplitParsing()
{
    using var handler = new RouteHttpHandler(new Dictionary<string, Queue<HttpResponseMessage>>
    {
        ["events/646/stages.json"] = Responses(new JsonArray
        {
            new JsonObject
            {
                ["stageId"] = 11608, ["number"] = 4, ["code"] = "SS4", ["name"] = "Turquia 2",
                ["status"] = "Running",
                ["splitPoints"] = new JsonArray
                {
                    new JsonObject { ["splitPointId"] = 21611, ["stageId"] = 11608, ["number"] = 2, ["distance"] = 5.0 },
                    new JsonObject { ["splitPointId"] = 21610, ["stageId"] = 11608, ["number"] = 1, ["distance"] = 2.5 }
                }
            }
        }.ToJsonString()),
        ["events/646/stages/11608/splittimes.json?rallyId=714"] = Responses(new JsonArray
        {
            new JsonObject { ["entryId"] = 1, ["splitPointId"] = 21610, ["elapsedDurationMs"] = 88000 },
            new JsonObject { ["entryId"] = 1, ["splitPointId"] = 21611, ["elapsedDurationMs"] = 171400 }
        }.ToJsonString())
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var stage = api.GetStagesAsync(646, CancellationToken.None).GetAwaiter().GetResult().Single();
    Equal(21610, stage.SplitPoints![0].SplitPointId);
    Equal(1, stage.SplitPoints[0].Number);
    Equal(2.5, stage.SplitPoints[0].DistanceKm);
    var splits = api.TryGetStageSplitTimesAsync(646, 11608, 714, CancellationToken.None).GetAwaiter().GetResult();
    Equal(2, splits.Count);
    Equal(171400, splits[1].ElapsedDurationMs);
    Equal("events/646/stages/11608/splittimes.json?rallyId=714", handler.Paths[^1]);
}

static void WrcSplitStandings()
{
    var entries = new Dictionary<int, WrcEntry>
    {
        [1] = new(1, 1, "AAA", "Driver A", "Toyota", "Rally1", "", "RC1", "Entry"),
        [2] = new(2, 2, "BBB", "Driver B", "Hyundai", "Rally1", "", "RC1", "Entry")
    };
    WrcStageTime[] times =
    [
        new(1, 1, 512500, 0, 0, "Completed"),
        new(2, null, null, null, null, "Running")
    ];
    WrcSplitTime[] splits =
    [
        new(1, 21610, 88000), new(1, 21611, 171400), new(2, 21610, 88400)
    ];
    var standings = WrcTiming.BuildStageStandings(entries, times, splits);
    Equal("1:28.0", standings[0].StageSplits![0].Value);
    Equal("2:51.4", standings[0].StageSplits![1].Value);
    Equal("1:28.4", standings[1].StageSplits![0].Value);
}

static void WrcStageSelection()
{
    static WrcStage Stage(int id, int number, WrcStageStatus status, DateTimeOffset? start = null) =>
        new(id, number, $"SS{number}", $"Stage {number}", status, start, new JsonObject { ["stageId"] = id });
    var now = new DateTimeOffset(2026, 7, 30, 16, 10, 0, TimeSpan.Zero);
    var future = Stage(1, 1, WrcStageStatus.ToRun, now.AddMinutes(5));
    var due = Stage(2, 2, WrcStageStatus.ToRun, now.AddMinutes(-5));
    var running = Stage(3, 3, WrcStageStatus.Running, now.AddMinutes(-1));
    Equal(3, WrcTiming.SelectRunningStage([future, due, running])?.StageId);
    Equal(2, WrcTiming.DueStages([future, due], now)[0].StageId);
    Equal(WrcStageStatus.Interrupted, WrcTiming.ParseStageStatus("interrupted"));
    Equal(WrcStageStatus.Cancelled, WrcTiming.ParseStageStatus("Canceled"));
}

static void WrcStageScheduling()
{
    static WrcStage Stage(int id, int number, WrcStageStatus status, DateTimeOffset? start) =>
        new(id, number, $"SS{number}", $"Stage {number}", status, start, new JsonObject { ["stageId"] = id });

    var now = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
    var sameStart = now.AddMinutes(10);
    WrcStage[] stages =
    [
        Stage(9, 9, WrcStageStatus.Cancelled, now.AddMinutes(1)),
        Stage(8, 8, WrcStageStatus.Completed, now.AddMinutes(2)),
        Stage(7, 7, WrcStageStatus.ToRun, null),
        Stage(5, 1, WrcStageStatus.ToRun, sameStart),
        Stage(4, 1, WrcStageStatus.Unknown, sameStart),
        Stage(3, 2, WrcStageStatus.ToRun, sameStart)
    ];
    Equal(4, WrcTiming.SelectNextScheduledStage(stages, now)?.StageId);

    var options = TestWrcOptions() with
    {
        PollInterval = TimeSpan.FromSeconds(15),
        IdlePollInterval = TimeSpan.FromMinutes(5),
        StageStartLead = TimeSpan.FromMinutes(1)
    };
    var metadataRefresh = now.AddHours(1);
    var eventEnd = now.AddDays(1);
    Equal(TimeSpan.FromMinutes(5), WrcTiming.NextPollDelay(now, false,
        Stage(1, 1, WrcStageStatus.ToRun, now.AddMinutes(10)), metadataRefresh, eventEnd, options));
    Equal(TimeSpan.FromMinutes(2), WrcTiming.NextPollDelay(now, false,
        Stage(1, 1, WrcStageStatus.ToRun, now.AddMinutes(3)), metadataRefresh, eventEnd, options));
    Equal(TimeSpan.FromSeconds(15), WrcTiming.NextPollDelay(now, false,
        Stage(1, 1, WrcStageStatus.ToRun, now.AddSeconds(30)), metadataRefresh, eventEnd, options));
    Equal(TimeSpan.FromSeconds(15), WrcTiming.NextPollDelay(now, true,
        Stage(1, 1, WrcStageStatus.ToRun, now.AddMinutes(10)), metadataRefresh, eventEnd, options));
    Equal(TimeSpan.FromSeconds(30), WrcTiming.NextPollDelay(now, false, null,
        now.AddSeconds(30), eventEnd, options));
    Equal(TimeSpan.FromSeconds(20), WrcTiming.NextPollDelay(now, false, null,
        metadataRefresh, now.AddSeconds(20), options));
}

static void WrcStageFingerprintOrdering()
{
    var stage = new WrcStage(1, 1, "SS1", "Stage 1", WrcStageStatus.Completed, null,
        new JsonObject { ["stageId"] = 1 });
    WrcStageTime[] times =
    [
        new(2, 2, 121000, 1000, 1000, "Completed"),
        new(1, 1, 120000, 0, 0, "Completed")
    ];
    WrcStageResult[] results =
    [
        new(2, 2, 121000),
        new(1, 1, 120000)
    ];
    var originalTimeOrder = times.Select(time => time.EntryId).ToArray();
    var originalResultOrder = results.Select(result => result.EntryId).ToArray();
    var expected = WrcTiming.StageFingerprint(stage, times, results);
    var reordered = WrcTiming.StageFingerprint(stage, times.Reverse().ToArray(), results.Reverse().ToArray());
    Equal(expected, reordered);
    True(originalTimeOrder.SequenceEqual(times.Select(time => time.EntryId)));
    True(originalResultOrder.SequenceEqual(results.Select(result => result.EntryId)));
}

static void WrcShakedownTiming()
{
    Dictionary<int, WrcEntry> entries = new()
    {
        [1] = new(1, 1, "AAA", "Driver A", "Toyota", "Rally1", "", "RC1", "Entry"),
        [2] = new(2, 2, "BBB", "Driver B", "Hyundai", "Rally1", "", "RC1", "Entry")
    };
    WrcShakedownTime[] times =
    [
        new(1, 1, 121500),
        new(1, 2, 120000),
        new(2, 1, 120500)
    ];
    var standings = WrcTiming.BuildShakedownStandings(entries, times);
    Equal("AAA", standings[0].Code);
    Equal("2:00.0", standings[0].ResultTime);
    Equal("BBB", standings[1].Code);
    Equal("+0:00.5", standings[1].GapToLeader);
}

static void WrcOptionalShakedown()
{
    using var handler = new RouteHttpHandler([]);
    using var api = new WrcApiClient(new HttpClient(handler));
    var times = api.TryGetShakedownTimesAsync(644, 1, CancellationToken.None).GetAwaiter().GetResult();
    Equal(0, times.Count);
    Equal("events/644/shakedowntimes.json?shakedownNumber=1", handler.Paths.Single());
}

/// <summary>
/// The Formula 1 feed keeps publishing between sessions, so the laps, stints and positions of a finished session
/// must not survive into the next one's classification: a practice session used to open on the race's lap counts.
/// </summary>
static void NewSessionClearsPreviousTiming()
{
    var (_, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"SessionInfo":{"Key":11396,"Name":"Race","Type":"Race"},
         "DriverList":{"1":{"Tla":"AAA"}},
         "TimingData":{"Lines":{"1":{"Position":3,"NumberOfLaps":56,"BestLapTime":{"Value":"1:22.000"}}}},
         "TimingAppData":{"Lines":{"1":{"Stints":[{"Compound":"HARD","TotalLaps":25}]}}},
         "LapCount":{"CurrentLap":56,"TotalLaps":56}}
        """)!.AsObject());
    Equal(56, processor.Current.Competitors[0].Lap);
    Equal(25, processor.Current.Competitors[0].TyreAge);
    Equal(3, processor.Current.Competitors[0].Position);

    // The next event's practice announces itself while the finished race is still the whole state.
    processor.ProcessDelta("SessionInfo", JsonNode.Parse("""{"Key":11397,"Name":"Practice 1","Type":"Practice"}""")!.AsObject(), DateTimeOffset.UtcNow);
    Equal(0, processor.Current.Competitors.Count);
    Equal(0, processor.Current.CurrentLap);

    // What the new session publishes replaces it; nothing is inherited from the race.
    processor.ProcessDelta("TimingData", JsonNode.Parse("""{"Lines":{"1":{"Position":1,"NumberOfLaps":2}}}""")!.AsObject(), DateTimeOffset.UtcNow);
    Equal(2, processor.Current.Competitors[0].Lap);
    Equal(null, processor.Current.Competitors[0].TyreAge);
    Equal(1, processor.Current.Competitors[0].Position);

    // A delta for the same session changes nothing about the state that is kept.
    processor.ProcessDelta("SessionInfo", JsonNode.Parse("""{"Key":11397,"Status":"Started"}""")!.AsObject(), DateTimeOffset.UtcNow);
    Equal(2, processor.Current.Competitors[0].Lap);
}

/// <summary>
/// Full course yellow is its own track status. The feed reader must not read the message "FULL COURSE YELLOW" as a
/// plain yellow, and the scenario FCY status must reach the same state the Testing page button selects.
/// </summary>
static void FullCourseYellowStatus()
{
    var (_, _, processor) = Fixture();
    processor.ProcessDelta("TrackStatus", JsonNode.Parse("""{"Status":"FCY","Message":"FULL COURSE YELLOW"}""")!.AsObject(), DateTimeOffset.UtcNow);
    Equal(TrackCondition.FullCourseYellow, processor.Current.TrackCondition);
    processor.ProcessDelta("TrackStatus", JsonNode.Parse("""{"Status":"2","Message":"Yellow flag sector 2"}""")!.AsObject(), DateTimeOffset.UtcNow);
    Equal(TrackCondition.Yellow, processor.Current.TrackCondition);
    processor.ProcessDelta("TrackStatus", JsonNode.Parse("""{"Message":"FULL COURSE YELLOW"}""")!.AsObject(), DateTimeOffset.UtcNow);
    Equal(TrackCondition.FullCourseYellow, processor.Current.TrackCondition);
}

static void F1PracticeTiming()
{
    var (_, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"DriverList":{"1":{"Tla":"AAA"},"2":{"Tla":"BBB"},"3":{"Tla":"CCC"}},
         "TimingData":{"Lines":{
           "1":{"Position":1,"NumberOfLaps":10,"BestLapTime":{"Value":"1:47.000"}},
           "2":{"Position":2,"NumberOfLaps":11,"BestLapTime":{"Value":"1:47.250"}},
           "3":{"Position":3,"NumberOfLaps":9,"BestLapTime":{"Value":"1:47.800"}}}},
         "TimingAppData":{"Lines":{
           "1":{"Stints":[{"Compound":"HARD","TotalLaps":12,"StartLaps":0}]},
           "2":{"Stints":[{"Compound":"MEDIUM","TotalLaps":9,"StartLaps":9},{"Compound":"SOFT","TotalLaps":1,"StartLaps":0}]}}}}
        """)!.AsObject());
    Equal("LEAD", processor.Current.Competitors[0].GapToLeader);
    Equal("+0.250", processor.Current.Competitors[1].GapToLeader);
    Equal("+0.250", processor.Current.Competitors[1].IntervalToPositionAhead);
    Equal("+0.800", processor.Current.Competitors[2].GapToLeader);
    Equal("+0.550", processor.Current.Competitors[2].IntervalToPositionAhead);
    Equal("SOFT", processor.Current.Competitors[1].Tyre);
    Equal(1, processor.Current.Competitors[1].TyreAge);
    Equal(12, processor.Current.Competitors[0].TyreAge);
    // TotalLaps already counts every lap run on the set, so a tyre fitted with laps on it keeps its full age.
    processor.ProcessDelta("TimingAppData", JsonNode.Parse("""{"Lines":{"1":{"Stints":{"1":{"TotalLaps":13,"StartLaps":3}}}}}""")!.AsObject(), DateTimeOffset.UtcNow);
    Equal(13, processor.Current.Competitors[0].TyreAge);
    processor.ProcessDelta("TimingAppData", JsonNode.Parse("""{"Lines":{"2":{"Stints":{"1":{"TotalLaps":5}}}}}""")!.AsObject(), DateTimeOffset.UtcNow);
    Equal(5, processor.Current.Competitors[1].TyreAge);
    Equal("SOFT", processor.Current.Competitors[1].Tyre);
    processor.ProcessDelta("TimingAppData", JsonNode.Parse("""{"Lines":{"2":{"Stints":{"1":{"TotalLaps":2}}}}}""")!.AsObject(), DateTimeOffset.UtcNow);
    Equal(2, processor.Current.Competitors[1].TyreAge);
    processor.ProcessDelta("TimingAppData", JsonNode.Parse("""{"Lines":{"2":{"Stints":{"1":{"StartLaps":1}}}}}""")!.AsObject(), DateTimeOffset.UtcNow);
    Equal(2, processor.Current.Competitors[1].TyreAge);
}

static void F1QualifyingParts()
{
    var (_, _, processor) = Fixture();
    var state = BaseState();
    state["SessionInfo"]!["Name"] = "Qualifying";
    state["SessionInfo"]!["Type"] = "Qualifying";
    // TimingData.SessionPart is the total part count, not the active part. Only SessionData.Series
    // transitions are authoritative for the displayed qualifying part.
    state["TimingData"]!["SessionPart"] = 3;
    state["TimingData"]!["Lines"]!["1"]!["BestLapTime"] = new JsonObject { ["Value"] = "1:42.000" };
    state["TimingData"]!["Lines"]!["2"]!["BestLapTime"] = new JsonObject { ["Value"] = "1:42.500" };
    state["SessionData"] = new JsonObject
    {
        ["Series"] = new JsonArray(new JsonObject { ["QualifyingPart"] = 1 })
    };
    state["SessionStatus"] = new JsonObject { ["Status"] = "Started" };
    processor.ProcessInitial(state);
    Equal("Qualifying 1", processor.Current.Session);
    Equal(SessionLifecycle.Live, processor.Current.Lifecycle);

    processor.ProcessDelta("SessionStatus", new JsonObject { ["Status"] = "Finished" }, DateTimeOffset.UtcNow);
    Equal(SessionLifecycle.Live, processor.Current.Lifecycle);
    processor.ProcessDelta("RaceControlMessages", new JsonObject
    {
        ["Messages"] = new JsonObject { ["1"] = new JsonObject { ["Flag"] = "CHEQUERED", ["Message"] = "SQ1 ENDED" } }
    }, DateTimeOffset.UtcNow);
    Equal(SessionLifecycle.Live, processor.Current.Lifecycle);

    processor.ProcessDelta("SessionData", new JsonObject
    {
        ["Series"] = new JsonObject { ["1"] = new JsonObject { ["QualifyingPart"] = 2 } }
    }, DateTimeOffset.UtcNow);
    processor.ProcessDelta("SessionStatus", new JsonObject { ["Status"] = "Inactive" }, DateTimeOffset.UtcNow);
    Equal("Qualifying 2", processor.Current.Session);
    Equal(SessionLifecycle.Live, processor.Current.Lifecycle);
    processor.ProcessDelta("SessionData", new JsonObject
    {
        ["Series"] = new JsonObject { ["2"] = new JsonObject { ["QualifyingPart"] = 3 } }
    }, DateTimeOffset.UtcNow);
    processor.ProcessDelta("SessionStatus", new JsonObject { ["Status"] = "Started" }, DateTimeOffset.UtcNow);
    Equal("Qualifying 3", processor.Current.Session);
    Equal(SessionLifecycle.Live, processor.Current.Lifecycle);
    processor.ProcessDelta("SessionStatus", new JsonObject { ["Status"] = "Finished" }, DateTimeOffset.UtcNow);
    Equal(SessionLifecycle.Ended, processor.Current.Lifecycle);

    var (_, _, noSeries) = Fixture();
    state = BaseState();
    state["SessionInfo"]!["Name"] = "Sprint Qualifying";
    state["SessionInfo"]!["Type"] = "Qualifying";
    state["TimingData"]!["SessionPart"] = 3;
    noSeries.ProcessInitial(state);
    Equal("Sprint Qualifying", noSeries.Current.Session);
    noSeries.ProcessDelta("SessionData", new JsonObject
    {
        ["Series"] = new JsonArray(new JsonObject { ["QualifyingPart"] = 1 })
    }, DateTimeOffset.UtcNow);
    Equal("Sprint Qualifying 1", noSeries.Current.Session);
}

static void WrcStageTiming()
{
    static WrcEntry Entry(int id, string code, int order) =>
        new(id, order, code, code, "Test", "Rally1", "", "RC1", "Entry");
    Dictionary<int, WrcEntry> entries = new()
    {
        [1] = Entry(1, "AAA", 1),
        [2] = Entry(2, "BBB", 2),
        [3] = Entry(3, "CCC", 3)
    };
    WrcStageTime[] times =
    [
        new(1, 2, 516600, 4100, 2100, "Completed"),
        new(2, 1, 512500, 0, 0, "Completed"),
        new(3, null, null, null, null, "ToRun")
    ];
    var standings = WrcTiming.BuildStageStandings(entries, times);
    Equal("BBB", standings[0].Code);
    Equal("8:32.5", standings[0].ResultTime);
    Equal("AAA", standings[1].Code);
    Equal("+0:04.1", standings[1].GapToLeader);
    Equal("+0:02.1", standings[1].IntervalToPositionAhead);
    Equal("8:36.6", standings[1].ResultTime);
    Equal("DUE", standings[2].StatusLabel);
    Equal("—", standings[2].PositionLabel);
}

static void WrcFinalResultsEndStage()
{
    Dictionary<int, WrcEntry> entries = new()
    {
        [1] = new(1, 1, "AAA", "A", "Team", "Rally1", "", "RC1", "Entry"),
        [2] = new(2, 2, "BBB", "B", "Team", "Rally1", "", "RC1", "Entry")
    };
    WrcStageTime[] active =
    [
        new(1, 1, 1000, 0, 0, "Completed"),
        new(2, null, null, null, null, "Running")
    ];
    WrcStageTime[] complete =
    [
        active[0],
        new(2, 2, 1100, 100, 100, "Completed")
    ];
    False(WrcTiming.Wrc1ResultsComplete(entries, [active[0]], [new(1, 1, 1000)]));
    False(WrcTiming.Wrc1ResultsComplete(entries, active, [new(1, 1, 1000)]));
    False(WrcTiming.Wrc1ResultsComplete(entries, complete, [new(1, 1, 1000)]));
    True(WrcTiming.Wrc1ResultsComplete(entries, complete,
        [new(1, 1, 1000), new(2, 2, 1100)]));
}

static void Wrc1Completion()
{
    Dictionary<int, WrcEntry> entries = new()
    {
        [1] = new(1, 1, "AAA", "A", "Team", "Rally1", "", "RC1", "Entry"),
        [2] = new(2, 2, "BBB", "B", "Team", "Rally2", "WRC2", "RC2", "Entry")
    };
    WrcStageTime[] times =
    [
        new(1, 1, 1000, 0, 0, "Completed"),
        new(2, 2, 1100, 100, 100, "Completed")
    ];
    True(WrcTiming.Wrc1ResultsComplete(entries, times, [new(1, 1, 1000)]));
}

static void F3QualifyingSnapshot()
{
    var clock = new FakeClock();
    Equal("https://ltss.fiaformula3.com/streaming", F2LiveTimingSource.BaseUrlFor("F3"));
    var source = new F2LiveTimingSource(clock, new AlertArbiter(clock), "F3");
    TimingSnapshot? snapshot = null;
    source.SnapshotReceived += value => snapshot = value;
    var initial = new JsonObject
    {
        ["data"] = new JsonArray("2026-07-17T13:22:51.578", new JsonObject { ["Series"] = "F3", ["Session"] = "Qualifying" }, new JsonObject
        {
            ["1"] = new JsonObject
            {
                ["position"] = new JsonObject { ["Show"] = 1, ["Value"] = "1" },
                ["driver"] = new JsonObject { ["RacingNumber"] = "1", ["FullName"] = "Test Driver", ["TLA"] = "TST" },
                ["gapP"] = new JsonObject { ["Value"] = "" },
                ["best"] = new JsonObject { ["Value"] = "2:05.150" }
            }
        }),
        ["racedetailsfeed"] = new JsonArray("2026-07-17T12:55:07.261", new JsonObject { ["Race"] = "Spa-Francorchamps", ["Circuit"] = "Spa", ["Session"] = "Qualifying" }),
        ["sessionfeed"] = new JsonArray("2026-07-17T13:20:22.955", new JsonObject { ["Value"] = "Aborted" })
    };
    source.ProcessMessage(new JsonObject { ["I"] = 1, ["R"] = initial }.ToJsonString());
    True(snapshot is not null);
    Equal("Spa-Francorchamps", snapshot!.Meeting);
    Equal("Qualifying", snapshot.Session);
    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal("TST", snapshot.Competitors[0].Code);
}

static void F1AcademyLiveTiming()
{
    var clock = new FakeClock();
    Equal("https://f2f3-prod-livetiming.azurewebsites.net/streaming", F2LiveTimingSource.BaseUrlFor("F1 Academy"));
    Equal("https://www.f1academy.com", F2LiveTimingSource.OriginFor("F1 Academy"));
    var source = new F2LiveTimingSource(clock, new AlertArbiter(clock), "F1 Academy");
    TimingSnapshot? snapshot = null;
    source.SnapshotReceived += value => snapshot = value;
    var initial = new JsonObject
    {
        ["data"] = new JsonArray("2026-10-24T23:00:00Z", new JsonObject { ["Series"] = "F1 Academy", ["Session"] = "Race" }, new JsonArray
        {
            new JsonObject
            {
                ["position"] = new JsonObject { ["Value"] = "1" },
                ["driver"] = new JsonObject { ["RacingNumber"] = "14", ["FullName"] = "Test Driver", ["TLA"] = "TST" },
                ["gap"] = new JsonObject { ["Value"] = "" },
                ["interval"] = new JsonObject { ["Value"] = "" },
                ["best"] = new JsonObject { ["Value"] = "1:58.100" },
                ["last"] = new JsonObject { ["Value"] = "1:58.400" },
                ["sectors"] = new JsonArray(
                    new JsonObject { ["Id"] = "1", ["Value"] = "36.0", ["OverallFastest"] = 1, ["PersonalFastest"] = 1 },
                    new JsonObject { ["Id"] = "2", ["Value"] = "51.4", ["OverallFastest"] = 0, ["PersonalFastest"] = 1 },
                    new JsonObject { ["Id"] = "3", ["Value"] = "", ["OverallFastest"] = 0, ["PersonalFastest"] = 0 }),
                ["laps"] = new JsonObject { ["Value"] = "8" }
            },
            new JsonObject
            {
                ["position"] = new JsonObject { ["Value"] = "2" },
                ["driver"] = new JsonObject { ["RacingNumber"] = "3", ["FullName"] = "Second Driver (WCD)", ["TLA"] = "SEC" },
                ["gap"] = new JsonObject { ["Value"] = "+1.2" },
                ["interval"] = new JsonObject { ["Value"] = "+1.2" },
                ["best"] = new JsonObject { ["Value"] = "1:58.900" },
                ["laps"] = new JsonObject { ["Value"] = "8" }
            }
        }),
        ["racedetailsfeed"] = new JsonArray("2026-10-24T23:00:00Z", new JsonObject
        {
            ["Season"] = "2026 F1 Academy", ["Race"] = "Austin", ["Circuit"] = "Circuit of the Americas", ["Session"] = "Reverse Grid Race"
        }),
        ["sessionfeed"] = new JsonArray("2026-10-24T23:00:00Z", new JsonObject { ["Value"] = "Started" })
    };
    source.ProcessMessage(new JsonObject { ["I"] = 1, ["R"] = initial }.ToJsonString());
    True(snapshot is not null);
    Equal(Championship.F1Academy, snapshot!.Championship);
    Equal("United States Grand Prix", snapshot.Meeting);
    Equal("Reverse Grid Race", snapshot.Session);
    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal("LEAD", snapshot.Competitors[0].GapToLeader);
    Equal("+1.2", snapshot.Competitors[1].IntervalToPositionAhead);
    Equal(8, snapshot.CurrentLap);
    // The feed names no team; the 2026 entry list does, so team names and logos resolve from the car number.
    Equal("ART Grand Prix", snapshot.Competitors[0].Team);
    Equal("MP Motorsport", snapshot.Competitors[1].Team);
    // The logo shown is the driver's F1 backer, as on the series' own graphics.
    Equal("Gatorade", snapshot.Competitors[0].Manufacturer);
    Equal("Alpine", snapshot.Competitors[1].Manufacturer);
    // A wild-card entry's "(WCD)" tag is not part of the name the broadcast shows.
    Equal("Second Driver", snapshot.Competitors[1].Name);
    Equal("F1A United States GP", ChampionshipDisplay.CompactEvent(snapshot));
    // A race shows its lap like F1 does, not the countdown the feed also publishes.
    Equal("RAC LAP 8", TaskbarSessionDisplay.Progress(snapshot with { TimeRemaining = "00:09:39" }));
    // Best and last laps and the sectors run so far are published; a blank sector has not been run yet.
    Equal("1:58.100", snapshot.Competitors[0].ResultTime);
    Equal("1:58.400", snapshot.Competitors[0].LastLapTime);
    Equal(new SectorTiming("36.0", SectorTimingState.OverallBest), snapshot.Competitors[0].CurrentSectors?.Sector1);
    Equal(new SectorTiming("51.4", SectorTimingState.PersonalBest), snapshot.Competitors[0].CurrentSectors?.Sector2);
    Equal(null, snapshot.Competitors[0].CurrentSectors?.Sector3);
    Equal(null, snapshot.Competitors[1].CurrentSectors);

    // A delta names only the sector that changed; the others must survive it.
    source.ProcessMessage(new JsonObject
    {
        ["M"] = new JsonArray(new JsonObject
        {
            ["M"] = "datafeed",
            ["A"] = new JsonArray("F1 Academy", "2026-10-24T23:00:01Z", new JsonObject
            {
                ["lines"] = new JsonArray(new JsonObject
                {
                    ["driver"] = new JsonObject { ["RacingNumber"] = "14" },
                    ["sectors"] = new JsonArray(new JsonObject { ["Id"] = "3", ["Value"] = "41.6", ["OverallFastest"] = 0, ["PersonalFastest"] = 0 })
                })
            })
        })
    }.ToJsonString());
    Equal(new SectorTiming("36.0", SectorTimingState.OverallBest), snapshot.Competitors[0].CurrentSectors?.Sector1);
    Equal(new SectorTiming("41.6", SectorTimingState.Slower), snapshot.Competitors[0].CurrentSectors?.Sector3);

    source.ProcessMessage(new JsonObject
    {
        ["M"] = new JsonArray(new JsonObject
        {
            ["M"] = "datafeed",
            ["A"] = new JsonArray("F1 Academy", "2026-10-24T23:00:01Z", new JsonArray(new JsonObject
            {
                ["position"] = new JsonObject { ["Value"] = "2" },
                ["driver"] = new JsonObject { ["RacingNumber"] = "14" }
            })
        )
    })
}.ToJsonString());
    Equal(2, snapshot.Competitors[0].Position);
}

/// <summary>F1 Academy keys a full field by display slot while deltas name the car: both must land on one line.</summary>
static void F1AcademyLinesConvergeOnCarNumber()
{
    var clock = new FakeClock();
    var source = new F2LiveTimingSource(clock, new AlertArbiter(clock), "F1 Academy");
    TimingSnapshot? snapshot = null;
    source.SnapshotReceived += value => snapshot = value;

    // A full field keyed by display slot, each line naming its car.
    source.ProcessMessage(new JsonObject
    {
        ["M"] = new JsonArray(new JsonObject
        {
            ["M"] = "datafeed",
            ["A"] = new JsonArray("F1 Academy", "2026-10-24T23:00:01Z", new JsonObject
            {
                ["lines"] = new JsonObject
                {
                    ["1"] = new JsonObject
                    {
                        ["position"] = new JsonObject { ["Value"] = "1" },
                        ["driver"] = new JsonObject { ["RacingNumber"] = "14", ["TLA"] = "TST", ["FullName"] = "Test Driver" }
                    },
                    ["2"] = new JsonObject
                    {
                        ["position"] = new JsonObject { ["Value"] = "2" },
                        ["driver"] = new JsonObject { ["RacingNumber"] = "3", ["TLA"] = "SEC", ["FullName"] = "Second Driver" }
                    }
                }
            })
        })
    }.ToJsonString());
    Equal(2, snapshot!.Competitors.Count);

    // A pair delta keyed by a display slot still carries the car, so it merges onto that car's standing.
    source.ProcessMessage(new JsonObject
    {
        ["M"] = new JsonArray(new JsonObject
        {
            ["M"] = "datafeed",
            ["A"] = new JsonArray("F1 Academy", "2026-10-24T23:00:02Z", new JsonObject
            {
                ["lines"] = new JsonArray(new JsonArray("1", new JsonObject
                {
                    ["driver"] = new JsonObject { ["RacingNumber"] = "14" },
                    ["position"] = new JsonObject { ["Value"] = "3" }
                }))
            })
        })
    }.ToJsonString());
    Equal(2, snapshot!.Competitors.Count);
    Equal("14", snapshot.Competitors[1].DriverId);
    Equal(3, snapshot.Competitors[1].Position);

    // A car-keyed delta lands on the same line again.
    source.ProcessMessage(new JsonObject
    {
        ["M"] = new JsonArray(new JsonObject
        {
            ["M"] = "datafeed",
            ["A"] = new JsonArray("F1 Academy", "2026-10-24T23:00:03Z", new JsonArray(new JsonObject
            {
                ["driver"] = new JsonObject { ["RacingNumber"] = "14" },
                ["last"] = new JsonObject { ["Value"] = "1:59.000" }
            }))
        })
    }.ToJsonString());
    Equal(2, snapshot!.Competitors.Count);
    Equal("1:59.000", snapshot.Competitors.Single(standing => standing.DriverId == "14").LastLapTime);
}

static void MotoGpLitePayload()
{
    var now = new DateTimeOffset(2026, 8, 7, 13, 22, 40, TimeSpan.Zero);
    var payload = JsonNode.Parse("""
        {
          "head": {
            "category": "Moto3", "circuit_name": "Silverstone Circuit",
            "event_tv_name": "Qatar Airways Grand Prix of Great Britain",
            "num_laps": 0, "session_name": "Practice", "session_shortname": "PR", "remaining": "1658", "session_status_id": "S"
          },
          "rider": {
            "1": {
              "order": 1, "rider_id": 10424, "status_name": "CL", "rider_number": "22", "rider_shortname": "Almansa", "pos": 1,
              "rider_name": "David", "rider_surname": "ALMANSA", "lap_time": "2'10.057", "num_lap": 2,
              "team_name": "Liqui Moly Dynavolt Intact GP", "bike_name": "KTM", "gap_first": "0.000", "gap_prev": "0.000", "on_pit": false
            },
            "2": {
              "order": 2, "rider_id": 10506, "status_name": "CL", "rider_number": "51", "pos": 2,
              "rider_name": "Brian", "rider_surname": "URIARTE", "lap_time": "2'10.149", "num_lap": 3,
              "team_name": "Red Bull KTM Ajo", "bike_name": "KTM", "gap_first": "0.092", "gap_prev": "0.092", "on_pit": true
            },
            "3": {
              "order": 3, "rider_id": 10291, "status_name": "NC", "rider_number": "5", "pos": -1,
              "rider_name": "Leo", "rider_surname": "RAMMERSTORFER", "lap_time": "0.000", "num_lap": 0,
              "team_name": "SIC58 Squadra Corse", "bike_name": "Honda", "gap_first": "0.000", "gap_prev": "0.000", "on_pit": false
            }
          }
        }
        """)!.AsObject();

    var snapshot = MotoGpTiming.BuildSnapshot(payload, now, ConnectionState.Connected);
    True(snapshot is not null);
    Equal(Championship.Moto3, snapshot!.Championship);
    Equal("Qatar Airways Grand Prix of Great Britain", snapshot.Meeting);
    Equal("Moto3 Practice", snapshot.Session);
    Equal("PR", snapshot.SessionShortName);
    Equal("Silverstone Circuit", snapshot.Circuit);
    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal(ConnectionState.Connected, snapshot.ConnectionState);
    Equal("27:38", snapshot.TimeRemaining);
    Equal("FP 27:38", TaskbarSessionDisplay.Progress(snapshot));
    Equal(3, snapshot.CurrentLap);
    Equal<int?>(null, snapshot.TotalLaps);
    Equal(3, snapshot.Competitors.Count);
    Equal("ALM", snapshot.Competitors[0].Code);
    Equal("David ALMANSA", snapshot.Competitors[0].Name);
    Equal("LEAD", snapshot.Competitors[0].GapToLeader);
    True(snapshot.Competitors[0].IsOverallFastest);
    Equal("KTM", snapshot.Competitors[0].Manufacturer);
    Equal("+0.092", snapshot.Competitors[1].GapToLeader);
    Equal("+0.092", snapshot.Competitors[1].IntervalToPositionAhead);
    True(snapshot.Competitors[1].InPit);
    Equal(3, snapshot.Competitors[2].Position);
    Equal("NC", snapshot.Competitors[2].PositionLabel);
    False(snapshot.Competitors[2].Retired);
    Equal(SessionLifecycle.Ended, MotoGpTiming.SessionLifecycleFor("F"));
    Equal(Championship.MotoGp, MotoGpTiming.ChampionshipForCategory("MotoGP"));
    Equal(Championship.Moto2, MotoGpTiming.ChampionshipForCategory("Moto2"));
    Equal(Championship.Moto3, MotoGpTiming.ChampionshipForCategory("Moto3"));
    Equal(Championship.Unknown, MotoGpTiming.ChampionshipForCategory("BWC"));
    Equal("KTM", MotoGpTiming.ManufacturerFor(null, "Red Bull KTM Ajo"));
    Equal("Ducati", MotoGpTiming.ManufacturerFor(null, "BK8 Gresini Racing MotoGP"));
    Equal("Ducati", MotoGpTiming.ManufacturerFor("Desmosedici GP", "Pertamina Enduro VR46 Racing Team"));
    Equal("Aprilia", MotoGpTiming.ManufacturerFor("RS-GP", "SuperFile Trackhouse MotoGP Team"));
    Equal("Honda", MotoGpTiming.ManufacturerFor("Honda", "Red Bull KTM Ajo"));
    var tiedPayload = (JsonObject)payload.DeepClone();
    tiedPayload["rider"]!["2"]!["lap_time"] = "2'10.057";
    var tied = MotoGpTiming.BuildSnapshot(tiedPayload, now, ConnectionState.Connected)!;
    Equal(1, tied.Competitors.Count(standing => standing.IsOverallFastest));
    True(tied.Competitors[0].IsOverallFastest);
    Equal("https://api.pulselive.motogp.com/motogp/v1/timing-gateway/livetiming-lite", MotoGpLiveTimingSource.Endpoint);

    UpcomingSession[] scheduled = [new(Championship.Moto3, "Test GP", "Practice", now.AddHours(4))];
    Equal(TimeSpan.FromHours(3.5), MotoGpLiveTimingSource.IdleDelay(
        scheduled, Championship.Moto3, now, TimeSpan.FromSeconds(5)));
    Equal(TimeSpan.FromSeconds(5), MotoGpLiveTimingSource.IdleDelay(
        scheduled, Championship.Moto3, now.AddHours(3.75), TimeSpan.FromSeconds(5)));
    Equal(TimeSpan.FromHours(6), MotoGpLiveTimingSource.IdleDelay(
        scheduled, Championship.MotoGp, now, TimeSpan.FromSeconds(5)));
}

static void MotoGpSourceLifecycle()
{
    const string body = """
        {
          "head": {
            "category": "Moto3", "circuit_name": "Silverstone Circuit", "event_tv_name": "British Grand Prix",
            "num_laps": 18, "session_name": "Race", "session_shortname": "RAC", "remaining": "120", "session_status_id": "S"
          },
          "rider": {
            "1": {
              "order": 1, "rider_id": 10424, "status_name": "CL", "rider_number": "22", "rider_shortname": "Almansa", "pos": 1,
              "rider_name": "David", "rider_surname": "ALMANSA", "lap_time": "2'10.057", "num_lap": 3,
              "team_name": "Liqui Moly Dynavolt Intact GP", "bike_name": "KTM", "gap_first": "0.000", "gap_prev": "0.000", "on_pit": false
            }
          }
        }
        """;
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 7, 14, 0, 0, TimeSpan.Zero));
    var alerts = new AlertArbiter(clock);
    var delay = new BlockingDelay();
    using var http = new HttpClient(new StaticJsonHandler(body));
    var source = new MotoGpLiveTimingSource(http, clock, delay, alerts, TimeSpan.FromHours(1), targetChampionship: Championship.Moto3);
    List<TimingSnapshot> snapshots = [];
    List<ConnectionState> connections = [];
    var deltas = 0;
    source.SnapshotReceived += snapshots.Add;
    source.ConnectionChanged += connections.Add;
    source.DeltaReceived += _ => deltas++;

    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    Equal(SessionLifecycle.Live, snapshots.Last().Lifecycle);
    Equal(Championship.Moto3, snapshots.Last().Championship);
    Equal(AlertKind.SessionStart, alerts.Current?.Kind);
    Equal(1, deltas);

    var ended = JsonNode.Parse(body)!.AsObject();
    ended["head"]!["session_status_id"] = "F";
    source.ProcessPayload(ended);
    Equal(SessionLifecycle.Ended, snapshots.Last().Lifecycle);
    Equal(AlertKind.Chequered, alerts.Current?.Kind);
    Equal(2, deltas);

    var otherCategory = JsonNode.Parse(body)!.AsObject();
    otherCategory["head"]!["category"] = "Moto2";
    source.ProcessPayload(otherCategory);
    Equal(SessionLifecycle.OffSession, snapshots.Last().Lifecycle);
    Equal(2, deltas);

    var unsupportedCategory = JsonNode.Parse(body)!.AsObject();
    unsupportedCategory["head"]!["category"] = "BWC";
    source.ProcessPayload(unsupportedCategory);
    Equal(SessionLifecycle.OffSession, snapshots.Last().Lifecycle);
    Equal(2, deltas);

    source.StopAsync().GetAwaiter().GetResult();
    Equal(ConnectionState.Connecting, connections[0]);
    Equal(ConnectionState.Connected, connections[1]);
    Equal(ConnectionState.Disconnected, connections[^1]);
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void F3RosterTeams() => FiaRosterTeams("F3", "Formula 3 2026", Championship.Formula3,
    ("1", "NAE", "Campos Racing"), ("14", "YAM", "Van Amersfoort Racing"), ("29", "LAC", "DAMS Lucas Oil"));

static void F2RosterTeams() => FiaRosterTeams("F2", "Formula 2 2026", Championship.Formula2,
    ("7", "BEG", "DAMS Lucas Oil"), ("9", "MIN", "MP Motorsport"), ("22", "VAR", "Van Amersfoort Racing"));

static void FiaRosterTeams(string series, string season, Championship championship, params (string Number, string Code, string Team)[] drivers)
{
    var clock = new FakeClock();
    var source = new F2LiveTimingSource(clock, new AlertArbiter(clock), series);
    TimingSnapshot? snapshot = null;
    source.SnapshotReceived += value => snapshot = value;
    var lines = new JsonObject();
    for (var index = 0; index < drivers.Length; index++)
    {
        var driver = drivers[index];
        var position = index + 1;
        lines[driver.Number] = new JsonObject
        {
            ["position"] = new JsonObject { ["Show"] = 1, ["Value"] = position.ToString() },
            ["driver"] = new JsonObject { ["RacingNumber"] = driver.Number, ["FullName"] = driver.Code, ["TLA"] = driver.Code },
            ["gapP"] = new JsonObject { ["Value"] = position == 1 ? "" : "+0.1" },
            ["best"] = new JsonObject { ["Value"] = $"1:{20 + position}.000" }
        };
    }
    var initial = new JsonObject
    {
        ["data"] = new JsonArray("2026-07-24T13:50:00Z", new JsonObject { ["Series"] = series, ["Session"] = "Qualifying" }, lines),
        ["racedetailsfeed"] = new JsonArray("2026-07-24T13:45:00Z", new JsonObject
        {
            ["Season"] = season, ["Race"] = "Budapest", ["Circuit"] = "Hungaroring", ["Session"] = "Qualifying Session"
        }),
        ["sessionfeed"] = new JsonArray("2026-07-24T13:50:00Z", new JsonObject { ["Value"] = "Started" })
    };
    source.ProcessMessage(new JsonObject { ["I"] = 1, ["R"] = initial }.ToJsonString());
    True(snapshot is not null);
    Equal(championship, snapshot!.Championship);
    foreach (var driver in drivers)
        Equal(driver.Team, snapshot.Competitors.Single(x => x.Code == driver.Code).Team);
}

/// <summary>
/// Manufacturer and team marks start on in both surfaces, for every series the capabilities do not rule out. A choice
/// the user saved is kept, so the default only reaches settings that never stored one.
/// </summary>
static void LogoDefaultsFollowTheUser()
{
    True(new ChampionshipDisplaySettings().FlyoutUseManufacturerLogos);
    True(new UserSettings().DisplayFor(Championship.Formula1).FlyoutUseManufacturerLogos);
    True(new UserSettings().DisplayFor(Championship.F1Academy).FlyoutUseManufacturerLogos);
    True(new UserSettings().DisplayFor(Championship.F1Academy).TaskbarUseManufacturerLogos);
    False(new UserSettings { ChampionshipDisplay = new ChampionshipDisplayProfiles
    {
        Formula1 = new ChampionshipDisplaySettings { FlyoutUseManufacturerLogos = false }
    } }.DisplayFor(Championship.Formula1).FlyoutUseManufacturerLogos);
}

/// <summary>Energy is read alongside lap time: it sits directly after it and before the sector columns.</summary>
static void EnergyFollowsLapTime()
{
    var order = new ChampionshipDisplaySettings().FlyoutColumnOrder.ToList();
    Equal(order.IndexOf(FlyoutColumn.LapTime) + 1, order.IndexOf(FlyoutColumn.Energy));
    True(order.IndexOf(FlyoutColumn.Energy) < order.IndexOf(FlyoutColumn.Sectors));
}

static void FlyoutColumnOrderNormalization()
{
    Equal(3, (int)FlyoutColumn.Tyre);
    Equal(5, (int)FlyoutColumn.Status);
    Equal(6, (int)FlyoutColumn.Sectors);
    Equal(7, (int)FlyoutColumn.Splits);
    Equal(8, (int)FlyoutColumn.Energy);
    var defaults = new ChampionshipDisplaySettings();
    False(defaults.FlyoutShowSectorTimes);
    False(defaults.FlyoutShowSplitTimes);
    Equal(SectorTimeMode.CurrentLast, defaults.FlyoutSectorTimeMode);
    var configured = new UserSettings
    {
        ChampionshipDisplay = new ChampionshipDisplayProfiles()
            .With(Championship.Formula1, defaults with { FlyoutShowSectorTimes = true, FlyoutShowSplitTimes = true })
            .With(Championship.Formula2, defaults with { FlyoutShowSectorTimes = true, FlyoutShowSplitTimes = true })
            .With(Championship.GtWorldChallengeEurope, defaults with { FlyoutShowSectorTimes = true, FlyoutShowSplitTimes = true })
            .With(Championship.WorldEnduranceChampionship, defaults with { FlyoutShowSectorTimes = true, FlyoutShowSplitTimes = true })
            .With(Championship.EuropeanLeMansSeries, defaults with { FlyoutShowSectorTimes = true })
            .With(Championship.WorldRallyChampionship, defaults with { FlyoutShowSplitTimes = true })
    };
    True(configured.DisplayFor(Championship.Formula1).FlyoutShowSectorTimes);
    False(configured.DisplayFor(Championship.Formula1).FlyoutShowSplitTimes);
    False(configured.DisplayFor(Championship.Formula2).FlyoutShowSectorTimes);
    False(configured.DisplayFor(Championship.Formula2).FlyoutShowSplitTimes);
    True(configured.DisplayFor(Championship.WorldRallyChampionship).FlyoutShowSplitTimes);
    foreach (var championship in new[] { Championship.GtWorldChallengeEurope, Championship.WorldEnduranceChampionship })
    {
        True(configured.DisplayFor(championship).FlyoutShowSectorTimes);
        False(configured.DisplayFor(championship).FlyoutShowSplitTimes);
        Equal(championship == Championship.WorldEnduranceChampionship, configured.DisplayFor(championship).FlyoutShowTyre);
        False(new UserSettings().DisplayFor(championship).FlyoutShowSectorTimes);
    }
    False(configured.DisplayFor(Championship.EuropeanLeMansSeries).FlyoutShowSectorTimes);

    // A hand-edited order that repeats a column, names one that no longer exists, and omits four others.
    var repaired = new ChampionshipDisplaySettings
    {
        FlyoutColumnOrder = [FlyoutColumn.Status, FlyoutColumn.Status, (FlyoutColumn)99, FlyoutColumn.Tyre]
    }.Normalize().FlyoutColumnOrder;
    // The columns that were named keep their position, then the missing ones follow in the canonical order.
    var missing = ChampionshipDisplaySettings.DefaultColumnOrder.Where(column => column is not (FlyoutColumn.Status or FlyoutColumn.Tyre));
    Equal("Status,Tyre," + string.Join(",", missing), string.Join(",", repaired));

    var canonical = string.Join(",", ChampionshipDisplaySettings.DefaultColumnOrder);
    Equal(canonical, string.Join(",", new ChampionshipDisplaySettings { FlyoutColumnOrder = [] }.Normalize().FlyoutColumnOrder));
    Equal(canonical, string.Join(",", new ChampionshipDisplaySettings { FlyoutColumnOrder = null! }.Normalize().FlyoutColumnOrder));
    Equal(canonical, string.Join(",", new ChampionshipDisplaySettings().FlyoutColumnOrder));

    // A rearranged order has to survive a real save and load, since it is a newly serialized shape.
    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock.Tests.{Guid.NewGuid():N}");
    var path = Path.Combine(directory, "settings.json");
    try
    {
        var store = new UserSettingsStore(path);
        var order = new[] { FlyoutColumn.Status, FlyoutColumn.LapTime, FlyoutColumn.Sectors, FlyoutColumn.Splits, FlyoutColumn.Tyre, FlyoutColumn.Category, FlyoutColumn.Interval, FlyoutColumn.Gap, FlyoutColumn.Energy };
        var display = new ChampionshipDisplaySettings
        {
            FlyoutColumnOrder = order,
            FlyoutShowSectorTimes = true,
            FlyoutSectorTimeMode = SectorTimeMode.BestLap
        };
        store.Save(new UserSettings
        {
            ChampionshipDisplay = new ChampionshipDisplayProfiles()
                .With(Championship.Formula1, display)
                .With(Championship.GtWorldChallengeEurope, display)
                .With(Championship.WorldEnduranceChampionship, display)
        });
        // The enum is written by name, so the file stays readable to anyone editing it by hand.
        True(File.ReadAllText(path).Contains("\"Status\""));
        var loaded = store.Load().DisplayFor(Championship.Formula1);
        Equal(string.Join(",", order), string.Join(",", loaded.FlyoutColumnOrder));
        True(loaded.FlyoutShowSectorTimes);
        Equal(SectorTimeMode.BestLap, loaded.FlyoutSectorTimeMode);
        foreach (var championship in new[] { Championship.GtWorldChallengeEurope, Championship.WorldEnduranceChampionship })
        {
            var restored = store.Load().DisplayFor(championship);
            True(restored.FlyoutShowSectorTimes);
            Equal(SectorTimeMode.BestLap, restored.FlyoutSectorTimeMode);
            Equal(string.Join(",", order), string.Join(",", restored.FlyoutColumnOrder));
        }
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}

static void EventNameShortening()
{
    Equal("Emilia-Romagna Grand Prix", EventNameShortener.Shorten(Championship.Formula1,
        "FORMULA 1 AWS GRAN PREMIO DEL MADE IN ITALY E DELL'EMILIA-ROMAGNA 2025"));
    Equal("British Grand Prix", EventNameShortener.Shorten(Championship.Formula1,
        "FORMULA 1 QATAR AIRWAYS BRITISH GRAND PRIX 2025"));
    Equal("Barcelona-Catalunya Grand Prix", EventNameShortener.Shorten(Championship.Formula1,
        "FORMULA 1 MSC CRUISES GRAN PREMIO DE BARCELONA-CATALUNYA 2026"));
    Equal("French Grand Prix", EventNameShortener.Shorten(Championship.MotoGp,
        "MICHELIN® GRAND PRIX OF FRANCE "));
    Equal("Catalan Grand Prix", EventNameShortener.Shorten(Championship.Moto2,
        "MONSTER ENERGY GRAND PRIX OF CATALUNYA"));
    Equal("Tokyo E-Prix", EventNameShortener.Shorten(Championship.FormulaE,
        "2026 TDK Tokyo E-Prix"));
    Equal("Rally Japan", EventNameShortener.Shorten(Championship.WorldRallyChampionship,
        "FORUM8 Rally Japan"));
    Equal("Rally Finland", EventNameShortener.Shorten(Championship.WorldRallyChampionship,
        "Secto Rally Finland"));
    Equal("Markham Indy", EventNameShortener.Shorten(Championship.IndyCar,
        "Ontario Honda Dealers Indy at Markham"));
    Equal("Detroit Grand Prix", EventNameShortener.Shorten(Championship.IndyCar,
        "Chevrolet Detroit Grand Prix presented by Lear"));
    Equal("24 Hours of Daytona", EventNameShortener.Shorten(Championship.Imsa,
        "Rolex 24 At Daytona"));
    Equal("6 Hours of The Glen", EventNameShortener.Shorten(Championship.Imsa,
        "Sahlen's Six Hours of The Glen"));
    Equal("Sprint Cup · Round 8", EventNameShortener.Shorten(Championship.GtWorldChallengeEurope,
        "GT WorldChEU pwrd by AWS Sprint Cup Round 8"));
    Equal("Sachsenring", EventNameShortener.Shorten(Championship.Dtm, "07 Sachsenring"));
    Equal("Lone Star Le Mans", EventNameShortener.Shorten(Championship.WorldEnduranceChampionship,
        "FIA WEC - Lone Star Le Mans"));
    Equal("8 Hours of Bahrain", EventNameShortener.Shorten(Championship.WorldEnduranceChampionship,
        "FIA WEC - Bapco Energies 8 Hours of Bahrain"));
    Equal("Silverstone Circuit", EventNameShortener.Shorten(Championship.EuropeanLeMansSeries,
        "ELMS - Silverstone Circuit"));
    Equal("Silverstone Round", EventNameShortener.Shorten(Championship.MichelinLeMansCup,
        "MLMC - Silverstone Round"));
    Equal("An Unknown Racing Festival", EventNameShortener.Shorten(Championship.Unknown,
        "  An   Unknown Racing Festival  "));
}

static void SessionLapTimeMode()
{
    var now = DateTimeOffset.UtcNow;
    LapTimeMode Mode(string session, Championship championship = Championship.Formula1) =>
        ChampionshipDisplay.LapTimeModeFor(new TimingSnapshot("Event", session, "Circuit", 1, null,
            TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: championship));

    Equal(LapTimeMode.BestLap, Mode("Qualifying"));
    Equal(LapTimeMode.BestLap, Mode("Qualifying Nr. 2"));
    Equal(LapTimeMode.BestLap, Mode("Practice 1"));
    Equal(LapTimeMode.BestLap, Mode("Free Practice 2"));
    Equal(LapTimeMode.LastLap, Mode("Race"));
    Equal(LapTimeMode.LastLap, Mode("Grand Prix"));
    Equal(LapTimeMode.LastLap, Mode("Sprint"));
    // Qualifying wins over sprint, so sprint shootouts still report a best lap.
    Equal(LapTimeMode.BestLap, Mode("Sprint Qualifying"));
    // Rally stages carry their own stage time and are never reclassified as last-lap sessions.
    Equal(LapTimeMode.BestLap, Mode("SS1", Championship.WorldRallyChampionship));
    Equal(LapTimeMode.BestLap, Mode("Race", Championship.WorldRallyChampionship));
}

static void AppLogStructuredRecords()
{
    var folder = Path.Combine(Path.GetTempPath(), $"racedock-log-{Guid.NewGuid():N}");
    try
    {
        AppLog.UseFolder(folder);
        AppLog.Info("Feed", "Connected.", new { Championship = "Formula1", Competitors = 20 });
        AppLog.Error("Feed", "Parse failed.", new InvalidDataException("bad frame"));
        AppLog.Close();

        var lines = File.ReadAllLines(Path.Combine(folder, "app.ndjson"));
        Equal(2, lines.Length);

        var first = JsonNode.Parse(lines[0])!.AsObject();
        Equal("Info", JsonSupport.String(first["Level"]));
        Equal("Feed", JsonSupport.String(first["Source"]));
        Equal("Connected.", JsonSupport.String(first["Message"]));
        Equal("Formula1", JsonSupport.String(first["Data"]!["Championship"]));
        Equal(20, JsonSupport.Int(first["Data"]!["Competitors"]));

        // An error keeps the exception detail, which the old string log discarded.
        var second = JsonNode.Parse(lines[1])!.AsObject();
        Equal("Error", JsonSupport.String(second["Level"]));
        True(JsonSupport.String(second["Exception"])!.Contains("bad frame"));

        // Rotation keeps the newest file current and never exceeds the retained count.
        for (var index = 0; index < 24_000; index++) AppLog.Info("Bulk", new string('x', 240));
        AppLog.Info("Marker", "after rotation");
        AppLog.Close();

        True(File.Exists(Path.Combine(folder, "app.ndjson.1")));
        False(File.Exists(Path.Combine(folder, "app.ndjson.3")));
        True(File.ReadAllText(Path.Combine(folder, "app.ndjson")).Contains("after rotation"));
    }
    finally
    {
        AppLog.UseFolder(AppPaths.Logs);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
}

static void AppLogNeverThrows()
{
    var file = Path.Combine(Path.GetTempPath(), $"racedock-log-{Guid.NewGuid():N}.txt");
    try
    {
        // A file where the folder must be: every write fails, and none of them may escape.
        File.WriteAllText(file, "occupied");
        AppLog.UseFolder(file);
        AppLog.Info("Feed", "first");
        AppLog.Error("Feed", "second", new InvalidOperationException("ignored"));
        AppLog.Close();

        // Recovery is automatic once a usable folder exists again.
        var folder = Path.Combine(Path.GetTempPath(), $"racedock-log-{Guid.NewGuid():N}");
        try
        {
            AppLog.UseFolder(folder);
            AppLog.Info("Feed", "third");
            AppLog.Close();
            True(File.ReadAllText(Path.Combine(folder, "app.ndjson")).Contains("third"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    finally
    {
        AppLog.UseFolder(AppPaths.Logs);
        if (File.Exists(file)) File.Delete(file);
    }
}

static void FeedDriftDeduplication()
{
    var folder = Path.Combine(Path.GetTempPath(), $"racedock-drift-{Guid.NewGuid():N}");
    try
    {
        AppLog.UseFolder(folder);
        FeedDrift.ResetForTests();
        MotoGpTiming.BuildSnapshot(new JsonObject(), DateTimeOffset.UtcNow, ConnectionState.Connected);
        MotoGpTiming.BuildSnapshot(new JsonObject(), DateTimeOffset.UtcNow, ConnectionState.Connected);
        MotoGpTiming.BuildSnapshot(new JsonObject { ["head"] = new JsonObject(), ["rider"] = new JsonObject() }, DateTimeOffset.UtcNow, ConnectionState.Connected);
        IndyCarTiming.BuildSnapshot(new JsonObject(), DateTimeOffset.UtcNow, ConnectionState.Connected);
        IndyCarTiming.BuildSnapshot(new JsonObject(), DateTimeOffset.UtcNow, ConnectionState.Connected);
        AppLog.Close();

        var entries = File.ReadAllLines(Path.Combine(folder, "app.ndjson"))
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .ToList();
        Equal(3, entries.Count);
        True(entries.Any(entry => JsonSupport.String(entry["Data"]?["Source"]) == "MotoGP"));
        True(entries.Any(entry => JsonSupport.String(entry["Data"]?["Field"]) == "head"));
        True(entries.Any(entry => JsonSupport.String(entry["Data"]?["Field"]) == "rider"));
        True(entries.Any(entry => JsonSupport.String(entry["Data"]?["Source"]) == "INDYCAR"));
    }
    finally
    {
        FeedDrift.ResetForTests();
        AppLog.UseFolder(AppPaths.Logs);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
}

static void CrashMarkerAnnouncesOnce()
{
    var folder = Path.Combine(Path.GetTempPath(), $"racedock-crash-{Guid.NewGuid():N}");
    try
    {
        CrashMarker.UseFolder(folder);
        // A session that never crashed says nothing.
        Equal(null, CrashMarker.Consume());

        CrashMarker.Set("Object reference not set to an instance of an object.");
        Equal("Object reference not set to an instance of an object.", CrashMarker.Consume());

        // Consuming clears it, so the notice cannot repeat on every later launch.
        Equal(null, CrashMarker.Consume());

        // The last crash wins rather than accumulating.
        CrashMarker.Set("first");
        CrashMarker.Set("second");
        Equal("second", CrashMarker.Consume());
    }
    finally
    {
        CrashMarker.UseFolder(AppPaths.Root);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
}

static void SettingsVersioningAndRecovery()
{
    var folder = Path.Combine(Path.GetTempPath(), $"racedock-settings-{Guid.NewGuid():N}");
    Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "settings.json");
    try
    {
        // A file predating versioning keeps its values and is stamped on the next save.
        File.WriteAllText(path, """{"RotationSeconds":9,"EnableWrc":false}""");
        var store = new UserSettingsStore(path);
        var legacy = store.Load();
        Equal(0, legacy.Version);
        Equal(9, legacy.RotationSeconds);
        False(legacy.EnableWrc);

        store.Save(legacy);
        var stamped = store.Load();
        Equal(UserSettingsStore.CurrentVersion, stamped.Version);
        Equal(9, stamped.RotationSeconds);

        // A file from a newer build is read, not reset: its known fields still apply.
        File.WriteAllText(path, """{"Version":99,"RotationSeconds":7,"SomethingUnknown":true}""");
        Equal(7, store.Load().RotationSeconds);

        // A truncated file falls back to defaults and is moved aside, not deleted.
        File.WriteAllText(path, """{"RotationSeconds":11,"Enab""");
        Equal(5, store.Load().RotationSeconds);
        var kept = Directory.GetFiles(folder, "settings.corrupt-*.json");
        Equal(1, kept.Length);
        True(File.ReadAllText(kept[0]).Contains("\"RotationSeconds\":11"));
        False(File.Exists(path));
    }
    finally
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
}

static void LegacySettingsImport()
{
    var folder = Path.Combine(Path.GetTempPath(), $"racedock-settings-import-{Guid.NewGuid():N}");
    Directory.CreateDirectory(folder);
    var legacyPath = Path.Combine(folder, "old", "settings.json");
    var targetPath = Path.Combine(folder, "new", "settings.json");
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, "{\"RotationSeconds\":13,\"EnableWrc\":false}");
        var imported = new UserSettingsStore(targetPath, legacyPath).Load();
        Equal(13, imported.RotationSeconds);
        False(imported.EnableWrc);
        True(File.Exists(targetPath));
        True(File.ReadAllText(targetPath).Contains($"\"Version\": {UserSettingsStore.CurrentVersion}", StringComparison.Ordinal));
    }
    finally
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
}

static void SharedEntryNumbers()
{
    var standing = new CompetitorStanding("internal-guid", 1, "VAN", "Laurens Vanthoor", "Porsche", null, null,
        null, 12, false, false, false, false) { CarNumber = "006" };
    foreach (var championship in new[] { Championship.WorldEnduranceChampionship, Championship.Imsa,
        Championship.GtWorldChallengeEurope, Championship.EuropeanLeMansSeries, Championship.MichelinLeMansCup,
        Championship.NascarCup, Championship.NascarXfinity, Championship.NascarTrucks })
    {
        Equal("#006  Laurens Vanthoor", ChampionshipDisplay.FlyoutDriverName(championship, standing));
        Equal("#006  Driver Two", ChampionshipDisplay.FlyoutDriverName(championship, standing with { Name = "Driver Two" }));
        Equal("Laurens Vanthoor", ChampionshipDisplay.FlyoutDriverName(championship, standing with { CarNumber = null }));
        Equal("Laurens Vanthoor", ChampionshipDisplay.FlyoutDriverName(championship, standing with { CarNumber = " " }));
        Equal("#006", ChampionshipDisplay.FlyoutDriverName(championship, standing with { Name = "", Code = "006" }));
        Equal("#006  Laurens Vanthoor", ChampionshipDisplay.FlyoutDriverName(championship, standing with { CarNumber = " #006 " }));
    }
    Equal("Laurens Vanthoor", ChampionshipDisplay.FlyoutDriverName(Championship.Formula1, standing));
    Equal("Laurens Vanthoor", ChampionshipDisplay.FlyoutDriverName(Championship.Dtm, standing));
    Equal("Laurens Vanthoor", ChampionshipDisplay.FlyoutDriverName(Championship.IndyCar, standing));
    Equal("VAN", standing.Code);
    Equal("internal-guid", standing.DriverId);
}

static void ChampionshipLabels()
{
    Equal(4, (int)Championship.WorldRallyChampionship);
    Equal(5, (int)Championship.GtWorldChallengeEurope);
    Equal(6, (int)Championship.Imsa);
    Equal(7, (int)Championship.MotoGp);
    Equal(8, (int)Championship.Moto2);
    Equal(9, (int)Championship.Moto3);
    Equal(10, (int)Championship.FormulaE);
    Equal(11, (int)Championship.Dtm);
    Equal(12, (int)Championship.IndyCar);
    Equal(13, (int)Championship.WorldEnduranceChampionship);
    Equal(14, (int)Championship.EuropeanLeMansSeries);
    Equal(15, (int)Championship.MichelinLeMansCup);
    var now = DateTimeOffset.UtcNow;
    var f1 = new TimingSnapshot("Belgian Grand Prix", "Race", "Spa-Francorchamps", 1, 44,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.Formula1);
    var wrc = new TimingSnapshot("Secto Rally Finland", "SS1", "", 0, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.WorldRallyChampionship);
    var gtwc = new TimingSnapshot("Magny-Cours", "Qualifying 1 Group B", "Magny-Cours", 3, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.GtWorldChallengeEurope);
    var imsa = new TimingSnapshot("Motul SportsCar Endurance Grand Prix", "Race", "Road America", 33, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.Imsa);
    var wec = new TimingSnapshot("FIA WEC - Lone Star Le Mans", "Free Practice 1", "Circuit of the Americas", 25, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.WorldEnduranceChampionship);
    var moto3 = new TimingSnapshot("Qatar Airways Grand Prix of Great Britain", "Moto3 Practice", "Silverstone Circuit", 3, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.Moto3);
    var formulaE = new TimingSnapshot("London E-Prix", "Free Practice 2", "London", 22, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.FormulaE);
    var dtm = new TimingSnapshot("Nürburgring", "Race 1", "Nürburgring", 9, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.Dtm);
    var indyCar = new TimingSnapshot("Ontario Honda Dealers Indy at Markham", "Practice 1", "Streets of Markham", 10, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.IndyCar);
    // NASCAR publishes the track's full name, which used to push the venue off a taskbar header.
    var nascar = new TimingSnapshot("Black's Tire 200 Kennametal Pole Qualifying", "Qualifying", "Martinsville Speedway", 1, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.NascarTrucks);
    var nascarGateway = new TimingSnapshot("Enjoy Illinois 300", "Race", "World Wide Technology Raceway", 12, 200,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.NascarCup);
    var nascarDaytona = new TimingSnapshot("DAYTONA 500", "Race", "Daytona International Speedway", 44, 200,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.NascarCup);
    Equal("F1 Belgian GP", ChampionshipDisplay.CompactEvent(f1));
    Equal("Formula 1 · Belgian Grand Prix", ChampionshipDisplay.FullEvent(f1));
    Equal("WRC Finland", ChampionshipDisplay.CompactEvent(wrc));
    Equal("World Rally Championship · Rally Finland", ChampionshipDisplay.FullEvent(wrc));
    Equal("GTWC Magny-Cours", ChampionshipDisplay.CompactEvent(gtwc));
    Equal("GT World Challenge Europe · Magny-Cours", ChampionshipDisplay.FullEvent(gtwc));
    Equal("IMSA SportsCar Endurance GP", ChampionshipDisplay.CompactEvent(imsa));
    Equal("IMSA WeatherTech SportsCar Championship · SportsCar Endurance Grand Prix", ChampionshipDisplay.FullEvent(imsa));
    Equal("WEC Lone Star Le Mans", ChampionshipDisplay.CompactEvent(wec));
    Equal("World Endurance Championship · Lone Star Le Mans", ChampionshipDisplay.FullEvent(wec));
    Equal("ELMS", ChampionshipDisplay.ShortName(Championship.EuropeanLeMansSeries));
    Equal("European Le Mans Series", ChampionshipDisplay.FullName(Championship.EuropeanLeMansSeries));
    Equal("Le Mans Cup", ChampionshipDisplay.ShortName(Championship.MichelinLeMansCup));
    Equal("Michelin Le Mans Cup", ChampionshipDisplay.FullName(Championship.MichelinLeMansCup));
    Equal("HY", ChampionshipDisplay.CompactCategory(Championship.WorldEnduranceChampionship, "HYPERCAR"));
    Equal("LMGT3", ChampionshipDisplay.CompactCategory(Championship.WorldEnduranceChampionship, "LMGT3"));
    Equal("HYPERCAR", ChampionshipDisplay.CompactCategory(Championship.Imsa, "HYPERCAR"));
    Equal("Moto3 British GP", ChampionshipDisplay.CompactEvent(moto3));
    Equal("Moto3 · British Grand Prix", ChampionshipDisplay.FullEvent(moto3));
    Equal("FE London E-Prix", ChampionshipDisplay.CompactEvent(formulaE));
    Equal("Formula E · London E-Prix", ChampionshipDisplay.FullEvent(formulaE));
    Equal("DTM Nürburgring", ChampionshipDisplay.CompactEvent(dtm));
    Equal("DTM · Nürburgring", ChampionshipDisplay.FullEvent(dtm));
    Equal("NASCAR Trucks Black's Tire 200 Kennametal Pole Qualifying", ChampionshipDisplay.CompactEvent(nascar));
    Equal("NASCAR Cup Enjoy Illinois 300", ChampionshipDisplay.CompactEvent(nascarGateway));
    Equal("NASCAR Cup DAYTONA 500", ChampionshipDisplay.CompactEvent(nascarDaytona));
    Equal("INDYCAR Markham Indy", ChampionshipDisplay.CompactEvent(indyCar));
    Equal("NTT INDYCAR SERIES · Markham Indy", ChampionshipDisplay.FullEvent(indyCar));
    // Provider and sponsor boilerplate is shortened consistently everywhere it is presented.
    Equal("Sprint Cup · Round 8", ChampionshipDisplay.ShortEventName(Championship.GtWorldChallengeEurope, "GT WorldChEU pwrd by AWS Sprint Cup Round 8"));
    Equal("San Marino Grand Prix", ChampionshipDisplay.ShortEventName(Championship.MotoGp, "RED Bull Grand Prix of SAN Marino and the Rimini Riviera"));
    Equal("Rally Paraguay", ChampionshipDisplay.ShortEventName(Championship.WorldRallyChampionship, "Ueno Rally Del Paraguay"));
    Equal("Italian Grand Prix", ChampionshipDisplay.ShortEventName(Championship.Formula1, "Italian Grand Prix"));
    Equal("COTA", ChampionshipDisplay.ShortEventName(Championship.WorldEnduranceChampionship, "Circuit of the Americas"));
    Equal("COTA", ChampionshipDisplay.ShortVenue("Circuit of the Americas"));
    // The compact line names the event, so the circuit a session happens to carry no longer reaches it.
    Equal("MotoGP British GP", ChampionshipDisplay.CompactEvent(moto3 with
    {
        Championship = Championship.MotoGp,
        Circuit = "MotoGP Silverstone Circuit"
    }));
}

static void IndexedArrayPatchMerge()
{
    // The timing feeds patch arrays by index, sending only the fields that changed.
    var state = (JsonObject)JsonNode.Parse("""
        {"Lines":{"63":{"Stints":[
            {"Compound":"MEDIUM","TotalLaps":9},
            {"Compound":"SOFT","TotalLaps":1}]}}}
        """)!;
    JsonSupport.DeepMerge(state, (JsonObject)JsonNode.Parse("""
        {"Lines":{"63":{"Stints":{"1":{"TotalLaps":4,"LapTime":"1:11.567"}}}}}
        """)!);

    var stints = (JsonArray)state["Lines"]!["63"]!["Stints"]!;
    Equal(2, stints.Count);
    Equal("MEDIUM", JsonSupport.String(stints[0]!["Compound"]));
    Equal("SOFT", JsonSupport.String(stints[1]!["Compound"]));
    Equal(4, JsonSupport.Int(stints[1]!["TotalLaps"]));
    Equal("1:11.567", JsonSupport.String(stints[1]!["LapTime"]));

    // A patch that grows the array still lands at the requested position.
    JsonSupport.DeepMerge(state, (JsonObject)JsonNode.Parse("""
        {"Lines":{"63":{"Stints":{"2":{"Compound":"HARD"}}}}}
        """)!);
    Equal(3, stints.Count);
    Equal("HARD", JsonSupport.String(stints[2]!["Compound"]));

    // An object with non-index keys is a value, not a patch, so it still replaces.
    JsonSupport.DeepMerge(state, (JsonObject)JsonNode.Parse("""
        {"Lines":{"63":{"Stints":{"Compound":"WET"}}}}
        """)!);
    Equal("WET", JsonSupport.String(state["Lines"]!["63"]!["Stints"]!["Compound"]));
}

static void FeedDumpCapture()
{
    var body = """
        {"head":{"session_status_id":"L","session_name":"Race","event_name":"Test GP"},"rider":{}}
        """;
    var root = Path.Combine(Path.GetTempPath(), $"racedock-dump-{Guid.NewGuid():N}");
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 7, 14, 0, 0, TimeSpan.Zero));
    try
    {
        // A source polling with no dump running writes nothing at all.
        var idleDelay = new BlockingDelay();
        using var idleHttp = new HttpClient(new StaticJsonHandler(body));
        var idle = new MotoGpLiveTimingSource(idleHttp, clock, idleDelay, new AlertArbiter(clock),
            TimeSpan.FromHours(1), targetChampionship: Championship.Moto2);
        idle.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        True(idleDelay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
        idle.StopAsync().GetAwaiter().GetResult();
        False(Directory.Exists(root));

        var folder = FeedDump.Start(root);
        var delay = new BlockingDelay();
        using var http = new HttpClient(new StaticJsonHandler(body));
        var source = new MotoGpLiveTimingSource(http, clock, delay, new AlertArbiter(clock),
            TimeSpan.FromHours(1), targetChampionship: Championship.Moto2);
        source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
        source.StopAsync().GetAwaiter().GetResult();

        // The file is named for the source and holds the payload exactly as the feed sent it.
        using var open = new FileStream(Path.Combine(folder, "moto2.ndjson"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var line = new StreamReader(open).ReadLine()!;
        Equal(body, System.Text.Json.JsonSerializer.Deserialize<FeedDumpEntry>(line).Payload);

        FeedDump.Stop();
        Equal(null, FeedDump.Folder);
    }
    finally
    {
        FeedDump.Stop();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

/// <summary>
/// A recording started while the feed is already connected never sees the initial state, so it opens with the state
/// its source last held instead of being unreplayable.
/// </summary>
static void FeedDumpOpensWithTheConnectedState()
{
    var root = Path.Combine(Path.GetTempPath(), $"racedock-dump-{Guid.NewGuid():N}");
    try
    {
        // The connection's initial state arrived before recording began.
        FeedDump.RememberInitial("formula1", System.Text.Json.JsonSerializer.Serialize(new
        {
            type = 3,
            invocationId = "1",
            result = new
            {
                SessionInfo = new { Name = "Qualifying", Meeting = new { Name = "Mexico City Grand Prix", Circuit = new { ShortName = "Mexico City" } } },
                SessionStatus = new { Status = "Started", Started = "Started" },
                DriverList = new Dictionary<string, object> { ["11"] = new { Tla = "PER" }, ["27"] = new { Tla = "HUL" } },
                TimingData = new { Lines = new Dictionary<string, object>
                {
                    ["11"] = new { Position = 1, NumberOfLaps = 3, BestLapTime = new { Value = "1:17.100" } },
                    ["27"] = new { Position = 2, NumberOfLaps = 3, BestLapTime = new { Value = "1:17.400" } }
                } }
            }
        }));
        var folder = FeedDump.Start(root);
        FeedDump.Record("formula1", "{\"type\":1,\"target\":\"feed\",\"arguments\":[\"LapCount\",{\"CurrentLap\":4}]}\u001e");
        FeedDump.Stop();

        var path = Path.Combine(folder, "formula1.ndjson");
        var source = F1FeedDumpReplaySource.CreateAsync(path, new FakeClock()).GetAwaiter().GetResult();
        try
        {
            Equal("Mexico City Grand Prix · Qualifying", source.ArchiveName);
            Equal(2, source.FrameCount);
            var seen = new List<TimingSnapshot>();
            source.SnapshotReceived += seen.Add;
            source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Equal(SessionLifecycle.Live, seen[0].Lifecycle);
            Equal("PER", seen[0].Competitors[0].Code);
        }
        finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
    finally
    {
        FeedDump.Stop();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void FlyoutBlurSettings()
{
    // The blur is experimental, so a settings file that has never heard of it must not turn it on.
    var defaults = new UserSettings().Normalize();
    False(defaults.FlyoutAcrylicBlur);
    Equal(70, defaults.FlyoutBlurTint);

    True(new UserSettings { FlyoutAcrylicBlur = true }.Normalize().FlyoutAcrylicBlur);

    // The tint is clamped either side: too low stops the classification being readable over a busy
    // desktop, and beyond the ceiling there is no blur left to see.
    Equal(30, new UserSettings { FlyoutBlurTint = 0 }.Normalize().FlyoutBlurTint);
    Equal(95, new UserSettings { FlyoutBlurTint = 400 }.Normalize().FlyoutBlurTint);
    Equal(45, new UserSettings { FlyoutBlurTint = 45 }.Normalize().FlyoutBlurTint);
}

static void ChampionshipVisibility()
{
    var defaults = new UserSettings().Normalize();
    foreach (var championship in UserSettings.DefaultHiddenChampionships)
    {
        False(defaults.IsChampionshipVisible(championship));
        False(defaults.IsChampionshipEnabled(championship));
    }
    True(defaults.IsChampionshipVisible(Championship.Formula1));
    True(defaults.EnableFormula1);
    True(defaults.IsChampionshipVisible(Championship.WorldEnduranceChampionship));
    True(defaults.EnableWec);
    False(defaults.IsChampionshipVisible(Championship.EuropeanLeMansSeries));
    False(defaults.EnableElms);
    False(defaults.IsChampionshipVisible(Championship.MichelinLeMansCup));
    False(defaults.EnableMichelinLeMansCup);

    // A settings file written before visibility existed keeps whatever the user had already enabled.
    var legacy = new UserSettings { EnableFormula2 = true, EnableIndyCar = true }.Normalize();
    True(legacy.IsChampionshipVisible(Championship.Formula2));
    True(legacy.EnableFormula2);
    True(legacy.IsChampionshipVisible(Championship.IndyCar));
    False(legacy.IsChampionshipVisible(Championship.Moto2));

    // Hiding a championship switches it off so nothing is fetched for it.
    var hidden = new UserSettings
    {
        EnableIndyCar = true,
        EnableDtm = true,
        HiddenChampionships = [Championship.IndyCar]
    }.Normalize();
    False(hidden.IsChampionshipVisible(Championship.IndyCar));
    False(hidden.EnableIndyCar);
    True(hidden.IsChampionshipVisible(Championship.Dtm));
    True(hidden.EnableDtm);

    // Any championship can be hidden, including the ones that are visible by default.
    var withoutFormula1 = new UserSettings
    {
        EnableGtWorldChallengeEurope = true,
        HiddenChampionships = [Championship.Formula1]
    }.Normalize();
    False(withoutFormula1.IsChampionshipVisible(Championship.Formula1));
    False(withoutFormula1.EnableFormula1);
    True(withoutFormula1.EnableGtWorldChallengeEurope);

    // Switching everything off is a valid idle state; nothing is turned back on behind the user's back.
    var idle = new UserSettings
    {
        EnableFormula1 = false,
        EnableGtWorldChallengeEurope = false,
        EnableWec = false,
        EnableWrc = false,
        HiddenChampionships = [Championship.Formula1]
    }.Normalize();
    False(idle.HasEnabledStream);
    False(idle.IsChampionshipVisible(Championship.Formula1));
    True(idle.IsChampionshipVisible(Championship.Formula2));

    // Normalizing again keeps the idle state stable.
    var renormalized = idle.Normalize();
    False(renormalized.HasEnabledStream);
    False(renormalized.IsChampionshipVisible(Championship.Formula1));

    // Hiding every championship disables all of them and leaves nothing visible.
    var allHidden = new UserSettings { HiddenChampionships = [.. UserSettings.AllChampionships] }.Normalize();
    False(allHidden.HasEnabledStream);
    False(allHidden.IsChampionshipVisible(Championship.Formula1));
    False(allHidden.EnableFormula1);
    False(allHidden.IsChampionshipVisible(Championship.Imsa));

    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock.Tests.{Guid.NewGuid():N}");
    var path = Path.Combine(directory, "settings.json");
    try
    {
        var store = new UserSettingsStore(path);
        store.Save(new UserSettings { EnableMoto2 = true, HiddenChampionships = [Championship.Dtm] });
        var reloaded = store.Load();
        True(reloaded.IsChampionshipVisible(Championship.Moto2));
        True(reloaded.EnableMoto2);
        False(reloaded.IsChampionshipVisible(Championship.Dtm));
        // An explicit list is authoritative: unlisted championships stay visible even while switched off.
        True(reloaded.IsChampionshipVisible(Championship.IndyCar));
        False(reloaded.EnableIndyCar);

        // Version 1 predates the two Le Mans series, so migration must not expose them unexpectedly.
        File.WriteAllText(path, """{"Version":1,"HiddenChampionships":[]}""");
        var upgraded = store.Load();
        Equal(UserSettingsStore.CurrentVersion, upgraded.Version);
        False(upgraded.IsChampionshipVisible(Championship.EuropeanLeMansSeries));
        False(upgraded.IsChampionshipVisible(Championship.MichelinLeMansCup));

        // Once explicitly shown and enabled, a current settings file preserves that choice.
        store.Save(new UserSettings
        {
            EnableElms = true,
            HiddenChampionships = [Championship.MichelinLeMansCup]
        });
        var enabledElms = store.Load();
        True(enabledElms.IsChampionshipVisible(Championship.EuropeanLeMansSeries));
        True(enabledElms.EnableElms);
        False(enabledElms.IsChampionshipVisible(Championship.MichelinLeMansCup));
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

static void UserSettingsPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock.Tests.{Guid.NewGuid():N}");
    var path = Path.Combine(directory, "settings.json");
    try
    {
        var store = new UserSettingsStore(path);
        True(new UserSettings().AutoStartOnWindowsBoot);
        False(new UserSettings().UseSingleRowTaskbar);
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "{\"EnableFormula1\":true}");
        var legacy = store.Load();
        True(legacy.AutoStartOnWindowsBoot);
        True(legacy.EnableGtWorldChallengeEurope);
        True(legacy.EnableWec);
        False(legacy.EnableImsa);
        store.Save(new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableGtWorldChallengeEurope = false,
            EnableWec = false,
            EnableWrc = false,
            EnableImsa = false,
            DriverCount = 99,
            RotationSeconds = 1,
            CompletedSessionRetentionMinutes = 99,
            UseManufacturerLogos = true,
            AutoStartOnWindowsBoot = false,
            UseSingleRowTaskbar = true
        });
        var loaded = store.Load();
        False(loaded.HasEnabledStream);
        Equal(5, loaded.DriverCount);
        Equal(2, loaded.RotationSeconds);
        Equal(30, loaded.CompletedSessionRetentionMinutes);
        True(loaded.UseManufacturerLogos);
        True(loaded.DisplayFor(Championship.Formula1).TaskbarUseManufacturerLogos);
        True(loaded.DisplayFor(Championship.Formula1).FlyoutShowTyre);
        False(loaded.DisplayFor(Championship.Formula2).TaskbarUseManufacturerLogos);
        False(loaded.DisplayFor(Championship.Formula2).FlyoutUseManufacturerLogos);
        False(loaded.DisplayFor(Championship.Formula2).FlyoutShowTyre);
        False(loaded.DisplayFor(Championship.F1Academy).FlyoutShowTyre);
        // Team logos start on for F1 Academy, including in a file written before it had a profile.
        True(loaded.DisplayFor(Championship.F1Academy).TaskbarUseManufacturerLogos);
        True(loaded.DisplayFor(Championship.F1Academy).FlyoutUseManufacturerLogos);
        True(new UserSettings().Normalize().DisplayFor(Championship.F1Academy).FlyoutUseManufacturerLogos);
        True(new UserSettings
        {
            ChampionshipDisplay = ChampionshipDisplayProfiles.Create(new ChampionshipDisplaySettings { FlyoutShowSectorTimes = true })
        }.Normalize().DisplayFor(Championship.F1Academy).FlyoutShowSectorTimes);
        True(loaded.DisplayFor(Championship.Moto3).FlyoutUseManufacturerLogos);
        False(loaded.DisplayFor(Championship.Moto3).FlyoutShowTyre);
        True(loaded.DisplayFor(Championship.WorldEnduranceChampionship).FlyoutShowTyre);
        False(new UserSettings
        {
            ChampionshipDisplay = new ChampionshipDisplayProfiles
            {
                WorldEnduranceChampionship = new ChampionshipDisplaySettings { FlyoutShowTyre = false }
            }
        }.DisplayFor(Championship.WorldEnduranceChampionship).FlyoutShowTyre);
        False(loaded.DisplayFor(Championship.EuropeanLeMansSeries).FlyoutShowTyre);
        False(loaded.DisplayFor(Championship.MichelinLeMansCup).FlyoutShowTyre);
        False(loaded.DisplayFor(Championship.WorldRallyChampionship).FlyoutShowTeamNames);
        False(loaded.AutoStartOnWindowsBoot);
        True(loaded.UseSingleRowTaskbar);
        False(loaded.EnableGtWorldChallengeEurope);
        var gtwcOnly = new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableGtWorldChallengeEurope = true,
            EnableWec = false,
            EnableWrc = false,
            EnableImsa = false
        }.Normalize();
        False(gtwcOnly.EnableFormula1);
        True(gtwcOnly.EnableGtWorldChallengeEurope);
        var imsaOnly = new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableGtWorldChallengeEurope = false,
            EnableWec = false,
            EnableWrc = false,
            EnableImsa = true
        }.Normalize();
        False(imsaOnly.EnableFormula1);
        True(imsaOnly.EnableImsa);
        var moto3Only = new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableMoto3 = true,
            EnableGtWorldChallengeEurope = false,
            EnableWec = false,
            EnableWrc = false,
            EnableImsa = false
        }.Normalize();
        False(moto3Only.EnableFormula1);
        True(moto3Only.EnableMoto3);
        store.Save(moto3Only);
        var persistedMoto3 = store.Load();
        False(persistedMoto3.EnableFormula1);
        True(persistedMoto3.EnableMoto3);
        var formulaEOnly = new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableGtWorldChallengeEurope = false,
            EnableWec = false,
            EnableWrc = false,
            EnableImsa = false,
            EnableFormulaE = true
        }.Normalize();
        False(formulaEOnly.EnableFormula1);
        True(formulaEOnly.EnableFormulaE);
        var dtmOnly = new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableGtWorldChallengeEurope = false,
            EnableWec = false,
            EnableWrc = false,
            EnableImsa = false,
            EnableDtm = true
        }.Normalize();
        False(dtmOnly.EnableFormula1);
        True(dtmOnly.EnableDtm);
        var indyCarOnly = new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableGtWorldChallengeEurope = false,
            EnableWec = false,
            EnableWrc = false,
            EnableImsa = false,
            EnableIndyCar = true
        }.Normalize();
        False(indyCarOnly.EnableFormula1);
        True(indyCarOnly.EnableIndyCar);
        var wecOnly = new UserSettings
        {
            EnableFormula1 = false,
            EnableGtWorldChallengeEurope = false,
            EnableWec = true,
            EnableWrc = false
        }.Normalize();
        False(wecOnly.EnableFormula1);
        True(wecOnly.EnableWec);

        var formula1Display = new ChampionshipDisplaySettings
        {
            TaskbarCompetitorCount = 2,
            FlyoutCompetitorCount = 18
        };
        var motoGpDisplay = new ChampionshipDisplaySettings
        {
            TaskbarCompetitorCount = 4,
            TaskbarUseManufacturerLogos = false,
            FlyoutCompetitorCount = 12,
            FlyoutShowIntervals = false,
            FlyoutUseManufacturerLogos = true
        };
        store.Save(new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableMotoGp = true,
            EnableGtWorldChallengeEurope = false,
            EnableWec = false,
            EnableWrc = false,
            WidgetPosition = TaskbarWidgetPosition.RightOfStart,
            ChampionshipDisplay = ChampionshipDisplayProfiles.Create(formula1Display)
                .With(Championship.MotoGp, motoGpDisplay)
        });
        var configured = store.Load();
        Equal(TaskbarWidgetPosition.RightOfStart, configured.WidgetPosition);
        Equal(2, configured.DisplayFor(Championship.Formula1).TaskbarCompetitorCount);
        Equal(4, configured.DisplayFor(Championship.MotoGp).TaskbarCompetitorCount);
        False(configured.DisplayFor(Championship.MotoGp).TaskbarUseManufacturerLogos);
        True(configured.DisplayFor(Championship.Formula1).TaskbarUseManufacturerLogos);
        False(configured.DisplayFor(Championship.MotoGp).FlyoutShowIntervals);
        True(configured.DisplayFor(Championship.MotoGp).FlyoutUseManufacturerLogos);

        var championships = new[]
        {
            Championship.Formula1, Championship.Formula2, Championship.Formula3,
            Championship.MotoGp, Championship.Moto2, Championship.Moto3,
            Championship.GtWorldChallengeEurope, Championship.WorldEnduranceChampionship,
            Championship.EuropeanLeMansSeries, Championship.MichelinLeMansCup,
            Championship.WorldRallyChampionship, Championship.Imsa,
            Championship.FormulaE, Championship.Dtm, Championship.IndyCar
        };
        var mappedProfiles = new ChampionshipDisplayProfiles();
        for (var index = 0; index < championships.Length; index++)
            mappedProfiles = mappedProfiles.With(championships[index], new ChampionshipDisplaySettings { FlyoutCompetitorCount = 5 + index });
        for (var index = 0; index < championships.Length; index++)
            Equal(5 + index, mappedProfiles.For(championships[index]).FlyoutCompetitorCount);

        File.WriteAllText(path, """{"EnableFormula1":true,"ChampionshipDisplay":{"Formula1":null,"MotoGp":null}}""");
        var nullProfiles = store.Load();
        Equal(5, nullProfiles.DisplayFor(Championship.Formula1).TaskbarCompetitorCount);
        Equal(20, nullProfiles.DisplayFor(Championship.MotoGp).FlyoutCompetitorCount);
        var invalid = (new ChampionshipDisplaySettings
        {
            TaskbarCompetitorCount = 99,
            FlyoutCompetitorCount = 1
        }).Normalize();
        Equal(5, invalid.TaskbarCompetitorCount);
        Equal(5, invalid.FlyoutCompetitorCount);

        // A first run draws manufacturer marks on both surfaces. A file that predates per-series settings keeps
        // whatever the retired global flag said, so an explicit "off" there is still honoured.
        var fresh = new UserSettings().Normalize();
        True(fresh.DisplayFor(Championship.Formula1).TaskbarUseManufacturerLogos);
        True(fresh.DisplayFor(Championship.Formula1).FlyoutUseManufacturerLogos);
        False(fresh.DisplayFor(Championship.Formula2).TaskbarUseManufacturerLogos);
        False(new UserSettings { UseManufacturerLogos = false }.Normalize().DisplayFor(Championship.Formula1).FlyoutUseManufacturerLogos);
        var legacyGlobal = new UserSettings { UseManufacturerLogos = true }.Normalize();
        True(legacyGlobal.DisplayFor(Championship.Formula1).TaskbarUseManufacturerLogos);
        True(legacyGlobal.DisplayFor(Championship.Formula1).FlyoutUseManufacturerLogos);

        // Motion is on out of the box, and switching it off has to survive a round trip to disk.
        True(fresh.InterfaceAnimations);
        var quiet = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(
            System.Text.Json.JsonSerializer.Serialize(new UserSettings { InterfaceAnimations = false }))!.Normalize();
        False(quiet.InterfaceAnimations);
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

static void StartupLaunchDetection()
{
    True(LaunchArguments.IndicatesStartup(["--startup"]));
    True(LaunchArguments.IndicatesStartup(["/startup"]));
    True(LaunchArguments.IndicatesStartup(["--STARTUP"]));
    True(LaunchArguments.IndicatesStartup(["--verbose", "--startup"]));
    False(LaunchArguments.IndicatesStartup(null));
    False(LaunchArguments.IndicatesStartup([]));
    False(LaunchArguments.IndicatesStartup(["", "  "]));
    False(LaunchArguments.IndicatesStartup(["--startupdelay"]));
}

static void F1CalendarIncludesPractice()
{
    const string payload = """
    {
      "MRData": { "RaceTable": { "Races": [{
        "raceName": "Italian Grand Prix",
        "Circuit": { "circuitName": "Autodromo Nazionale di Monza" },
        "FirstPractice": { "date": "2026-09-04", "time": "10:30:00Z" },
        "SecondPractice": { "date": "2026-09-04", "time": "14:00:00Z" },
        "ThirdPractice": { "date": "2026-09-05", "time": "10:30:00Z" },
        "Qualifying": { "date": "2026-09-05", "time": "14:00:00Z" },
        "date": "2026-09-06", "time": "13:00:00Z"
      }] } }
    }
    """;
    using var http = new HttpClient(new StaticJsonHandler(payload))
    {
        BaseAddress = new Uri("https://api.jolpi.ca/ergast/f1/")
    };
    using var client = new F1ScheduleClient(http);
    var sessions = client.GetSessionsAsync(new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero),
        CancellationToken.None).GetAwaiter().GetResult();

    Equal(5, sessions.Count);
    Equal("Practice 1", sessions[0].Name);
    Equal(new DateTimeOffset(2026, 9, 4, 10, 30, 0, TimeSpan.Zero), sessions[0].Start);
    Equal("Practice 2", sessions[1].Name);
    Equal("Practice 3", sessions[2].Name);
    Equal("Qualifying", sessions[3].Name);
    Equal("Race", sessions[4].Name);
    True(sessions.All(session => session.Venue == "Autodromo Nazionale di Monza"));
}

static void ChampionshipCalendarSelection()
{
    var now = new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    var f1 = ChampionshipCalendars.NextFormula1(
    [
        new("Dutch Grand Prix", "Race", now.AddDays(-2), now.AddDays(-2).AddHours(2), "Circuit Zandvoort"),
        new("Italian Grand Prix", "Qualifying", now.AddDays(9), now.AddDays(9).AddHours(1), "Autodromo Nazionale Monza"),
        new("Italian Grand Prix", "Race", now.AddDays(10), now.AddDays(10).AddHours(2), "Autodromo Nazionale Monza")
    ], now);
    Equal(Championship.Formula1, f1!.Championship);
    Equal("Italian Grand Prix", f1.EventName);
    Equal("Qualifying", f1.SessionName);
    Equal("Autodromo Nazionale Monza", f1.Venue);

    // Tests and launch events share the motorcycle feed and must not be presented as race weekends.
    var motorcycle = ChampionshipCalendars.NextMotorcycleGrandPrix(JsonNode.Parse("""
    [
      { "kind": "TEST", "name": "SEPANG TEST", "date_start": "2026-08-28T08:00:00+00:00", "date_end": "2026-08-28T18:00:00+00:00" },
      { "kind": "MEDIA", "name": "TEAM PRESENTATION", "date_start": "2026-08-29T08:00:00+00:00", "date_end": "2026-08-29T18:00:00+00:00" },
      { "kind": "GP", "name": "GRAND PRIX OF ARAGON", "date_start": "2026-08-30T08:00:00+00:00", "date_end": "2026-09-01T18:00:00+00:00", "circuit": { "name": "MotorLand Aragon" } },
      { "kind": "GP", "name": "GRAND PRIX OF MISANO", "date_start": "2026-09-13T08:00:00+00:00", "date_end": "2026-09-15T18:00:00+00:00" }
    ]
    """) as JsonArray, now);
    Equal(3, motorcycle.Count);
    Equal("Grand Prix of Aragon", motorcycle[0].EventName);
    Equal(Championship.MotoGp, motorcycle[0].Championship);
    Equal(Championship.Moto2, motorcycle[1].Championship);
    Equal(Championship.Moto3, motorcycle[2].Championship);
    // Every class of the weekend is held at the same circuit.
    True(motorcycle.All(item => item.Venue == "MotorLand Aragon"));
    // Without a published timetable every class falls back to the weekend start.
    True(motorcycle.All(item => item.StartUtc == new DateTimeOffset(2026, 8, 30, 8, 0, 0, TimeSpan.Zero)));

    var griiipSessions = JsonNode.Parse("""
    [
      { "name": "Free Practice 2", "startTime": "2026-09-04T21:00:00+00:00", "series": { "name": "FIA World Endurance Championship" }, "event": { "name": "FIA WEC - Lone Star Le Mans", "trackConfig": { "track": { "name": "Circuit of the Americas" } } } },
      { "name": "Free Practice 1", "startTime": "2026-09-11T09:00:00+00:00", "series": { "name": "European Le Mans Series" }, "event": { "name": "ELMS - Silverstone Circuit", "trackConfig": { "track": { "name": "Silverstone" } } } },
      { "name": "Free Practice 1", "startTime": "2026-09-11T05:00:00+00:00", "series": { "name": "Michelin Le Mans Cup" }, "event": { "name": "MLMC - Silverstone Round", "trackConfig": { "track": { "name": "Silverstone" } } } },
      { "name": "Race 2", "startTime": "2026-09-13T12:00:00+00:00", "series": { "name": "DTM" }, "event": { "name": "07 Sachsenring" } },
      { "name": "Free Practice", "startTime": "2026-09-11T07:00:00+00:00", "series": { "name": "DTM" }, "event": { "name": "07 Sachsenring", "trackConfig": { "track": { "name": "Sachsenring" } } } }
    ]
    """) as JsonArray;
    var dtm = ChampionshipCalendars.NextDtm(griiipSessions, now);
    Equal("07 Sachsenring", dtm!.EventName);
    Equal("Free Practice", dtm.SessionName);
    Equal("Sachsenring", dtm.Venue);
    var wec = ChampionshipCalendars.NextWorldEnduranceChampionship(griiipSessions, now);
    Equal(Championship.WorldEnduranceChampionship, wec!.Championship);
    Equal("FIA WEC - Lone Star Le Mans", wec.EventName);
    Equal("Free Practice 2", wec.SessionName);
    Equal("Circuit of the Americas", wec.Venue);
    var elms = ChampionshipCalendars.NextEuropeanLeMansSeries(griiipSessions, now);
    Equal(Championship.EuropeanLeMansSeries, elms!.Championship);
    Equal("ELMS - Silverstone Circuit", elms.EventName);
    Equal("Silverstone", elms.Venue);
    var michelinLeMansCup = ChampionshipCalendars.NextMichelinLeMansCup(griiipSessions, now);
    Equal(Championship.MichelinLeMansCup, michelinLeMansCup!.Championship);
    Equal("MLMC - Silverstone Round", michelinLeMansCup.EventName);
    Equal("Silverstone", michelinLeMansCup.Venue);

    // GT World Challenge publishes day-precision meeting windows in dd.MM.yyyy form.
    const string gtwcSeason = """
    {
      "Meetings": {
        "a": { "Id": "a", "Name": "Round 6", "CountryCode": "FRA", "BeginDate": "10.07.2026", "EndDate": "12.07.2026" },
        "b": { "Id": "b", "Name": "Round 7", "CountryCode": "DEU", "Location": "Nurburgring", "TrackName": "Nurburgring GP", "BeginDate": "28.08.2026", "EndDate": "30.08.2026" },
        "c": { "Id": "c", "Name": "Round 8", "CountryCode": "GBR", "BeginDate": "02.10.2026", "EndDate": "04.10.2026" }
      }
    }
    """;
    // Support races share the meeting timetable and must not be presented as championship sessions.
    const string gtwcSchedule = """
    {
      "Series": { "s1": { "Name": "GTWorldChEU pwrd by AWS Endurance Cup" }, "s2": { "Name": "Lamborghini Super Trofeo" } },
      "Competitions": { "c1": { "SerieId": "s1" }, "c2": { "SerieId": "s2" } },
      "Units": {
        "u0": { "Name": "Bronze Test and Paid Test GT2", "CompetitionId": "c1", "BeginTime": "28.08.2026 14:00:00" },
        "u1": { "Name": "Free Practice 1", "CompetitionId": "c1", "BeginTime": "29.08.2026 10:50:00" },
        "u2": { "Name": "Super Trofeo Race", "CompetitionId": "c2", "BeginTime": "29.08.2026 09:00:00" },
        "u3": { "Name": "Qualifying 1", "CompetitionId": "c1", "BeginTime": "30.08.2026 09:45:00" }
      }
    }
    """;
    var meeting = ChampionshipCalendars.NextGtWorldChallengeMeeting(JsonNode.Parse(gtwcSeason) as JsonObject, now);
    Equal("Nurburgring GP", meeting!.Name);
    Equal(new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero), meeting.StartUtc);

    // Nurburgring runs on central European summer time, so 10:50 local is 08:50 UTC.
    var gtwc = ChampionshipCalendars.NextGtWorldChallenge(meeting, JsonNode.Parse(gtwcSchedule) as JsonObject, now);
    Equal("Nurburgring GP", gtwc.EventName);
    Equal("Free Practice 1", gtwc.SessionName);
    Equal("Nurburgring", gtwc.Venue);
    Equal(new DateTimeOffset(2026, 8, 29, 8, 50, 0, TimeSpan.Zero), gtwc.StartUtc);

    // Swiss Timing uses POR for Portugal, whose summer sessions are one hour ahead of UTC.
    var portimao = ChampionshipCalendars.NextGtWorldChallenge(
        new ChampionshipCalendars.GtwcMeeting("p", "Portimao", "POR", now, "Portimao"),
        JsonNode.Parse(gtwcSchedule) as JsonObject,
        now);
    Equal("Free Practice 1", portimao.SessionName);
    Equal(new DateTimeOffset(2026, 8, 29, 9, 50, 0, TimeSpan.Zero), portimao.StartUtc);

    // Once a session has started the countdown moves to the next one.
    var duringRaceWeekend = new DateTimeOffset(2026, 8, 29, 9, 0, 0, TimeSpan.Zero);
    var runningGtwc = ChampionshipCalendars.NextGtWorldChallenge(
        ChampionshipCalendars.NextGtWorldChallengeMeeting(JsonNode.Parse(gtwcSeason) as JsonObject, duringRaceWeekend)!,
        JsonNode.Parse(gtwcSchedule) as JsonObject,
        duringRaceWeekend);
    Equal("Nurburgring GP", runningGtwc.EventName);
    Equal("Qualifying 1", runningGtwc.SessionName);

    // Without a timetable the round still shows, carrying its meeting date and no session name.
    var withoutSchedule = ChampionshipCalendars.NextGtWorldChallenge(meeting, null, now);
    Equal("Nurburgring GP", withoutSchedule.EventName);
    Equal(null, withoutSchedule.SessionName);
    Equal(meeting.StartUtc, withoutSchedule.StartUtc);

    // Once the closing day has passed the following round takes over.
    var afterRaceWeekend = new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero);
    Equal("Round 8", ChampionshipCalendars.NextGtWorldChallengeMeeting(JsonNode.Parse(gtwcSeason) as JsonObject, afterRaceWeekend)!.Name);

    // A grand prix weekend counts down to each class's own next session.
    const string motorcycleWeekend = """
    [
      { "kind": "GP", "name": "GRAND PRIX OF ARAGON", "date_start": "2026-08-28T08:00:00+00:00", "date_end": "2026-08-30T18:00:00+00:00",
        "broadcasts": [
          { "type": "MEDIA", "name": "Press Conference", "date_start": "2026-08-29T10:00:00+00:00", "category": { "acronym": "MGP" } },
          { "type": "SESSION", "name": "Free Practice Nr. 2", "date_start": "2026-08-29T08:10:00+00:00", "category": { "acronym": "MGP" } },
          { "type": "SESSION", "name": "Qualifying Nr. 1", "date_start": "2026-08-29T12:50:00+00:00", "category": { "acronym": "MGP" } },
          { "type": "SESSION", "name": "Free Practice Nr. 2", "date_start": "2026-08-29T09:25:00+00:00", "category": { "acronym": "MT2" } },
          { "type": "SESSION", "name": "Race Nr. 1", "date_start": "2026-08-29T14:10:00+00:00", "category": { "acronym": "BWC" } }
        ] },
      { "kind": "GP", "name": "GRAND PRIX OF MISANO", "date_start": "2026-09-13T08:00:00+00:00", "date_end": "2026-09-15T18:00:00+00:00" }
    ]
    """;
    var runningMotorcycle = ChampionshipCalendars.NextMotorcycleGrandPrix(JsonNode.Parse(motorcycleWeekend) as JsonArray, duringRaceWeekend);
    Equal("Grand Prix of Aragon", runningMotorcycle[0].EventName);
    Equal("Qualifying Nr. 1", runningMotorcycle[0].SessionName);
    Equal(new DateTimeOffset(2026, 8, 29, 12, 50, 0, TimeSpan.Zero), runningMotorcycle[0].StartUtc);
    // Moto2 keeps its own timetable, and a class without one falls back to the weekend start.
    Equal("Free Practice Nr. 2", runningMotorcycle[1].SessionName);
    Equal(null, runningMotorcycle[2].SessionName);
    Equal(new DateTimeOffset(2026, 8, 28, 8, 0, 0, TimeSpan.Zero), runningMotorcycle[2].StartUtc);

    // A rally counts down to its next stage, and falls back to the rally start without an itinerary.
    var rallyStart = new DateTimeOffset(2026, 8, 28, 3, 0, 0, TimeSpan.Zero);
    var stages = new (string Code, string Name, DateTimeOffset? StartUtc)[]
    {
        ("SS9", "Bella Vista", duringRaceWeekend.AddHours(-2)),
        ("SS10", "Hohenau", duringRaceWeekend.AddHours(4)),
        ("SS11", "Trinidad", duringRaceWeekend.AddHours(5))
    };
    var rally = ChampionshipCalendars.NextWorldRally("Ueno Rally Del Paraguay", rallyStart, stages, duringRaceWeekend, "Encarnacion");
    Equal("Encarnacion", rally.Venue);
    Equal("SS10 Hohenau", rally.SessionName);
    Equal(duringRaceWeekend.AddHours(4), rally.StartUtc);
    Equal(rallyStart, ChampionshipCalendars.NextWorldRally("Ueno Rally Del Paraguay", rallyStart, [], duringRaceWeekend).StartUtc);

    Equal(null, ChampionshipCalendars.NextFormula1([], now));
    Equal(0, ChampionshipCalendars.NextMotorcycleGrandPrix(null, now).Count);
    Equal(null, ChampionshipCalendars.NextDtm(null, now));
    Equal(null, ChampionshipCalendars.NextWorldEnduranceChampionship(null, now));
    Equal(null, ChampionshipCalendars.NextGtWorldChallengeMeeting(null, now));

    Equal("in 3 days", ChampionshipCalendars.DescribeTimeUntil(now.AddDays(3), now));
    Equal("in 1 day", ChampionshipCalendars.DescribeTimeUntil(now.AddDays(1), now));
    Equal("in 5 hours", ChampionshipCalendars.DescribeTimeUntil(now.AddHours(5), now));
    Equal("in 20 minutes", ChampionshipCalendars.DescribeTimeUntil(now.AddMinutes(20), now));
    Equal("under way", ChampionshipCalendars.DescribeTimeUntil(now.AddMinutes(-1), now));
}

static void ReleaseNotesAndUpdateCheck()
{
    const string changelog = """
    # Changelog

    All notable RaceDock changes are recorded here.

    ## [1.0.2] - 2026-08-28

    ### Added

    - An in-app release notes page.
    - An update check for downloaded builds.

    ### Fixed

    - A wrong lead time.

    ## [1.0.1] - 2026-08-26

    ### Changed

    - Sign-in launches stay in the notification area.
    """;

    var notes = ReleaseNotes.Parse(changelog);
    Equal(2, notes.Count);
    Equal("1.0.2", notes[0].Version);
    Equal("2026-08-28", notes[0].Date);
    Equal(2, notes[0].Groups.Count);
    Equal("Added", notes[0].Groups[0].Heading);
    Equal(2, notes[0].Groups[0].Items.Count);
    Equal("An update check for downloaded builds.", notes[0].Groups[0].Items[1]);
    Equal("Fixed", notes[0].Groups[1].Heading);
    Equal("1.0.1", notes[1].Version);

    // The running build reports an informational version, and tags carry a leading v.
    Equal("1.0.2", ReleaseNotes.ForVersion(notes, "1.0.2+abc123")!.Version);
    Equal("1.0.1", ReleaseNotes.ForVersion(notes, "v1.0.1")!.Version);
    Equal(null, ReleaseNotes.ForVersion(notes, "9.9.9"));
    Equal(0, ReleaseNotes.Parse(null).Count);
    Equal(0, ReleaseNotes.Parse("# Changelog\n\nNothing released yet.\n").Count);

    True(UpdateCheck.IsNewer("v1.0.3", "1.0.2"));
    True(UpdateCheck.IsNewer("1.1.0", "1.0.2+abc123"));
    False(UpdateCheck.IsNewer("v1.0.2", "1.0.2"));
    False(UpdateCheck.IsNewer("v1.0.1", "1.0.2"));
    // A prerelease of the running version is not an update, and unreadable values never claim one.
    False(UpdateCheck.IsNewer("v1.0.2-beta.1", "1.0.2"));
    False(UpdateCheck.IsNewer("nightly", "1.0.2"));
    False(UpdateCheck.IsNewer("v1.0.3", "unknown"));
    False(UpdateCheck.IsNewer(null, "1.0.2"));
}

static void NextSessionPresentation()
{
    var now = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    var zone = TimeZoneInfo.Utc;
    UpcomingSession[] sessions =
    [
        new(Championship.Dtm, "Sachsenring", "Race", now.AddDays(4)),
        new(Championship.Formula1, "Italian Grand Prix", "Practice 1", now.AddDays(3)),
        new(Championship.WorldRallyChampionship, "Rally Paraguay", "SS1", now.AddHours(-2))
    ];

    // A session already under way belongs to the live feeds, so the widget looks past it.
    var next = NextSessionDisplay.Next(sessions, now)!;
    Equal(Championship.Formula1, next.Championship);
    Equal(null, NextSessionDisplay.Next([], now));
    Equal(null, NextSessionDisplay.Next([sessions[2]], now));

    Equal("F1 · Italian Grand Prix", NextSessionDisplay.Headline(next));
    Equal("WRC", NextSessionDisplay.Headline(new(Championship.WorldRallyChampionship, "", null, now)));

    // Close sessions count down; distant ones give the local clock time instead.
    Equal("Practice 1 · in 42m", NextSessionDisplay.Detail(next with { StartUtc = now.AddMinutes(42) }, now, zone));
    Equal("Practice 1 · in 3h 05m", NextSessionDisplay.Detail(next with { StartUtc = now.AddMinutes(185) }, now, zone));
    Equal("Practice 1 · Fri 13:30", NextSessionDisplay.Detail(next with { StartUtc = new DateTimeOffset(2026, 9, 4, 13, 30, 0, TimeSpan.Zero) }, now, zone));
    // The month name follows the machine's culture ("Sep" or "Sept"), so only the fixed part is asserted.
    True(NextSessionDisplay.Detail(next with { StartUtc = new DateTimeOffset(2026, 9, 12, 13, 30, 0, TimeSpan.Zero) }, now, zone).StartsWith("Practice 1 · Sat 12 "));
    Equal("in 1m", NextSessionDisplay.Detail(new(Championship.Dtm, "Sachsenring", null, now.AddSeconds(30)), now, zone));

    // The schedule keeps what is ahead, soonest first, whatever order the calendars answered in.
    var schedule = NextSessionDisplay.Schedule(sessions, now);
    Equal(2, schedule.Count);
    Equal(Championship.Formula1, schedule[0].Championship);
    Equal(Championship.Dtm, schedule[1].Championship);

    // A session that has reached its start stays listed while its timing has not reported, so a row never
    // disappears without saying what happened to it.
    var due = new UpcomingSession(Championship.Formula1, "Mexican Grand Prix", "Practice 1", now.AddMinutes(-4));
    Equal(1, NextSessionDisplay.Schedule([due], now).Count);
    Equal(Championship.Formula1, NextSessionDisplay.Next([due], now)!.Championship);
    Equal("Practice 1 · Waiting for data", NextSessionDisplay.Detail(due, now, zone));
    Equal("waiting for data", NextSessionDisplay.RowLead(due, now, zone));
    True(NextSessionDisplay.Tooltip(due, now, zone).StartsWith("Waiting for data: "));
    True(NextSessionDisplay.IsWaiting(due, now));
    True(NextSessionDisplay.IsScheduled(due, now));
    // The window ends, so a session that never reports drops off rather than sitting there for a day.
    var late = due with { StartUtc = now - NextSessionDisplay.WaitingWindow - TimeSpan.FromMinutes(1) };
    Equal(false, NextSessionDisplay.IsWaiting(late, now));
    Equal(0, NextSessionDisplay.Schedule([late], now).Count);
    // A date-only entry names a day, not a moment, so it is never waiting for a start that never existed.
    var dateOnly = new UpcomingSession(Championship.GtWorldChallengeEurope, "Misano", "Race", now.AddHours(-1))
    {
        StartDate = DateOnly.FromDateTime(now.UtcDateTime)
    };
    Equal(false, NextSessionDisplay.IsWaiting(dateOnly, now));

    var monza = next with { Venue = "Autodromo Nazionale Monza" };
    Equal("Practice 1 · Autodromo Nazionale Monza", NextSessionDisplay.RowSubtitle(monza));
    Equal("Practice 1", NextSessionDisplay.RowSubtitle(next));
    Equal("Autodromo Nazionale Monza", NextSessionDisplay.RowSubtitle(next with { SessionName = null, Venue = "Autodromo Nazionale Monza" }));
    Equal("Free Practice 1 · COTA", NextSessionDisplay.RowSubtitle(new(
        Championship.WorldEnduranceChampionship, "Lone Star Le Mans", "Free Practice 1", now, "Circuit of the Americas")));
    Equal("", NextSessionDisplay.RowSubtitle(next with { SessionName = null }));

    // A venue the event name already carries is not repeated.
    Equal("Free Practice", NextSessionDisplay.RowSubtitle(new(Championship.Dtm, "07 Sachsenring", "Free Practice", now, "Sachsenring")));

    // Near days are named rather than dated, because a date is harder to read against today.
    Equal("TODAY", NextSessionDisplay.GroupDay(next with { StartUtc = now.AddHours(6) }, now, zone));
    Equal("TOMORROW", NextSessionDisplay.GroupDay(next with { StartUtc = now.AddDays(1) }, now, zone));
    True(NextSessionDisplay.GroupDay(next with { StartUtc = new DateTimeOffset(2026, 9, 4, 13, 30, 0, TimeSpan.Zero) }, now, zone).StartsWith("FRIDAY 4 "));
    True(NextSessionDisplay.GroupDay(next with { StartUtc = new DateTimeOffset(2026, 9, 12, 13, 30, 0, TimeSpan.Zero) }, now, zone).StartsWith("SAT 12 "));
    Equal("13:30", NextSessionDisplay.RowTime(next with { StartUtc = new DateTimeOffset(2026, 9, 4, 13, 30, 0, TimeSpan.Zero) }, zone));

    // A meeting window publishes a date and no clock time: the row keeps that date in every timezone and says the
    // time is unknown instead of inventing midnight in the reader's zone.
    var losAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
    var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
    var misano = new UpcomingSession(Championship.GtWorldChallengeEurope, "Misano", null,
        new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero)) { StartDate = new DateOnly(2026, 5, 15) };
    Equal("", NextSessionDisplay.RowTime(misano, zone));
    Equal("", NextSessionDisplay.RowTime(misano, losAngeles));
    Equal("Fri 15 May", NextSessionDisplay.RowDate(misano, zone));
    Equal("Fri 15 May", NextSessionDisplay.RowDate(misano, losAngeles));
    False(NextSessionDisplay.Tooltip(misano, now, losAngeles).Contains("17:00"));
    True(NextSessionDisplay.Tooltip(misano, now, losAngeles).EndsWith("Fri 15 May, time TBA"));
    Equal("Fri 15 May", NextSessionDisplay.Detail(misano, now, tokyo));
    True(NextSessionDisplay.GroupDay(misano, now, losAngeles).StartsWith("FRIDAY 15 "));

    // The WRC fallback carries the rally's published date too, which the venue-local midnight conversion moves.
    var rally = ChampionshipCalendars.WorldRally("Rally Portugal", new DateTimeOffset(2026, 5, 13, 23, 0, 0, TimeSpan.Zero),
        [], now, "Matosinhos", new DateOnly(2026, 5, 14))[0];
    Equal(new DateOnly(2026, 5, 14), rally.StartDate);
    Equal("", NextSessionDisplay.RowTime(rally, zone));
    Equal("Thu 14 May", NextSessionDisplay.RowDate(rally, zone));
    Equal("Thu 14 May", NextSessionDisplay.RowDate(rally, losAngeles));

    // A date-only entry stays ahead through its published date in the reader's zone, not only until the UTC
    // midnight that orders it.
    var meetingDayInLosAngeles = new DateTimeOffset(2026, 5, 15, 1, 0, 0, TimeSpan.Zero); // 14 May 18:00 in LA
    True(misano.IsAhead(meetingDayInLosAngeles, losAngeles));
    Equal(Championship.GtWorldChallengeEurope, NextSessionDisplay.Next([misano], meetingDayInLosAngeles, losAngeles)?.Championship);
    Equal(1, NextSessionDisplay.Schedule([misano], meetingDayInLosAngeles, losAngeles).Count);
    Equal("TOMORROW", NextSessionDisplay.RowLead(misano, meetingDayInLosAngeles, losAngeles).ToUpperInvariant());
    Equal("TODAY", NextSessionDisplay.RowLead(misano, new DateTimeOffset(2026, 5, 15, 20, 0, 0, TimeSpan.Zero), losAngeles).ToUpperInvariant());
    Equal("IN 3 DAYS", NextSessionDisplay.RowLead(misano, new DateTimeOffset(2026, 5, 12, 20, 0, 0, TimeSpan.Zero), losAngeles).ToUpperInvariant());
    False(misano.IsAhead(new DateTimeOffset(2026, 5, 16, 20, 0, 0, TimeSpan.Zero), losAngeles)); // 16 May in LA
    // A published clock time still counts down to the instant.
    Equal("in 5 hours", NextSessionDisplay.RowLead(next with { StartUtc = now.AddHours(5) }, now, zone));

    var tooltip = NextSessionDisplay.Tooltip(next with { StartUtc = new DateTimeOffset(2026, 9, 4, 13, 30, 0, TimeSpan.Zero) }, now, zone);
    True(tooltip.StartsWith("Next session: Formula 1 · Italian Grand Prix — Practice 1, Fri 4 "));
    True(tooltip.EndsWith(", 13:30"));
}

static void UpcomingScheduleAggregation()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));
    var working = new StubCalendar([new(Championship.Dtm, "Sachsenring", "Race", clock.UtcNow.AddDays(4))]);
    var late = new StubCalendar([new(Championship.Formula1, "Monza", "Race", clock.UtcNow.AddDays(2))]);
    var broken = new StubCalendar(Championship.Imsa, new InvalidOperationException("provider offline"));
    var disabled = new StubCalendar([new(Championship.IndyCar, "Portland", "Race", clock.UtcNow.AddDays(3))]);
    var service = new UpcomingScheduleService([working, broken, late, disabled], clock, TimeSpan.FromHours(6), TimeSpan.FromSeconds(5));
    var enabled = new HashSet<Championship> { Championship.Dtm, Championship.Formula1, Championship.Imsa };

    var first = service.GetUpcomingAsync(enabled, CancellationToken.None).GetAwaiter().GetResult();
    // A broken provider drops out; the rest stay and are ordered by start time.
    Equal(2, first.Count);
    Equal(Championship.Formula1, first[0].Championship);
    Equal(Championship.Dtm, first[1].Championship);
    Equal(1, working.Calls);
    // A championship the user has not enabled is never contacted.
    Equal(0, disabled.Calls);

    service.GetUpcomingAsync(enabled, CancellationToken.None).GetAwaiter().GetResult();
    Equal(1, working.Calls);

    // A selected session starting invalidates the cache even before its normal six-hour lifetime expires.
    var soon = new StubCalendar([new(Championship.Formula1, "Baku", "Practice 3", clock.UtcNow.AddMinutes(5))]);
    var rollover = new UpcomingScheduleService([soon], clock, TimeSpan.FromHours(6), TimeSpan.FromSeconds(5));
    rollover.GetUpcomingAsync(enabled, CancellationToken.None).GetAwaiter().GetResult();
    clock.Advance(TimeSpan.FromMinutes(6));
    Equal(0, rollover.GetUpcomingAsync(enabled, CancellationToken.None).GetAwaiter().GetResult().Count);
    Equal(2, soon.Calls);

    // A date-only meeting stays in the cache through its published date even after its ordering instant passed.
    var datedClock = new FakeClock(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));
    var dated = new StubCalendar([new(Championship.GtWorldChallengeEurope, "Misano", null,
        new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero)) { StartDate = new DateOnly(2026, 8, 27) }]);
    var datedService = new UpcomingScheduleService([dated], datedClock, TimeSpan.FromHours(6), TimeSpan.FromSeconds(5));
    var gtOnly = new HashSet<Championship> { Championship.GtWorldChallengeEurope };
    Equal(1, datedService.GetUpcomingAsync(gtOnly, CancellationToken.None).GetAwaiter().GetResult().Count);
    datedClock.Advance(TimeSpan.FromDays(1));
    Equal(0, datedService.GetUpcomingAsync(gtOnly, CancellationToken.None).GetAwaiter().GetResult().Count);
    Equal(2, dated.Calls);

    // Enabling another championship invalidates the cache so the new source is queried.
    var widened = new HashSet<Championship>(enabled) { Championship.IndyCar };
    var second = service.GetUpcomingAsync(widened, CancellationToken.None).GetAwaiter().GetResult();
    Equal(3, second.Count);
    Equal(1, disabled.Calls);
    Equal(2, working.Calls);

    clock.Advance(TimeSpan.FromHours(7));
    service.GetUpcomingAsync(widened, CancellationToken.None).GetAwaiter().GetResult();
    Equal(3, working.Calls);

    var stalling = new StubCalendar(Championship.Dtm, TimeSpan.FromSeconds(30));
    var timed = new UpcomingScheduleService([stalling], clock, TimeSpan.FromHours(6), TimeSpan.FromMilliseconds(150));
    Equal(0, timed.GetUpcomingAsync(enabled, CancellationToken.None).GetAwaiter().GetResult().Count);
}

static void EventScheduleAggregation()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));
    ScheduledSession[] sessions =
    [
        new("Monza", "Practice 1", clock.UtcNow.AddHours(1), clock.UtcNow.AddHours(2)),
        new("Monza", "Qualifying", clock.UtcNow.AddHours(2), clock.UtcNow.AddHours(3)),
        new("Monza", "Race", clock.UtcNow.AddHours(3), clock.UtcNow.AddHours(4)),
        new("Singapore", "Race", clock.UtcNow.AddDays(7), clock.UtcNow.AddDays(7).AddHours(2))
    ];
    var weekend = ChampionshipCalendars.Formula1Weekend(sessions, clock.UtcNow);
    Equal(3, weekend.Count);
    True(weekend.All(session => session.EventName == "Monza"));
    var calendar = new EventScheduleCalendar(ChampionshipCalendars.NextFormula1(sessions, clock.UtcNow)!, weekend);
    var fallback = new StubCalendar([new(Championship.WorldEnduranceChampionship, "COTA", "Race", clock.UtcNow.AddHours(4))]);
    var broken = new StubCalendar(Championship.Imsa, new InvalidOperationException("provider offline"));
    var disabled = new StubCalendar([new(Championship.Dtm, "Sachsenring", "Race", clock.UtcNow.AddHours(5))]);
    using var service = new UpcomingScheduleService([calendar, fallback, broken, disabled], clock,
        TimeSpan.FromHours(6), TimeSpan.FromSeconds(5));
    var enabled = new HashSet<Championship> { Championship.Formula1, Championship.WorldEnduranceChampionship, Championship.Imsa };
    Equal(2, service.GetUpcomingAsync(enabled, CancellationToken.None).GetAwaiter().GetResult().Count);
    var events = service.GetEventSchedulesAsync(enabled, CancellationToken.None).GetAwaiter().GetResult();
    Equal(4, events.Count);
    Equal("Practice 1", events[0].SessionName);
    Equal("Race", events[2].SessionName);
    Equal(Championship.WorldEnduranceChampionship, events[3].Championship);
    Equal(0, disabled.Calls);

    // Reading the complete schedule must not replace the desktop's next-session cache.
    Equal(2, service.GetUpcomingAsync(enabled, CancellationToken.None).GetAwaiter().GetResult().Count);
    Equal(4, service.GetEventSchedulesAsync(enabled, CancellationToken.None).GetAwaiter().GetResult().Count);
    Equal(1, calendar.UpcomingCalls);
    Equal(1, calendar.EventCalls);
    clock.Advance(TimeSpan.FromMinutes(75));
    Equal(1, service.GetUpcomingAsync(enabled, CancellationToken.None).GetAwaiter().GetResult().Count);
    Equal(3, service.GetEventSchedulesAsync(enabled, CancellationToken.None).GetAwaiter().GetResult().Count);
    Equal(2, calendar.UpcomingCalls);
    Equal(2, calendar.EventCalls);
}

static void RunValueExecutablePaths()
{
    Equal(@"C:\Apps\RaceDock\RaceDock.exe", LaunchArguments.ExecutablePathFromRunValue(@"""C:\Apps\RaceDock\RaceDock.exe"" --startup"));
    Equal(@"C:\Apps\RaceDock\RaceDock.exe", LaunchArguments.ExecutablePathFromRunValue(@"""C:\Apps\RaceDock\RaceDock.exe"""));
    Equal(@"C:\Apps\RaceDock.exe", LaunchArguments.ExecutablePathFromRunValue(@"C:\Apps\RaceDock.exe --startup"));
    Equal(@"C:\Apps\RaceDock.exe", LaunchArguments.ExecutablePathFromRunValue(@"C:\Apps\RaceDock.exe"));
    Equal(null, LaunchArguments.ExecutablePathFromRunValue("   "));
    Equal(null, LaunchArguments.ExecutablePathFromRunValue(null));
    Equal(null, LaunchArguments.ExecutablePathFromRunValue("\"\""));
}

static void MotoGpEmptyResponse()
{
    foreach (var body in new[] { "", " \r\n" })
    {
        var clock = new FakeClock();
        var delay = new BlockingDelay();
        using var http = new HttpClient(new StaticJsonHandler(body));
        var source = new MotoGpLiveTimingSource(http, clock, delay, new AlertArbiter(clock), TimeSpan.FromSeconds(5));
        List<TimingSnapshot> snapshots = [];
        List<ConnectionState> connections = [];
        List<FeedFailure> failures = [];
        source.SnapshotReceived += snapshots.Add;
        source.ConnectionChanged += connections.Add;
        source.Failed += failures.Add;
        try
        {
            source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
            Equal(ConnectionState.Connected, connections[^1]);
            Equal(SessionLifecycle.OffSession, snapshots.Single().Lifecycle);
            Equal(TimeSpan.FromSeconds(5), delay.Duration);
            Equal(0, failures.Count);
        }
        finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
}

static void TaskbarSessionProgress()
{
    var now = DateTimeOffset.UtcNow;
    (Championship Championship, int? Total, string Race)[] races =
    [
        (Championship.Formula1, 57, "RAC 12/57"),
        (Championship.Formula2, 30, "RAC 12/30"),
        (Championship.Formula3, 25, "RAC 12/25"),
        (Championship.MotoGp, 27, "RAC 12/27"),
        (Championship.Moto2, 20, "RAC 12/20"),
        (Championship.Moto3, 18, "RAC 12/18"),
        (Championship.FormulaE, 32, "RAC 12/32"),
        (Championship.IndyCar, 200, "RAC 12/200"),
        (Championship.WorldEnduranceChampionship, null, "RAC 1:23:45"),
        (Championship.EuropeanLeMansSeries, null, "RAC 1:23:45"),
        (Championship.MichelinLeMansCup, null, "RAC 1:23:45"),
        (Championship.Imsa, 100, "RAC 1:23:45"),
        (Championship.GtWorldChallengeEurope, null, "RAC 1:23:45"),
        (Championship.Dtm, null, "RAC 1:23:45")
    ];
    foreach (var (championship, total, race) in races)
    {
        var snapshot = new TimingSnapshot("Event", "Race", "Track", 12, total,
            TrackCondition.AllClear, [], now, SessionLifecycle.Live,
            TimeRemaining: "01:23:45", Championship: championship);
        Equal(race, TaskbarSessionDisplay.Progress(snapshot));
        Equal("FP1 21:52", TaskbarSessionDisplay.Progress(snapshot with
            { Session = "Free Practice 1", TimeRemaining = "00:21:52" }));
        Equal("Q 08:30", TaskbarSessionDisplay.Progress(snapshot with
            { Session = "Qualifying", TimeRemaining = "00:08:30" }));
        Equal("Q —", TaskbarSessionDisplay.Progress(snapshot with
            { Session = "Qualifying", TimeRemaining = null }));
        var noClock = race.Contains('/') ? race : "RAC —";
        Equal(noClock, TaskbarSessionDisplay.Progress(snapshot with { TimeRemaining = null }));
    }

    var f1 = new TimingSnapshot("Event", "Sprint Race", "Track", 5, 19,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live,
        TimeRemaining: "00:30:00", Championship: Championship.Formula1);
    Equal("SPR 5/19", TaskbarSessionDisplay.Progress(f1));
    Equal("SPR LAP 5", TaskbarSessionDisplay.Progress(f1 with { TotalLaps = null }));
    Equal("SPR 0/19", TaskbarSessionDisplay.Progress(f1 with { CurrentLap = 0 }));
    Equal("RAC 30:00", TaskbarSessionDisplay.Progress(f1 with
        { Championship = Championship.FormulaE, Session = "Race", TotalLaps = null }));
    Equal("SS4  Harju 1", TaskbarSessionDisplay.Progress(f1 with
        { Championship = Championship.WorldRallyChampionship, Session = "SS4  Harju 1" }));
}

static void TaskbarSessionLabels()
{
    var snapshot = new TimingSnapshot("Event", "Race", "Track", 12, 57,
        TrackCondition.AllClear, [], DateTimeOffset.UtcNow, SessionLifecycle.Live,
        TimeRemaining: "00:21:52", Championship: Championship.Formula1);
    (string Session, string Text)[] names =
    [
        ("Practice", "FP 21:52"),
        ("Free Practice", "FP 21:52"),
        ("Practice 1 - All Cars", "FP1 21:52"),
        ("  FREE   PRACTICE 2  ", "FP2 21:52"),
        ("Free Practice Nr. 3", "FP3 21:52"),
        ("Qualifying", "Q 21:52"),
        ("Qualifying 1", "Q1 21:52"),
        ("Qualifying Nr. 2", "Q2 21:52"),
        ("Qualifying 3", "Q3 21:52"),
        ("Sprint Qualifying", "SQ 21:52"),
        ("Sprint Qualifying 1", "SQ1 21:52"),
        ("Sprint Qualifying 2", "SQ2 21:52"),
        ("Sprint Shootout 3", "SQ3 21:52"),
        ("Qualifying 1 Group A", "Q1A 21:52"),
        ("Qualifying 2 Group B", "Q2B 21:52"),
        ("Qualifying Group A", "QGA 21:52"),
        ("Qualifying - LMGT3", "Q 21:52"),
        ("Free Practice - HYPERCAR", "FP 21:52"),
        ("Combined Qualifying", "CQ 21:52"),
        ("Combined Qualifying 2", "CQ2 21:52"),
        ("Qualifying Duel Quarter-Final 4", "QF4 21:52"),
        ("Qualifying Duel Semi-Final 2", "SF2 21:52"),
        ("Qualifying Duel Final", "FIN 21:52"),
        ("Hyperpole", "HP 21:52"),
        ("Warmup", "WUP 21:52"),
        ("Race", "RAC 12/57"),
        ("Race 1", "R1 12/57"),
        ("Race 2", "R2 12/57"),
        ("Sprint", "SPR 12/57"),
        ("Sprint Race", "SPR 12/57"),
        ("Feature Race", "FEA 12/57"),
        ("Opening Race", "RAC 12/57"),
        ("Reverse Grid Race", "RAC 12/57"),
        ("Unknown provider session name", "LIVE 12/57")
    ];
    foreach (var (session, text) in names)
        Equal(text, TaskbarSessionDisplay.Progress(snapshot with { Session = session }));
    // F1 Academy calls its third race a feature race; its other two already read RAC, so it does as well.
    Equal("RAC 12/57", TaskbarSessionDisplay.Progress(snapshot with { Championship = Championship.F1Academy, Session = "Feature Race" }));
    // A Formula 2 or 3 feature race keeps the code that tells it apart from the sprint.
    Equal("FEA 12/57", TaskbarSessionDisplay.Progress(snapshot with { Championship = Championship.Formula2, Session = "Feature Race" }));

    foreach (var championship in new[] { Championship.MotoGp, Championship.Moto2, Championship.Moto3 })
    {
        var moto = snapshot with { Championship = championship, Session = "MotoGP Practice", SessionShortName = "PR" };
        Equal("FP 21:52", TaskbarSessionDisplay.Progress(moto));
        Equal("FP1 21:52", TaskbarSessionDisplay.Progress(moto with { SessionShortName = "FP1" }));
        Equal("Q2 21:52", TaskbarSessionDisplay.Progress(moto with { SessionShortName = "Q2" }));
        Equal("RAC 12/57", TaskbarSessionDisplay.Progress(moto with { SessionShortName = "RAC" }));
        Equal("SPR 12/57", TaskbarSessionDisplay.Progress(moto with { SessionShortName = "SPR" }));
        Equal("FP2 21:52", TaskbarSessionDisplay.Progress(moto with
            { Session = "Moto3 Free Practice Nr. 2", SessionShortName = null }));
    }
}

static void TaskbarSessionClocks()
{
    var snapshot = new TimingSnapshot("Event", "Qualifying", "Track", 12, null,
        TrackCondition.AllClear, [], DateTimeOffset.UtcNow, SessionLifecycle.Live,
        Championship: Championship.Formula1);
    (string? Value, string Text)[] clocks =
    [
        ("1:27", "01:27"),
        ("21:52", "21:52"),
        ("00:21:52", "21:52"),
        ("01:02:03", "1:02:03"),
        ("24:00:00", "24:00:00"),
        ("90:00", "1:30:00"),
        ("00:00:00", "00:00"),
        ("00:00", "00:00"),
        (null, "—"),
        (" ", "—"),
        ("-00:01", "—"),
        ("00:60:00", "—"),
        ("00:00:60", "—"),
        ("not a clock", "—")
    ];
    foreach (var (value, text) in clocks)
        Equal($"Q {text}", TaskbarSessionDisplay.Progress(snapshot with { TimeRemaining = value }));
}

static void GtwcMeetingNames()
{
    var now = new DateTimeOffset(2026, 10, 16, 5, 0, 0, TimeSpan.Zero);
    var season = JsonNode.Parse("""
        {"PresentationMeetingId":"p","Meetings":{"p":{
          "Id":"p","Name":"GT WorldChEU pwrd by AWS Endurance Cup Round 10",
          "TrackName":"Portimao","Location":"Autodromo Internacional do Algarve Portimao",
          "CountryCode":"POR","BeginDate":"16.10.2026","EndDate":"18.10.2026","State":1}}}
        """)!.AsObject();
    var node = season["Meetings"]!["p"]!.AsObject();
    (string? Track, string? Location, string Expected)[] names =
    [
        ("Portimao", "Autodromo Internacional do Algarve Portimao", "Portimao"),
        (null, "Autodromo Internacional do Algarve Portimao", "Autodromo Internacional do Algarve Portimao"),
        (null, null, "GT WorldChEU pwrd by AWS Endurance Cup Round 10")
    ];
    foreach (var (track, location, expected) in names)
    {
        node["TrackName"] = track;
        node["Location"] = location;
        var live = GtwcTiming.PresentationMeeting(season)!;
        var meeting = ChampionshipCalendars.NextGtWorldChallengeMeeting(season, now)!;
        Equal(expected, live.Name);
        Equal(live.Name, meeting.Name);
        // A timetable not yet published must retain the same event identity.
        Equal(live.Name, ChampionshipCalendars.NextGtWorldChallenge(meeting, null, now).EventName);
        True(ChampionshipCalendars.GtWorldChallengeMeeting(meeting, null, now)
            .All(session => session.EventName == live.Name));
    }
}

static void GtwcDiscoveryFiltering()
{
    var season = JsonNode.Parse("""
        {"PresentationMeetingId":"meeting-1","Meetings":{"meeting-1":{"TrackName":"Magny-Cours","State":1}}}
        """)!.AsObject();
    var meeting = GtwcTiming.PresentationMeeting(season);
    Equal("Magny-Cours", meeting?.Name);
    True(GtwcTiming.IsGtwcEuropeName("GTWorldChEU pwrd by AWS Sprint Cup"));
    True(GtwcTiming.IsGtwcEuropeName("GT World Challenge Europe Endurance Cup"));
    False(GtwcTiming.IsGtwcEuropeName("GT4 European Series"));

    var schedule = JsonNode.Parse("""
        {"PresentationRoundId":"support","Series":{
           "gt":{"Name":"GTWorldChEu pwrd by AWS Sprint Cup"},"f4":{"Name":"FFSA F4"}},
         "Competitions":{"gtc":{"SerieId":"gt","Name":"GTWorldChEu pwrd by AWS Sprint Cup"},
           "f4c":{"SerieId":"f4","Name":"FFSA F4"}},
         "Units":{"gtwc":{"Id":"gtwc","Name":"Qualifying 1 Group B","Code":"Q1B","CompetitionId":"gtc","State":1},
           "test":{"Id":"test","Name":"Bronze Test and Paid Test GT2","CompetitionId":"gtc","State":1},
           "support":{"Id":"support","Name":"F4 Race","CompetitionId":"f4c","State":1}}}
        """)!.AsObject();
    Equal<GtwcUnit?>(null, GtwcTiming.PresentedUnit(schedule));
    schedule["PresentationRoundId"] = "test";
    Equal<GtwcUnit?>(null, GtwcTiming.PresentedUnit(schedule));
    schedule["PresentationRoundId"] = "gtwc";
    var unit = GtwcTiming.PresentedUnit(schedule);
    Equal("gtwc", unit?.Id);
    Equal("Qualifying 1 Group B", unit?.Name);
}

static void GtwcStandings()
{
    var meeting = new GtwcMeeting("meeting-1", "Magny-Cours", "Magny-Cours", 1);
    var unit = new GtwcUnit("unit-1", "Qualifying 1 Group B", "gtc", 1);
    var timing = JsonNode.Parse("""
        {"UnitId":"unit-1","UntInfo":{"RemainingTime":"1:27","TrackFlag":1,"SectorFlags":[1,1,1]},"Results":{
          "car-1":{"CompetitorId":"car-1","ListIndex":0,"MainResult":{"Rank":1,"BestTime":{"Time":"1:36.500"},"TotalLapCount":3,"Status":2}},
          "car-2":{"CompetitorId":"car-2","ListIndex":1,"MainResult":{"Rank":2,"BestTime":{"Time":"1:36.500"},"Behind":"0.200","Diff":"0.200","TotalLapCount":4,"Status":2}},
          "car-3":{"CompetitorId":"car-3","ListIndex":2,"MainResult":{"Rank":3,"BestTime":{"Time":"1:37.100"},"Behind":"0.600","Diff":"0.400","TotalLapCount":2,"Status":4}}}}
        """)!.AsObject();
    var detail = JsonNode.Parse("""
        {"UnitId":"unit-1","Classes":{"pro":{"ShortName":"Pro"},"silver":{"ShortName":"Silver"}},"Competitors":{
          "car-1":{"Bib":"3","ClassId":"pro","TeamName":"Winward Racing","ManufacturerName":"Mercedes-AMG","CurrentDriverId":"driver-1","InPitLane":false,
            "Drivers":{"driver-1":{"ListIndex":1,"FirstName":"Jules","LastName":"Gounon","ShortName":"GOU"}}},
          "car-2":{"Bib":"46","ClassId":"silver","TeamName":"Team WRT","ManufacturerName":"BMW M","InPitLane":true,
            "Drivers":{"later":{"ListIndex":2,"FirstName":"Second","LastName":"Driver","ShortName":"SEC"},"first":{"ListIndex":1,"FirstName":"Max","LastName":"Hesse","ShortName":"HES"}}},
          "car-3":{"Bib":"7","ClassId":"pro","TeamName":"Comtoyou Racing","ManufacturerName":"Aston Martin","InPitLane":false,
            "Drivers":{"driver-3":{"ListIndex":1,"FirstName":"Kobe","LastName":"Pauwels","ShortName":"PAU"}}}}}
        """)!.AsObject();
    var snapshot = GtwcTiming.BuildSnapshot(meeting, unit, timing, detail,
        new DateTimeOffset(2026, 8, 1, 13, 18, 0, TimeSpan.Zero), ConnectionState.Connected);
    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal(Championship.GtWorldChallengeEurope, snapshot.Championship);
    Equal("1:27", snapshot.TimeRemaining);
    Equal("Q1B 01:27", TaskbarSessionDisplay.Progress(snapshot));
    Equal(4, snapshot.CurrentLap);
    Equal("GOU", snapshot.Competitors[0].Code);
    Equal("Winward Racing", snapshot.Competitors[0].Team);
    Equal("Mercedes-AMG", snapshot.Competitors[0].Manufacturer);
    Equal("Pro", snapshot.Competitors[0].Category);
    Equal("1:36.500", snapshot.Competitors[0].ResultTime);
    Equal("+0.200", snapshot.Competitors[1].GapToLeader);
    Equal("HES", snapshot.Competitors[1].Code);
    True(snapshot.Competitors[1].InPit);
    True(snapshot.Competitors[2].Retired);
    Equal(1, snapshot.Competitors.Count(standing => standing.IsOverallFastest));
    True(snapshot.Competitors[0].IsOverallFastest);
    timing["UntInfo"]!["ChequeredFlag"] = true;
    Equal(SessionLifecycle.Ended, GtwcTiming.BuildSnapshot(meeting, unit, timing, detail,
        new DateTimeOffset(2026, 8, 1, 13, 20, 0, TimeSpan.Zero), ConnectionState.Connected).Lifecycle);
}

static void GtwcSectorTiming()
{
    var fixture = SectorFixture("gtwc-portimao-fp1.json");
    var timing = fixture["timing"]!.AsObject();
    var detail = fixture["detail"]!.AsObject();
    var unit = new GtwcUnit(JsonSupport.String(timing["UnitId"])!, "Free Practice 1", "gtwc", 1);
    TimingSnapshot Read() => GtwcTiming.BuildSnapshot(new("meeting", "Portimao", "Portimao", 1), unit,
        timing, detail, DateTimeOffset.Parse(JsonSupport.String(fixture["timestamp"])!), ConnectionState.Connected);
    var snapshot = Read();
    var leader = snapshot.Competitors.Single(value => value.Code == "MAG");
    Equal("10", leader.CarNumber);
    Equal("#10  Gilles Magnus", ChampionshipDisplay.FlyoutDriverName(snapshot.Championship, leader));
    Equal("33.402", leader.CurrentSectors?.Sector1?.Value);
    Equal(SectorTimingState.PersonalBest, leader.CurrentSectors?.Sector1?.State);
    Equal("38.050", leader.CurrentSectors?.Sector3?.Value);
    Equal(SectorTimingState.OverallBest, leader.CurrentSectors?.Sector3?.State);
    Equal(leader.CurrentSectors, leader.BestLapSectors);
    var following = snapshot.Competitors.Single(value => value.Code == "LIN");
    Equal("32.324", following.CurrentSectors?.Sector1?.Value);
    Equal<SectorTiming?>(null, following.CurrentSectors?.Sector2);
    Equal<SectorTiming?>(null, following.CurrentSectors?.Sector3);
    Equal("33.499", following.BestLapSectors?.Sector1?.Value);
    Equal("38.052", following.BestLapSectors?.Sector3?.Value);
    True(snapshot.Competitors.All(value => value.CurrentMiniSectors is null));

    var main = timing["Results"]![following.DriverId]!["MainResult"]!.AsObject();
    main["LastLap"]!["Intermediates"] = JsonNode.Parse("""
        [{"Time":"40.000","TimeState":0},{"Time":"bad","TimeState":2},{"Time":"0.000","TimeState":1}]
        """);
    following = Read().Competitors.Single(value => value.Code == "LIN");
    Equal("40.000", following.CurrentSectors?.Sector1?.Value);
    Equal(SectorTimingState.Slower, following.CurrentSectors?.Sector1?.State);
    Equal<SectorTiming?>(null, following.CurrentSectors?.Sector2);
    Equal<SectorTiming?>(null, following.CurrentSectors?.Sector3);
    Equal("33.499", following.BestLapSectors?.Sector1?.Value);
    main["LastLap"]!["Intermediates"] = JsonNode.Parse("""[{"Time":"12.000","TimeState":99}]""");
    Equal(SectorTimingState.None, Read().Competitors.Single(value => value.Code == "LIN").CurrentSectors?.Sector1?.State);
    main["LastLap"] = new JsonObject();
    Equal<SectorTimingSet?>(null, Read().Competitors.Single(value => value.Code == "LIN").CurrentSectors);
}

static void GtwcFlagsAndStatuses()
{
    Equal("FIN", GtwcTiming.StatusLabel(1));
    Equal("RUN", GtwcTiming.StatusLabel(2));
    Equal("RET", GtwcTiming.StatusLabel(4));
    Equal("RET", GtwcTiming.StatusLabel(16));
    Equal("N/S", GtwcTiming.StatusLabel(32));
    Equal("+0.257", GtwcTiming.FormatGap("0.257"));
    Equal("1 LAP", GtwcTiming.FormatGap("1 LAP"));
    Equal(TrackCondition.AllClear, GtwcTiming.TrackState(JsonNode.Parse("{\"TrackFlag\":1}")!.AsObject()).Condition);
    Equal(TrackCondition.Yellow, GtwcTiming.TrackState(JsonNode.Parse("{\"TrackFlag\":1,\"SectorFlags\":[1,4,1]}")!.AsObject()).Condition);
    var fcy = GtwcTiming.TrackState(JsonNode.Parse("{\"TrackFlag\":4,\"SectorFlags\":[1,1,1]}")!.AsObject());
    Equal(TrackCondition.FullCourseYellow, fcy.Condition);
    Equal("FULL COURSE YELLOW", fcy.Detail);
    var code60 = GtwcTiming.TrackState(JsonNode.Parse("{\"TrackFlag\":64}")!.AsObject());
    Equal(TrackCondition.VirtualSafetyCar, code60.Condition);
    Equal("CODE 60", code60.Detail);
    Equal(8, (int)TrackCondition.FullCourseYellow);
    Equal(7, (int)TrackCondition.RedFlag);

    var clock = new FakeClock();
    var alerts = new AlertArbiter(clock);
    alerts.ApplyTrackCondition(fcy.Condition, fcy.Detail, 12, clock.UtcNow);
    Equal(AlertKind.FullCourseYellow, alerts.Current?.Kind);
    Equal("FULL COURSE YELLOW", alerts.Current?.Title);
    alerts.ApplyTrackCondition(TrackCondition.AllClear, null, 12, clock.UtcNow);
    Equal<RaceAlert?>(null, alerts.Current);
    Equal(TrackCondition.RedFlag, GtwcTiming.TrackState(JsonNode.Parse("{\"TrackFlag\":1,\"SectorFlags\":[4,8,32]}")!.AsObject()).Condition);
}

static void GtwcClientPaths()
{
    using var handler = new GtwcHttpHandler([]);
    using var http = new HttpClient(handler);
    using var client = new GtwcApiClient(http);
    client.GetScheduleAsync("2026", "meeting-ab", CancellationToken.None).GetAwaiter().GetResult();
    client.GetScheduleAsync("2026", "meeting-ab", CancellationToken.None).GetAwaiter().GetResult();
    Equal(2, handler.Paths.Count);
    True(handler.Paths.All(path => path.Contains("SRO_2026_SCHEDULE_MEETING-AB_JSON.json", StringComparison.Ordinal)));
    False(handler.Paths[0] == handler.Paths[1]);
}

static void GtwcScheduleRefreshAfterUnitFailure()
{
    static JsonObject Schedule(string unitId, string name, string code) => new()
    {
        ["MeetingId"] = "meeting-1",
        ["PresentationRoundId"] = unitId,
        ["Series"] = new JsonObject { ["gt"] = new JsonObject { ["Name"] = "GTWorldChEU pwrd by AWS Sprint Cup" } },
        ["Competitions"] = new JsonObject { ["gtc"] = new JsonObject { ["SerieId"] = "gt" } },
        ["Units"] = new JsonObject
        {
            [unitId] = new JsonObject { ["Name"] = name, ["Code"] = code, ["CompetitionId"] = "gtc", ["State"] = 1 }
        }
    };
    static JsonObject Timing(string unitId) => new()
    {
        ["UnitId"] = unitId,
        ["UntInfo"] = new JsonObject { ["RemainingTime"] = "8:15", ["TrackFlag"] = 1, ["SectorFlags"] = new JsonArray(1, 1, 1) },
        ["Results"] = new JsonObject
        {
            ["car-1"] = new JsonObject
            {
                ["CompetitorId"] = "car-1",
                ["ListIndex"] = 0,
                ["MainResult"] = new JsonObject
                {
                    ["Rank"] = 1, ["BestTime"] = new JsonObject { ["Time"] = "1:36.500" },
                    ["TotalLapCount"] = 2, ["Status"] = 2
                }
            }
        }
    };
    static JsonObject Detail(string unitId) => new()
    {
        ["UnitId"] = unitId,
        ["Classes"] = new JsonObject { ["pro"] = new JsonObject { ["ShortName"] = "Pro" } },
        ["Competitors"] = new JsonObject
        {
            ["car-1"] = new JsonObject
            {
                ["Bib"] = "3", ["ClassId"] = "pro", ["TeamName"] = "Winward Racing",
                ["ManufacturerName"] = "Mercedes-AMG", ["CurrentDriverId"] = "driver-1",
                ["Drivers"] = new JsonObject
                {
                    ["driver-1"] = new JsonObject
                    {
                        ["ListIndex"] = 1, ["FirstName"] = "Jules", ["LastName"] = "Gounon", ["ShortName"] = "GOU"
                    }
                }
            }
        }
    };
    var routes = new Dictionary<string, string[]>
    {
        ["SRO_SEASONS_JSON.json"] = [GtwcEnvelope("""{"CurrentSeason":"2026"}""")],
        ["SRO_2026_SEASON_JSON.json"] =
            [GtwcEnvelope("""{"PresentationMeetingId":"meeting-1","Meetings":{"meeting-1":{"TrackName":"Magny-Cours","State":1}}}""")],
        ["SRO_2026_SCHEDULE_MEETING-1_JSON.json"] =
        [
            GtwcEnvelope(Schedule("unit-1", "Qualifying 1 Group B", "Q1B").ToJsonString()),
            GtwcEnvelope(Schedule("unit-2", "Race", "R").ToJsonString())
        ],
        ["SRO_2026_TIMING_UNIT-1_JSON.json"] = [GtwcEnvelope(Timing("unit-1").ToJsonString()), "__404__"],
        ["SRO_2026_COMP_DETAIL_UNIT-1_JSON.json"] = [GtwcEnvelope(Detail("unit-1").ToJsonString()), "__404__"],
        ["SRO_2026_TIMING_UNIT-2_JSON.json"] = [GtwcEnvelope(Timing("unit-2").ToJsonString())],
        ["SRO_2026_COMP_DETAIL_UNIT-2_JSON.json"] = [GtwcEnvelope(Detail("unit-2").ToJsonString())]
    };
    using var handler = new GtwcSequenceHandler(routes);
    using var http = new HttpClient(handler);
    using var api = new GtwcApiClient(http);
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 1, 13, 18, 0, TimeSpan.Zero));
    // Every wait advances the fake clock, so the schedule refresh cadence runs without real waiting.
    var delay = new AdvancingDelay(clock, TimeSpan.FromMilliseconds(400), 40);
    var options = new GtwcPollingOptions(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500), TimeSpan.FromHours(1),
        [TimeSpan.FromHours(1)], [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)]);
    var source = new GtwcLiveTimingSource(api, clock, delay, new AlertArbiter(clock), options);
    var sessions = new List<string>();
    source.SnapshotReceived += snapshot =>
    {
        if (snapshot.Lifecycle != SessionLifecycle.Live) return;
        lock (sessions) sessions.Add(snapshot.Session);
    };
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
    while (DateTime.UtcNow < deadline)
    {
        lock (sessions) if (sessions.Contains("Race")) break;
        Thread.Sleep(10);
    }
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();

    lock (sessions)
    {
        Equal("Qualifying 1 Group B", sessions.FirstOrDefault());
        Equal("Race", sessions.LastOrDefault());
    }
    // The session that replaced the failing unit is polled even though the old unit never answers again.
    True(handler.Paths.Contains("SRO_2026_TIMING_UNIT-2_JSON.json"));
    True(handler.Paths.Count(path => path == "SRO_2026_SCHEDULE_MEETING-1_JSON.json") >= 2);
    True(handler.Paths.Count(path => path == "SRO_2026_TIMING_UNIT-1_JSON.json") <= 4);
}

static void GtwcSourceLifecycle()
{
    var routes = new Dictionary<string, string>
    {
        ["SRO_SEASONS_JSON.json"] = GtwcEnvelope("{\"CurrentSeason\":\"2026\"}"),
        ["SRO_2026_SEASON_JSON.json"] = GtwcEnvelope("{\"PresentationMeetingId\":\"meeting-1\",\"Meetings\":{\"meeting-1\":{\"TrackName\":\"Magny-Cours\",\"State\":1}}}"),
        ["SRO_2026_SCHEDULE_MEETING-1_JSON.json"] = GtwcEnvelope("{\"MeetingId\":\"meeting-1\",\"PresentationRoundId\":\"unit-1\",\"Series\":{\"gt\":{\"Name\":\"GTWorldChEU pwrd by AWS Sprint Cup\"}},\"Competitions\":{\"gtc\":{\"SerieId\":\"gt\"}},\"Units\":{\"unit-1\":{\"Name\":\"Qualifying 1 Group B\",\"Code\":\"Q1B\",\"CompetitionId\":\"gtc\",\"State\":1}}}"),
        ["SRO_2026_TIMING_UNIT-1_JSON.json"] = GtwcEnvelope("{\"UnitId\":\"unit-1\",\"UntInfo\":{\"RemainingTime\":\"8:15\",\"TrackFlag\":1,\"SectorFlags\":[1,1,1]},\"Results\":{\"car-1\":{\"CompetitorId\":\"car-1\",\"ListIndex\":0,\"MainResult\":{\"Rank\":1,\"BestTime\":{\"Time\":\"1:36.500\"},\"TotalLapCount\":2,\"Status\":2}}}}"),
        ["SRO_2026_COMP_DETAIL_UNIT-1_JSON.json"] = GtwcEnvelope("{\"UnitId\":\"unit-1\",\"Classes\":{\"pro\":{\"ShortName\":\"Pro\"}},\"Competitors\":{\"car-1\":{\"Bib\":\"3\",\"ClassId\":\"pro\",\"TeamName\":\"Winward Racing\",\"ManufacturerName\":\"Mercedes-AMG\",\"CurrentDriverId\":\"driver-1\",\"Drivers\":{\"driver-1\":{\"ListIndex\":1,\"FirstName\":\"Jules\",\"LastName\":\"Gounon\",\"ShortName\":\"GOU\"}}}}}"),
    };
    using var handler = new GtwcHttpHandler(routes);
    using var http = new HttpClient(handler);
    using var api = new GtwcApiClient(http);
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 1, 13, 18, 0, TimeSpan.Zero));
    var delay = new BlockingDelay();
    var options = new GtwcPollingOptions(TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.FromHours(1),
        [TimeSpan.FromHours(1)], [TimeSpan.FromHours(1)]);
    var source = new GtwcLiveTimingSource(api, clock, delay, new AlertArbiter(clock), options);
    List<TimingSnapshot> snapshots = [];
    source.SnapshotReceived += snapshots.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    var live = snapshots.Single(snapshot => snapshot.Lifecycle == SessionLifecycle.Live);
    Equal("Magny-Cours", live.Meeting);
    Equal("Qualifying 1 Group B", live.Session);
    Equal("GOU", live.Competitors.Single().Code);
}

static void GtwcHandlerBounds()
{
    using var handler = GtwcApiClient.CreateDefaultHandler();
    Equal(TimeSpan.FromMinutes(2), handler.PooledConnectionLifetime);
    Equal(TimeSpan.FromMinutes(1), handler.PooledConnectionIdleTimeout);
    Equal(TimeSpan.FromSeconds(10), handler.ConnectTimeout);
}

static void ImsaSockJsProtocol()
{
    var connect = new JsonObject
    {
        ["msg"] = "connect",
        ["version"] = "1",
        ["support"] = new JsonArray("1")
    };
    var encoded = AlkamelDdpClient.EncodeMessage(connect);
    var outer = JsonNode.Parse(encoded)!.AsArray();
    Equal(1, outer.Count);
    Equal("connect", JsonSupport.String(JsonNode.Parse(outer[0]!.GetValue<string>())!["msg"]));

    var frame = "a" + new JsonArray(connect.ToJsonString()).ToJsonString();
    var decoded = AlkamelDdpClient.DecodeFrame(frame, out var opened, out var heartbeat);
    False(opened);
    False(heartbeat);
    Equal("connect", JsonSupport.String(decoded.Single()["msg"]));
    Equal(0, AlkamelDdpClient.DecodeFrame("h", out opened, out heartbeat).Count);
    True(heartbeat);
    Equal(0, AlkamelDdpClient.DecodeFrame("o", out opened, out heartbeat).Count);
    True(opened);

    var endpoint = AlkamelDdpClient.CreateEndpoint(AlkamelFeedDefinition.Imsa.SocketBase);
    Equal("livetiming.alkamelsystems.com", endpoint.Host);
    True(endpoint.AbsolutePath.StartsWith("/sockjs/", StringComparison.Ordinal));
    True(endpoint.AbsolutePath.EndsWith("/websocket", StringComparison.Ordinal));
    var formulaEEndpoint = AlkamelDdpClient.CreateEndpoint(AlkamelFeedDefinition.FormulaE.SocketBase);
    Equal("livetiming-formula-e.alkamelsystems.com", formulaEEndpoint.Host);
    True(formulaEEndpoint.AbsolutePath.EndsWith("/websocket", StringComparison.Ordinal));
}

static void ImsaStandings()
{
    var session = JsonNode.Parse("""{"name":"Race"}""")!.AsObject();
    var info = JsonNode.Parse("""{"eventName":"Motul SportsCar Endurance Grand Prix","champName":"IMSA WeatherTech SportsCar Championship","name":"RACE","type":"RACE","closed":false}""")!.AsObject();
    True(ImsaTiming.IsWeatherTechSession(info));
    False(ImsaTiming.IsWeatherTechSession(JsonNode.Parse("""{"champName":"IMSA AirBNB Endurance Challenge"}""")!.AsObject()));
    var entries = JsonNode.Parse("""
        {"01":{"driver":"Wit","name":"M. WITTMANN","team":"BMW M Team WRT","vehicle":"BMW M HYBRID V8","class":"GTP"},
         "24":{"driver":"Van","name":"S. VAN DER LINDE","team":"BMW M Team WRT","vehicle":"BMW M HYBRID V8","class":"GTP"},
         "7":{"driver":"Nas","name":"F. NASR","team":"Porsche Penske Motorsport","vehicle":"PORSCHE 963","class":"GTP"}}
        """)!.AsObject();
    var track = JsonNode.Parse("""{"name":"Road America","sectors":{"1":{},"2":{},"3":{}}}""")!.AsObject();
    var standings = JsonNode.Parse("""
        {"displayDrivers":true,"hasClasses":true,"standings":{
          "1":{"data":"1;01;CLASSIFIED;1;10;1;0;1000000;TRACK;11;","class":"GTP","isRunning":true,"isCheckered":false,"bestLapTime":112373,"currentLoopSectors":"1;10000;2;20000;","previousLoopSectors":"1;10000;2;20000;"},
          "2":{"data":"2;24;CLASSIFIED;2;10;2;0;1000500;BOX;11;","class":"GTP","isRunning":true,"isCheckered":false,"bestLapTime":112124,"currentLoopSectors":"1;11000;","previousLoopSectors":"1;10000;2;20000;"},
          "3":{"data":"3;7;RETIRED;3;9;1;0;900000;STOPPED;10;","class":"GTP","isRunning":false,"isCheckered":false,"bestLapTime":113638,"currentLoopSectors":"1;9000;","previousLoopSectors":"1;10000;2;20000;"}}}
        """)!.AsObject();
    var status = JsonNode.Parse("""
        {"currentFlag":"GF","currentLap":11,"finalType":"BY_TIME","finalTime":600,"startTime":1000,"stopTime":0,
         "stoppedSeconds":0,"isSessionRunning":true,"isFinished":false,"isClosed":false,"isTimeFinished":false}
        """)!.AsObject();
    var best = JsonNode.Parse("""{"bestLap":{"participantNumber":"24","time":112124}}""")!.AsObject();
    var snapshot = ImsaTiming.BuildSnapshot(session, info, entries, track, standings, status, best,
        DateTimeOffset.FromUnixTimeSeconds(1100), ConnectionState.Connected);

    Equal(Championship.Imsa, snapshot.Championship);
    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal("Motul SportsCar Endurance Grand Prix", snapshot.Meeting);
    Equal("Road America", snapshot.Circuit);
    Equal(11, snapshot.CurrentLap);
    Equal<int?>(null, snapshot.TotalLaps);
    Equal("00:08:20", snapshot.TimeRemaining);
    Equal(3, snapshot.Competitors.Count);
    Equal("01", snapshot.Competitors[0].DriverId);
    Equal("01", snapshot.Competitors[0].CarNumber);
    Equal("WIT", snapshot.Competitors[0].Code);
    Equal("BMW", snapshot.Competitors[0].Manufacturer);
    Equal("GTP", snapshot.Competitors[0].Category);
    Equal("LEAD", snapshot.Competitors[0].GapToLeader);
    Equal("+1.500", snapshot.Competitors[1].GapToLeader);
    True(snapshot.Competitors[1].InPit);
    True(snapshot.Competitors[1].IsOverallFastest);
    Equal("1:52.124", snapshot.Competitors[1].ResultTime);
    True(snapshot.Competitors[2].Retired);
    True(snapshot.Competitors[2].Stopped);

    var qualifyingInfo = (JsonObject)info.DeepClone();
    qualifyingInfo["type"] = "QUALIFYING";
    var qualifyingStandings = (JsonObject)standings.DeepClone();
    qualifyingStandings["standings"]!["2"]!["bestLapTime"] = 112873;
    var qualifyingBest = JsonNode.Parse("""{"bestLap":{"participantNumber":"01","time":112373}}""")!.AsObject();
    var qualifying = ImsaTiming.BuildSnapshot(session, qualifyingInfo, entries, track, qualifyingStandings, status,
        qualifyingBest, DateTimeOffset.FromUnixTimeSeconds(1100), ConnectionState.Connected);
    Equal("+0.500", qualifying.Competitors[1].GapToLeader);

    var pausedStatus = (JsonObject)status.DeepClone();
    pausedStatus["isSessionRunning"] = false;
    pausedStatus["stopTime"] = 1100;
    Equal(SessionLifecycle.Live, ImsaTiming.BuildSnapshot(session, info, entries, track, standings, pausedStatus, best,
        DateTimeOffset.FromUnixTimeSeconds(1200), ConnectionState.Connected).Lifecycle);

    var lappedStandings = (JsonObject)standings.DeepClone();
    lappedStandings["standings"]!["3"]!["data"] = "3;7;CLASSIFIED;3;8;1;0;900000;TRACK;9;";
    lappedStandings["standings"]!["3"]!["isRunning"] = true;
    var lapped = ImsaTiming.BuildSnapshot(session, info, entries, track, lappedStandings, status, best,
        DateTimeOffset.FromUnixTimeSeconds(1100), ConnectionState.Connected);
    Equal("1 LAP", lapped.Competitors[2].GapToLeader);
}

static void FullCourseYellowAcrossSeries()
{
    // Al Kamel feeds (IMSA, Formula E) publish FCY as a whole-track flag.
    Equal(TrackCondition.FullCourseYellow,
        AlkamelTiming.TrackState(JsonNode.Parse("{\"currentFlag\":\"FCY\"}")!.AsObject()).Condition);
    Equal(TrackCondition.Yellow,
        AlkamelTiming.TrackState(JsonNode.Parse("{\"currentFlag\":\"YF\"}")!.AsObject()).Condition);

    // INDYCAR publishes one whole-track flag, so its caution neutralises the full course.
    Equal(TrackCondition.FullCourseYellow, IndyCarTiming.TrackState("CAUTION"));
    Equal(TrackCondition.FullCourseYellow, IndyCarTiming.TrackState("YELLOW"));
    Equal(TrackCondition.FullCourseYellow, IndyCarTiming.TrackState("FCY"));
    Equal(TrackCondition.AllClear, IndyCarTiming.TrackState("GREEN"));
    Equal(TrackCondition.RedFlag, IndyCarTiming.TrackState("RED"));

    // Griiip feeds (WEC, ELMS, Le Mans Cup, DTM) scope a race-level yellow by sectorNumbers.
    Equal(TrackCondition.FullCourseYellow,
        GriiipTiming.TrackState(JsonNode.Parse("[{\"flag\":\"Yellow\",\"sectorNumbers\":[]}]")!.AsArray()));
    Equal(TrackCondition.FullCourseYellow,
        GriiipTiming.TrackState(JsonNode.Parse("[{\"flag\":\"Yellow\"}]")!.AsArray()));
    Equal(TrackCondition.Yellow,
        GriiipTiming.TrackState(JsonNode.Parse("[{\"flag\":\"Yellow\",\"sectorNumbers\":[2]}]")!.AsArray()));
    Equal(TrackCondition.FullCourseYellow,
        GriiipTiming.TrackState(JsonNode.Parse("[{\"flag\":\"FullCourseYellow\"}]")!.AsArray()));
    Equal(TrackCondition.VirtualSafetyCar,
        GriiipTiming.TrackState(JsonNode.Parse("[{\"flag\":\"VirtualSafetyCar\"}]")!.AsArray()));
    Equal(TrackCondition.AllClear,
        GriiipTiming.TrackState(JsonNode.Parse("[{\"flag\":\"Green\"}]")!.AsArray()));

    // Display surfaces agree on the series-independent full-course yellow state.
    var clock = new FakeClock();
    var alerts = new AlertArbiter(clock);
    alerts.ApplyTrackCondition(TrackCondition.FullCourseYellow, "FULL COURSE YELLOW", 12, clock.UtcNow);
    Equal(AlertKind.FullCourseYellow, alerts.Current?.Kind);
    Equal("FULL COURSE YELLOW", alerts.Current?.Title);
    Equal(new TaskbarFlagBadge(TaskbarFlagStrip.Caution, "FCY"), TaskbarFlagDisplay.Badge(TrackCondition.FullCourseYellow));
    alerts.ApplyTrackCondition(TrackCondition.AllClear, null, 12, clock.UtcNow);
    Equal<RaceAlert?>(null, alerts.Current);
}

static void ImsaFlagsAndClock()
{
    Equal(TrackCondition.AllClear, ImsaTiming.TrackState(JsonNode.Parse("{\"currentFlag\":\"GF\"}")!.AsObject()).Condition);
    var fcy = ImsaTiming.TrackState(JsonNode.Parse("{\"currentFlag\":\"FCY\"}")!.AsObject());
    Equal(TrackCondition.FullCourseYellow, fcy.Condition);
    Equal("FULL COURSE YELLOW", fcy.Detail);
    Equal(TrackCondition.SafetyCar, ImsaTiming.TrackState(JsonNode.Parse("{\"currentFlag\":\"SC\"}")!.AsObject()).Condition);
    Equal(TrackCondition.RedFlag, ImsaTiming.TrackState(JsonNode.Parse("{\"currentFlag\":\"RF\"}")!.AsObject()).Condition);
    Equal("RET", ImsaTiming.StatusLabel("RETIRED", "STOPPED", false));
    Equal("N/C", ImsaTiming.StatusLabel("NOT_CLASSIFIED", "", false));
    Equal("N/S", ImsaTiming.StatusLabel("NOT_STARTED", "", false));
    Equal("DSQ", ImsaTiming.StatusLabel("DISQUALIFIED", "", false));
    Equal("FIN", ImsaTiming.StatusLabel("CLASSIFIED", "TRACK", true));
    Equal("OUT", ImsaTiming.StatusLabel("CLASSIFIED", "OUT_LAP", false));
    Equal("00:08:20", ImsaTiming.TimeRemaining(JsonNode.Parse(
        "{\"startTime\":1000,\"finalTime\":600,\"stopTime\":0,\"stoppedSeconds\":0,\"isSessionRunning\":true}")!.AsObject(),
        DateTimeOffset.FromUnixTimeSeconds(1100)));
    Equal("00:08:40", ImsaTiming.TimeRemaining(JsonNode.Parse(
        "{\"startTime\":1000,\"finalTime\":600,\"stopTime\":1100,\"stoppedSeconds\":20,\"isSessionRunning\":false}")!.AsObject(),
        DateTimeOffset.FromUnixTimeSeconds(1300)));
}

static void AlkamelSessionWindow()
{
    var now = new DateTimeOffset(2026, 8, 15, 16, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    True(AlkamelLiveTimingSource.IsCurrentSession(now - (long)TimeSpan.FromHours(1).TotalMilliseconds, 0, now));
    True(AlkamelLiveTimingSource.IsCurrentSession(now - (long)TimeSpan.FromHours(2).TotalMilliseconds, now + 1, now));
    False(AlkamelLiveTimingSource.IsCurrentSession(now - (long)TimeSpan.FromDays(13).TotalMilliseconds, 0, now));
    False(AlkamelLiveTimingSource.IsCurrentSession(now - (long)TimeSpan.FromHours(2).TotalMilliseconds, now - 1, now));
    True(AlkamelLiveTimingSource.IsCurrentSession(now - (long)TimeSpan.FromMinutes(5).TotalMilliseconds,
        now - (long)TimeSpan.FromMinutes(5).TotalMilliseconds, now));
    False(AlkamelLiveTimingSource.IsCurrentSession(now - (long)TimeSpan.FromHours(4).TotalMilliseconds,
        now - (long)TimeSpan.FromHours(4).TotalMilliseconds, now));
    False(AlkamelLiveTimingSource.IsCurrentSession(now + 1, 0, now));
    True(AlkamelLiveTimingSource.IsCurrentSession(now + (long)TimeSpan.FromMinutes(5).TotalMilliseconds,
        now + (long)TimeSpan.FromMinutes(5).TotalMilliseconds, now, (long)TimeSpan.FromMinutes(10).TotalMilliseconds));
}

static void ImsaSourceLifecycle()
{
    var sessionReference = ImsaSessionReference("session-1");
    var messages = new List<JsonObject>
    {
        new() { ["msg"] = "connected", ["session"] = "test" },
        DdpAdded("feeds", "feed", new JsonObject { ["running"] = true, ["sessions"] = new JsonArray { sessionReference.DeepClone() } }),
        DdpAdded("sessions", "session-1", new JsonObject { ["name"] = "Race", ["date"] = 1000000 }),
        DdpAdded("session_info", "info", new JsonObject { ["session"] = sessionReference.DeepClone(), ["info"] = JsonNode.Parse("{\"eventName\":\"Road America\",\"champName\":\"IMSA WeatherTech SportsCar Championship\",\"name\":\"RACE\",\"type\":\"RACE\",\"closed\":false}") }),
        DdpAdded("session_entry", "entry", new JsonObject { ["session"] = sessionReference.DeepClone(), ["entry"] = JsonNode.Parse("{\"01\":{\"driver\":\"Wit\",\"name\":\"M. WITTMANN\",\"team\":\"BMW M Team WRT\",\"vehicle\":\"BMW M HYBRID V8\",\"class\":\"GTP\"}}") }),
        DdpAdded("track_info", "track", new JsonObject { ["session"] = sessionReference.DeepClone(), ["track"] = JsonNode.Parse("{\"name\":\"Road America\",\"sectors\":{\"1\":{},\"2\":{},\"3\":{}}}") }),
        DdpAdded("session_status", "status", new JsonObject { ["session"] = sessionReference.DeepClone(), ["status"] = JsonNode.Parse("{\"currentFlag\":\"GF\",\"currentLap\":11,\"finalType\":\"BY_TIME\",\"finalTime\":600,\"startTime\":1000,\"isSessionRunning\":true,\"isFinished\":false}") }),
        DdpAdded("best_results", "best", new JsonObject { ["session"] = sessionReference.DeepClone(), ["bestResults"] = JsonNode.Parse("{\"bestLap\":{\"participantNumber\":\"01\"}}") }),
        DdpAdded("standings", "standings", new JsonObject { ["session"] = sessionReference.DeepClone(), ["standings"] = JsonNode.Parse("{\"standings\":{\"1\":{\"data\":\"1;01;CLASSIFIED;1;10;1;0;1000000;TRACK;11;\",\"class\":\"GTP\",\"isRunning\":true,\"bestLapTime\":112373,\"currentLoopSectors\":\"1;10000;\",\"previousLoopSectors\":\"1;10000;\"}}}") }),
        DdpChanged("feeds", "feed", new JsonObject { ["running"] = false }),
        new() { ["msg"] = "nosub", ["id"] = "2" },
        new() { ["msg"] = "nosub", ["id"] = "3" },
        new() { ["msg"] = "nosub", ["id"] = "4" },
        new() { ["msg"] = "nosub", ["id"] = "5" },
        new() { ["msg"] = "nosub", ["id"] = "6" },
        new() { ["msg"] = "nosub", ["id"] = "7" },
        new() { ["msg"] = "nosub", ["id"] = "8" },
        DdpChanged("feeds", "feed", new JsonObject { ["running"] = true })
    };
    var client = new FakeAlkamelDdpClient(messages);
    var clock = new FakeClock(DateTimeOffset.FromUnixTimeSeconds(1100));
    var alerts = new AlertArbiter(clock);
    var source = new ImsaLiveTimingSource(() => client, clock, new BlockingDelay(), alerts,
        new AlkamelTimingOptions(TimeSpan.FromHours(1), [TimeSpan.FromHours(1)]));
    TimingSnapshot? snapshot = null;
    List<FeedFailure> failures = [];
    source.Failed += failures.Add;
    using var ready = new ManualResetEventSlim();
    source.SnapshotReceived += value =>
    {
        if (value.Lifecycle != SessionLifecycle.Live) return;
        snapshot = value;
        ready.Set();
    };
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(ready.Wait(TimeSpan.FromSeconds(2)));
    True(SpinWait.SpinUntil(() => client.SubscriptionCount("sessions") >= 2, TimeSpan.FromSeconds(2)));
    Task.WhenAll(source.DisposeAsync().AsTask(), source.DisposeAsync().AsTask()).GetAwaiter().GetResult();
    Equal(Championship.Imsa, snapshot?.Championship);
    Equal("WIT", snapshot?.Competitors.Single().Code);
    Equal(AlertKind.SessionStart, alerts.Current?.Kind);
    Equal(0, failures.Count);
    True(client.Sent.Any(message => JsonSupport.String(message["name"]) == "livetimingFeed"));
    True(client.Sent.Any(message => JsonSupport.String(message["name"]) == "standings"));
    True(client.SubscriptionCount("sessions") >= 2);
}

static void FormulaEStandings()
{
    var session = JsonNode.Parse("""{"name":"Free Practice 2"}""")!.AsObject();
    var info = JsonNode.Parse("""{"eventName":"Example E-Prix","champName":"Formula E","name":"FREE PRACTICE 2","type":"FREE_PRACTICE","closed":false}""")!.AsObject();
    var entries = JsonNode.Parse("""
        {"9":{"driver":"AAA","name":"A. DRIVER","firstname":"Alex","lastname":"DRIVER","team":"Example Jaguar Team","vehicle":"JAGUAR I-TYPE 7"},
         "27":{"driver":"BBB","name":"B. RACER","firstname":"Blake","lastname":"RACER","team":"Example Porsche Team","vehicle":"PORSCHE 99X ELECTRIC"}}
        """)!.AsObject();
    var track = JsonNode.Parse("""{"name":"Example Circuit","shortName":"Example","numberOfLoops":9}""")!.AsObject();
    var standings = JsonNode.Parse("""
        {"displayDrivers":true,"hasClasses":false,"standings":{
          "1":{"data":"1;9;CLASSIFIED;0;21;2;0;1415549;TRACK;22;","isRunning":true,"isCheckered":false,"bestLapTime":67696,"currentLoopSectors":"0;0;","previousLoopSectors":"0;0;"},
          "2":{"data":"2;27;CLASSIFIED;0;20;2;0;1416000;BOX;21;","isRunning":true,"isCheckered":false,"bestLapTime":68196,"currentLoopSectors":"0;0;","previousLoopSectors":"0;0;"}}}
        """)!.AsObject();
    var status = JsonNode.Parse("""
        {"currentFlag":"YF","currentLap":22,"finalType":"BY_TIME","finalTime":2400,"startTime":1000,
         "stoppedSeconds":0,"isSessionRunning":true,"isFinished":false,"isClosed":false,"isTimeFinished":false}
        """)!.AsObject();
    var best = JsonNode.Parse("""{"bestLap":{"participantNumber":"9","time":67696}}""")!.AsObject();
    var snapshot = FormulaETiming.BuildSnapshot(session, info, entries, track, standings, status, best,
        DateTimeOffset.FromUnixTimeSeconds(1100), ConnectionState.Connected);

    Equal(Championship.FormulaE, snapshot.Championship);
    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal("Example E-Prix", snapshot.Meeting);
    Equal("Free Practice 2", snapshot.Session);
    Equal("Example Circuit", snapshot.Circuit);
    Equal(22, snapshot.CurrentLap);
    Equal("00:38:20", snapshot.TimeRemaining);
    Equal(TrackCondition.Yellow, snapshot.TrackCondition);
    Equal(2, snapshot.Competitors.Count);
    Equal("AAA", snapshot.Competitors[0].Code);
    Equal("Example Jaguar Team", snapshot.Competitors[0].Team);
    Equal("Jaguar", snapshot.Competitors[0].Manufacturer);
    Equal("LEAD", snapshot.Competitors[0].GapToLeader);
    Equal("+0.500", snapshot.Competitors[1].GapToLeader);
    Equal("Porsche", snapshot.Competitors[1].Manufacturer);
    Equal("1:08.196", snapshot.Competitors[1].ResultTime);
    True(snapshot.Competitors[1].InPit);
    True(snapshot.Competitors[0].IsOverallFastest);
}

static void FormulaEDuelPairing()
{
    var session = JsonNode.Parse("""{"name":"Qualifying Duel Quarter-Final 1"}""")!.AsObject();
    var info = JsonNode.Parse("""{"eventName":"Example E-Prix","name":"QUALIFYING DUEL QUARTER-FINAL 1","type":"RACE","closed":false}""")!.AsObject();
    var entries = JsonNode.Parse("""
        {"9":{"driver":"AAA","name":"A. DRIVER"},"27":{"driver":"BBB","name":"B. RACER"}}
        """)!.AsObject();
    var standings = JsonNode.Parse("""
        {"standings":{
          "1":{"data":"1;9;CLASSIFIED;0;1;0;0;1000;TRACK;1;","isRunning":true,"bestLapTime":68000},
          "2":{"data":"2;27;CLASSIFIED;0;1;0;0;1500;TRACK;1;","isRunning":true,"bestLapTime":68500},
          "3":{"data":"3;7;CLASSIFIED;0;1;0;0;1700;TRACK;1;","isRunning":true,"bestLapTime":68700},
          "4":{"data":"4;8;CLASSIFIED;0;1;0;0;1800;TRACK;1;","isRunning":true,"bestLapTime":68800}}}
        """)!.AsObject();
    var status = JsonNode.Parse("""{"currentFlag":"GF","currentLap":1,"startTime":1000,"isSessionRunning":true}""")!.AsObject();
    var snapshot = FormulaETiming.BuildSnapshot(session, info, entries, [], standings, status, null,
        DateTimeOffset.FromUnixTimeSeconds(1100), ConnectionState.Connected);

    Equal(2, snapshot.Competitors.Count);
    Equal("AAA", snapshot.Competitors[0].Code);
    Equal("BBB", snapshot.Competitors[1].Code);
}

static void FormulaEFutureRace()
{
    var now = new DateTimeOffset(2026, 8, 15, 11, 0, 0, TimeSpan.Zero);
    var session = new JsonObject
    {
        ["name"] = "Race",
        ["date"] = now.AddHours(3).ToUnixTimeMilliseconds()
    };
    var info = JsonNode.Parse("""{"eventName":"Example E-Prix","name":"RACE","type":"RACE","closed":false}""")!.AsObject();
    var entries = JsonNode.Parse("""{"9":{"driver":"AAA","name":"A. DRIVER"}}""")!.AsObject();
    var standings = JsonNode.Parse("""{"standings":{"1":{"data":"1;9;CLASSIFIED;0;0;0;0;1000;TRACK;1;","isRunning":true}}}""")!.AsObject();
    var status = new JsonObject
    {
        ["currentFlag"] = "GF",
        ["currentLap"] = 1,
        ["startTime"] = now.ToUnixTimeSeconds(),
        ["isSessionRunning"] = true
    };

    var future = FormulaETiming.BuildSnapshot(session, info, entries, [], standings, status, null, now, ConnectionState.Connected);
    Equal(SessionLifecycle.PreSession, future.Lifecycle);
    var imminentSession = (JsonObject)session.DeepClone();
    imminentSession["date"] = now.AddMinutes(5).ToUnixTimeMilliseconds();
    var imminent = FormulaETiming.BuildSnapshot(imminentSession, info, entries, [], standings, status, null,
        now, ConnectionState.Connected);
    Equal(SessionLifecycle.Live, imminent.Lifecycle);
}

static void FormulaESourceLifecycle()
{
    var sessionReference = ImsaSessionReference("fe-session");
    var messages = new List<JsonObject>
    {
        new() { ["msg"] = "connected", ["session"] = "test" },
        DdpAdded("feeds", "feed", new JsonObject { ["running"] = true, ["sessions"] = new JsonArray { sessionReference.DeepClone() } }),
        DdpAdded("sessions", "fe-session", new JsonObject { ["name"] = "Free Practice 2", ["date"] = 1000000 }),
        DdpAdded("session_info", "info", new JsonObject { ["session"] = sessionReference.DeepClone(), ["info"] = JsonNode.Parse("{\"eventName\":\"Example Circuit E-Prix\",\"name\":\"FREE PRACTICE 2\",\"type\":\"FREE_PRACTICE\",\"closed\":false}") }),
        DdpAdded("session_entry", "entry", new JsonObject { ["session"] = sessionReference.DeepClone(), ["entry"] = JsonNode.Parse("{\"9\":{\"driver\":\"AAA\",\"name\":\"A. DRIVER\",\"team\":\"Example Jaguar Team\",\"vehicle\":\"JAGUAR I-TYPE 7\"}}") }),
        DdpAdded("track_info", "track", new JsonObject { ["session"] = sessionReference.DeepClone(), ["track"] = JsonNode.Parse("{\"name\":\"Example Circuit\"}") }),
        DdpAdded("session_status", "status", new JsonObject { ["session"] = sessionReference.DeepClone(), ["status"] = JsonNode.Parse("{\"currentFlag\":\"GF\",\"currentLap\":22,\"finalType\":\"BY_TIME\",\"finalTime\":2400,\"startTime\":1000,\"isSessionRunning\":true,\"isFinished\":false}") }),
        DdpAdded("best_results", "best", new JsonObject { ["session"] = sessionReference.DeepClone(), ["bestResults"] = JsonNode.Parse("{\"bestLap\":{\"participantNumber\":\"9\"}}") }),
        DdpAdded("standings", "standings", new JsonObject { ["session"] = sessionReference.DeepClone(), ["standings"] = JsonNode.Parse("{\"standings\":{\"1\":{\"data\":\"1;9;CLASSIFIED;0;21;2;0;1415549;TRACK;22;\",\"isRunning\":true,\"bestLapTime\":67696,\"currentLoopSectors\":\"0;0;\",\"previousLoopSectors\":\"0;0;\"}}}") })
    };
    var client = new FakeAlkamelDdpClient(messages);
    var clock = new FakeClock(DateTimeOffset.FromUnixTimeSeconds(1100));
    var alerts = new AlertArbiter(clock);
    var source = new FormulaELiveTimingSource(() => client, clock, new BlockingDelay(), alerts,
        new AlkamelTimingOptions(TimeSpan.FromHours(1), [TimeSpan.FromHours(1)]));
    TimingSnapshot? snapshot = null;
    List<FeedFailure> failures = [];
    source.Failed += failures.Add;
    using var ready = new ManualResetEventSlim();
    source.SnapshotReceived += value =>
    {
        if (value.Lifecycle != SessionLifecycle.Live) return;
        snapshot = value;
        ready.Set();
    };
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(ready.Wait(TimeSpan.FromSeconds(2)));
    Task.WhenAll(source.DisposeAsync().AsTask(), source.DisposeAsync().AsTask()).GetAwaiter().GetResult();
    Equal(Championship.FormulaE, snapshot?.Championship);
    Equal("AAA", snapshot?.Competitors.Single().Code);
    Equal(AlertKind.SessionStart, alerts.Current?.Kind);
    Equal(0, failures.Count);
    var feedSubscription = client.Sent.First(message => JsonSupport.String(message["name"]) == "livetimingFeed");
    Equal("fiaformulae", JsonSupport.String(feedSubscription["params"]?[0]));
    True(client.Sent.Any(message => JsonSupport.String(message["name"]) == "standings"));
}

static void FormulaESourceRefreshesBetweenDuels()
{
    var sessionReference = ImsaSessionReference("fe-duel");
    var messages = new List<JsonObject>
    {
        new() { ["msg"] = "connected", ["session"] = "test" },
        DdpAdded("feeds", "feed", new JsonObject { ["running"] = true, ["sessions"] = new JsonArray { sessionReference.DeepClone() } }),
        DdpAdded("sessions", "fe-duel", new JsonObject { ["name"] = "Qualifying Duel Quarter-Final 1", ["date"] = 1000000, ["endDate"] = 1000000 }),
        DdpAdded("session_info", "info", new JsonObject { ["session"] = sessionReference.DeepClone(), ["info"] = JsonNode.Parse("{\"name\":\"QUALIFYING DUEL QUARTER-FINAL 1\",\"closed\":true}") }),
        new() { ["msg"] = "_sockjsHeartbeat" }
    };
    var client = new FakeAlkamelDdpClient(messages);
    var clock = new FakeClock(DateTimeOffset.FromUnixTimeSeconds(1100));
    var source = new FormulaELiveTimingSource(() => client, clock, new BlockingDelay(), new AlertArbiter(clock),
        new AlkamelTimingOptions(TimeSpan.FromHours(1), [TimeSpan.FromHours(1)]));

    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(SpinWait.SpinUntil(() => client.SubscriptionCount("sessionInfo") >= 2, TimeSpan.FromSeconds(2)));
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void DtmBootstrapTiming()
{
    var session = JsonNode.Parse("""
        {"id":100,"name":"Race 1","isRunning":true,"hasResult":false,"hasSeenChequered":false,
         "event":{"name":"06 N�rburgring"},"series":{"name":"DTM"}}
        """)!.AsObject();
    var bootstrap = JsonNode.Parse("""
        {"sessionInfo":{"eventName":"06 N�rburgring","sessionName":"Race 1","trackName":"N�RBURGRING","isStarted":true,"hasSeenChequered":false},
         "trackDetails":{"trackName":"N�RBURGRING"},
         "participants":[
           {"pid":1,"carNumber":"59","threeLettersName":"DÖR","displayName":"Ben Driver","teamName":"Example Motorsport","manufacturer":"McLaren 720S GT3 EVO","classId":""},
           {"pid":2,"carNumber":"91","threeLettersName":"PRE","displayName":"Pat Racer","teamName":"Example Racing","manufacturer":"Porsche 911 GT3 R EVO","classId":""}],
         "ranks":[{"pid":1,"overallPosition":1,"isDeleted":false},{"pid":2,"overallPosition":2,"isDeleted":false}],
         "gaps":[{"pid":1,"gapToFirstLaps":0,"gapToFirstMillis":0,"gapToAheadLaps":0,"gapToAheadMillis":0},
                 {"pid":2,"gapToFirstLaps":0,"gapToFirstMillis":2399,"gapToAheadLaps":0,"gapToAheadMillis":2399}],
         "bestLaps":[{"pid":1,"lapNumber":3,"lapTimeMillis":87335,"isValid":true},{"pid":2,"lapNumber":3,"lapTimeMillis":87400,"isValid":true}],
         "laps":[{"pid":1,"lapNumber":5,"lapTimeMillis":88000},{"pid":2,"lapNumber":4,"lapTimeMillis":89000}],
         "runningStatuses":[{"pid":1,"status":"Running"},{"pid":2,"status":"Retired"}],
         "carLocations":[{"pid":1,"carLocation":"Track"},{"pid":2,"carLocation":"Pit"}],
         "raceFlags":[{"flag":"Green"}],"chequeredFlags":[],
         "sessionLengthLimit":{"sessionLengthType":"TimePlusXLaps","lapsLimit":-1,"timeLimitSeconds":3600},
         "sessionClock":{"startTime":"2026-08-15T11:30:00Z","tsNow":"2026-08-15T11:40:00Z"}}
        """)!.AsObject();
    var snapshot = DtmTiming.BuildSnapshot(session, bootstrap,
        new DateTimeOffset(2026, 8, 15, 11, 40, 0, TimeSpan.Zero), ConnectionState.Connected);

    Equal(Championship.Dtm, snapshot.Championship);
    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal("Nürburgring", snapshot.Meeting);
    Equal("Nürburgring", snapshot.Circuit);
    Equal("Race 1", snapshot.Session);
    Equal(5, snapshot.CurrentLap);
    Equal("00:50:00", snapshot.TimeRemaining);
    Equal(TrackCondition.AllClear, snapshot.TrackCondition);
    Equal(2, snapshot.Competitors.Count);
    Equal("DÖR", snapshot.Competitors[0].Code);
    Equal("McLaren", snapshot.Competitors[0].Manufacturer);
    Equal("LEAD", snapshot.Competitors[0].GapToLeader);
    Equal("+2.399", snapshot.Competitors[1].GapToLeader);
    Equal("Porsche", snapshot.Competitors[1].Manufacturer);
    True(snapshot.Competitors[1].Retired);
    True(snapshot.Competitors[1].InPit);
    True(snapshot.Competitors[0].IsOverallFastest);
}

static void DtmQualifyingTiming()
{
    var session = JsonNode.Parse("""
        {"id":101,"name":"Qualifying 2","isRunning":true,"hasResult":false,"hasSeenChequered":false,
         "event":{"name":"06 Nürburgring"},"series":{"name":"DTM"}}
        """)!.AsObject();
    var bootstrap = JsonNode.Parse("""
        {"sessionInfo":{"eventName":"06 Nürburgring","sessionName":"Qualifying 2","trackName":"Nürburgring","isStarted":true},
         "participants":[
           {"pid":1,"carNumber":"10","threeLettersName":"MAP","displayName":"Marco Map","teamName":"Team A","manufacturer":"BMW"},
           {"pid":2,"carNumber":"130","threeLettersName":"ENG","displayName":"Emil Eng","teamName":"Team B","manufacturer":"Mercedes-AMG"},
           {"pid":3,"carNumber":"11","threeLettersName":"WIT","displayName":"Will Wit","teamName":"Team C","manufacturer":"BMW"}],
         "ranks":[],"gaps":[],"bestLaps":[],
         "laps":[
           {"pid":1,"lapNumber":3,"lapTimeMillis":95698,"isValid":true},
           {"pid":2,"lapNumber":3,"lapTimeMillis":95112,"isValid":true},
           {"pid":3,"lapNumber":2,"lapTimeMillis":611115,"isValid":false}],
         "runningStatuses":[],"carLocations":[],"raceFlags":[{"flag":"Green"}],"chequeredFlags":[],
         "sessionLengthLimit":{"sessionLengthType":"TimeOnly","lapsLimit":-1,"timeLimitSeconds":1200},
         "sessionClock":{"startTime":"2026-08-16T07:10:00Z","tsNow":"2026-08-16T07:22:00Z"}}
        """)!.AsObject();

    var snapshot = DtmTiming.BuildSnapshot(session, bootstrap,
        new DateTimeOffset(2026, 8, 16, 7, 22, 0, TimeSpan.Zero), ConnectionState.Connected);

    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal(3, snapshot.Competitors.Count);
    Equal("ENG", snapshot.Competitors[0].Code);
    Equal("LEAD", snapshot.Competitors[0].GapToLeader);
    Equal("MAP", snapshot.Competitors[1].Code);
    Equal("+0.586", snapshot.Competitors[1].GapToLeader);
    Equal("WIT", snapshot.Competitors[2].Code);
    Equal<string?>(null, snapshot.Competitors[2].GapToLeader);
    Equal("00:08:00", snapshot.TimeRemaining);
}

static void DtmSessionSelection()
{
    var sessions = JsonNode.Parse("""
        [{"id":1,"name":"Other","isRunning":true,"series":{"name":"Other"}},
         {"id":2,"name":"Practice","isRunning":false,"series":{"name":"DTM"}},
         {"id":3,"name":"Race 1","isRunning":true,"startTime":"2026-08-15T11:30:00Z","series":{"name":"DTM"}}]
        """)!.AsArray();
    Equal(3, JsonSupport.Int(DtmLiveTimingSource.ActiveSession(sessions)?["id"]));
}

static void GriiipEnduranceBootstrapTiming()
{
    var session = JsonNode.Parse("""
        {"id":18139,"name":"Free Practice 1","isRunning":true,"hasResult":false,"hasSeenChequered":false,
         "event":{"name":"FIA WEC - Lone Star Le Mans"},"series":{"name":"FIA World Endurance Championship"}}
        """)!.AsObject();
    var bootstrap = JsonNode.Parse("""
        {"sessionInfo":{"eventName":"FIA WEC - Lone Star Le Mans","sessionName":"Free Practice 1",
             "trackName":"Circuit of the Americas","isStarted":true,"hasSeenChequered":false},
         "participants":[
           {"pid":415019,"carNumber":"51","displayName":"AF CORSE","threeLettersName":"AFC",
            "teamName":"AF CORSE","manufacturer":"Ferrari 499P","classId":"HYPERCAR","currentDriverId":"2",
            "drivers":[{"externalDriverID":"1","displayName":"Driver One","threeLettersName":"ONE"},
                       {"externalDriverID":"2","displayName":"Antonio FUOCO","threeLettersName":"FUO"}]},
           {"pid":415030,"carNumber":"87","displayName":"AKKODIS ASP TEAM","threeLettersName":"ASP",
            "teamName":"AKKODIS ASP TEAM","manufacturer":"Lexus RC F LMGT3","classId":"LMGT3","currentDriverId":"3",
            "drivers":[{"externalDriverID":"1","displayName":"Petru UMBRĂRESCU","threeLettersName":"UMB"},
                       {"externalDriverID":"3","displayName":"Clemens SCHMID","threeLettersName":"SCH"}]}],
         "ranks":[{"pid":415019,"overallPosition":1},{"pid":415030,"overallPosition":2}],
         "gaps":[{"pid":415019,"gapToFirstMillis":0,"gapToAheadMillis":0},
                 {"pid":415030,"gapToFirstMillis":1301,"gapToAheadMillis":251}],
         "bestLaps":[{"pid":415019,"lapNumber":20,"lapTimeMillis":113824,"isValid":true},
                     {"pid":415030,"lapNumber":19,"lapTimeMillis":116100,"isValid":true}],
         "laps":[{"pid":415019,"lapNumber":24,"lapTimeMillis":115001},
                 {"pid":415030,"lapNumber":23,"lapTimeMillis":119140}],
         "runningStatuses":[{"pid":415019,"status":"Running"},{"pid":415030,"status":"Running"}],
         "carLocations":[{"pid":415019,"carLocation":"Track"},{"pid":415030,"carLocation":"Pit"}],
         "raceFlags":[{"flag":"Yellow"}],"chequeredFlags":[],
         "sessionLengthLimit":{"sessionLengthType":"TimeOnly","lapsLimit":-1,"timeLimitSeconds":5400},
         "sessionClock":{"startTime":"2026-09-04T16:30:00Z","tsNow":"2026-09-04T17:30:00Z"}}
        """)!.AsObject();

    var now = new DateTimeOffset(2026, 9, 4, 17, 30, 0, TimeSpan.Zero);
    var snapshot = GriiipTiming.BuildSnapshot(session, bootstrap, now, ConnectionState.Connected, GriiipSeries.Wec);

    Equal(Championship.WorldEnduranceChampionship, snapshot.Championship);
    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal("FIA WEC - Lone Star Le Mans", snapshot.Meeting);
    Equal("Circuit of the Americas", snapshot.Circuit);
    Equal("Free Practice 1", snapshot.Session);
    Equal(24, snapshot.CurrentLap);
    Equal("00:30:00", snapshot.TimeRemaining);
    Equal(TrackCondition.FullCourseYellow, snapshot.TrackCondition);
    Equal("FUO", snapshot.Competitors[0].Code);
    Equal("Antonio FUOCO", snapshot.Competitors[0].Name);
    Equal("51", snapshot.Competitors[0].CarNumber);
    Equal("#51  Antonio FUOCO", ChampionshipDisplay.FlyoutDriverName(snapshot.Championship, snapshot.Competitors[0]));
    Equal("Ferrari", snapshot.Competitors[0].Manufacturer);
    Equal("SCH", snapshot.Competitors[1].Code);
    Equal("Clemens SCHMID", snapshot.Competitors[1].Name);
    Equal("AKKODIS ASP TEAM", snapshot.Competitors[1].Team);
    Equal("LMGT3", snapshot.Competitors[1].Category);
    Equal("Lexus", snapshot.Competitors[1].Manufacturer);
    Equal("+1.301", snapshot.Competitors[1].GapToLeader);
    Equal("1:59.140", snapshot.Competitors[1].LastLapTime);
    True(snapshot.Competitors[1].InPit);
    Equal("Alpine", AlkamelTiming.ManufacturerFromVehicle("Alpine A424"));
    Equal("Genesis", AlkamelTiming.ManufacturerFromVehicle("Genesis GMR-001"));
    Equal("Peugeot", AlkamelTiming.ManufacturerFromVehicle("Peugeot 9X8"));
    Equal("Toyota", AlkamelTiming.ManufacturerFromVehicle("Toyota GR010 Hybrid"));

    var elms = GriiipTiming.BuildSnapshot(session, bootstrap, now, ConnectionState.Connected, GriiipSeries.Elms);
    Equal(Championship.EuropeanLeMansSeries, elms.Championship);
    Equal("FUO", elms.Competitors[0].Code);
    Equal("51", elms.Competitors[0].CarNumber);
    var michelinLeMansCup = GriiipTiming.BuildSnapshot(
        session, bootstrap, now, ConnectionState.Connected, GriiipSeries.MichelinLeMansCup);
    Equal(Championship.MichelinLeMansCup, michelinLeMansCup.Championship);
    Equal("FUO", michelinLeMansCup.Competitors[0].Code);
    Equal("51", michelinLeMansCup.Competitors[0].CarNumber);
}

static void WecEnergy()
{
    (double? Percent, EnergyLevel Expected)[] levels =
    [
        (100, EnergyLevel.Good), (50.1, EnergyLevel.Good),
        (50, EnergyLevel.Warning), (20.1, EnergyLevel.Warning),
        (20, EnergyLevel.Low), (0, EnergyLevel.Low),
        (null, EnergyLevel.Unavailable), (-1, EnergyLevel.Unavailable),
        (101, EnergyLevel.Unavailable), (double.NaN, EnergyLevel.Unavailable),
        (double.PositiveInfinity, EnergyLevel.Unavailable), (double.NegativeInfinity, EnergyLevel.Unavailable)
    ];
    foreach (var (percent, expected) in levels) Equal(expected, EnergyDisplay.Level(percent));
    var fixture = SectorFixture("wec-energy.json");
    var frames = fixture["frames"]!.AsArray();
    var first = WecEnergyTiming.Read(frames[0]!["carsEnergyTanks"]);
    Equal<double?>(89, first[424030]);
    Equal<double?>(82, first[424039]);
    Equal<double?>(81, WecEnergyTiming.Read(frames[1]!["carsEnergyTanks"])[424039]);
    var limits = new JsonObject { ["items"] = new JsonArray(fixture["unavailable"]!.DeepClone(), fixture["full"]!.DeepClone()) };
    Equal<double?>(null, WecEnergyTiming.Read(limits)[424035]);
    Equal<double?>(100, WecEnergyTiming.Read(limits)[424022]);
    var boundary = JsonNode.Parse("""
        {"items":[{"pid":1,"e":0},{"pid":2,"e":32.5},{"pid":3,"e":101},
          {"pid":4,"e":"NaN"},{"pid":5},{"pid":"invalid","e":20}]}
        """);
    var parsed = WecEnergyTiming.Read(boundary);
    Equal<double?>(0, parsed[1]);
    Equal<double?>(32.5, parsed[2]);
    Equal<double?>(null, parsed[3]);
    Equal<double?>(null, parsed[4]);
    Equal<double?>(null, parsed[5]);
    Equal(5, parsed.Count);
    Equal(0, WecEnergyTiming.Read(null).Count);

    var bootstrap = SectorFixture("wec-barcelona-fp1.json")["frames"]![0]!["bootstrap"]!.DeepClone().AsObject();
    bootstrap["carsEnergyTanks"] = frames[0]!["carsEnergyTanks"]!.DeepClone();
    var session = new JsonObject { ["isRunning"] = true };
    var wec = GriiipTiming.BuildSnapshot(session, bootstrap, DateTimeOffset.UtcNow, ConnectionState.Connected, GriiipSeries.Wec);
    Equal<double?>(89, wec.Competitors.Single(value => value.DriverId == "21").VirtualEnergyPercent);
    Equal<double?>(82, wec.Competitors.Single(value => value.DriverId == "50").VirtualEnergyPercent);
    bootstrap["carsEnergyTanks"] = null;
    True(GriiipTiming.BuildSnapshot(session, bootstrap, DateTimeOffset.UtcNow, ConnectionState.Connected,
        GriiipSeries.Wec).Competitors.All(value => value.VirtualEnergyPercent is null));
    bootstrap["carsEnergyTanks"] = frames[0]!["carsEnergyTanks"]!.DeepClone();
    True(GriiipTiming.BuildSnapshot(session, bootstrap, DateTimeOffset.UtcNow, ConnectionState.Connected,
        GriiipSeries.Elms).Competitors.All(value => value.VirtualEnergyPercent is null));

    var defaults = new UserSettings();
    True(defaults.DisplayFor(Championship.WorldEnduranceChampionship).FlyoutShowEnergy);
    foreach (var championship in UserSettings.AllChampionships.Where(value => value != Championship.WorldEnduranceChampionship))
        False(defaults.DisplayFor(championship).FlyoutShowEnergy);
    var legacy = System.Text.Json.JsonSerializer.Deserialize<ChampionshipDisplaySettings>("{}");
    True(legacy!.FlyoutShowEnergy);
    var folder = Path.Combine(Path.GetTempPath(), $"RaceDock-energy-{Guid.NewGuid():N}");
    try
    {
        var store = new UserSettingsStore(Path.Combine(folder, "settings.json"));
        store.Save(new UserSettings { ChampionshipDisplay = new ChampionshipDisplayProfiles
        {
            WorldEnduranceChampionship = new ChampionshipDisplaySettings
            {
                FlyoutShowEnergy = false,
                FlyoutColumnOrder = [FlyoutColumn.Energy, .. ChampionshipDisplaySettings.DefaultColumnOrder.Where(value => value != FlyoutColumn.Energy)]
            }
        } });
        var restored = store.Load().DisplayFor(Championship.WorldEnduranceChampionship);
        False(restored.FlyoutShowEnergy);
        Equal(FlyoutColumn.Energy, restored.FlyoutColumnOrder[0]);
    }
    finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
}

static void WecTyreAges()
{
    var fixture = SectorFixture("wec-tyre-ages.json");
    var frames = fixture["frames"]!.AsArray();
    var first = WecTyreTiming.Read(frames[0]!["tires"])[424040];
    Equal(6, first.FrontLeft?.AgeInLaps);
    Equal("MEDIUM", first.FrontLeft?.Compound);
    var mixed = WecTyreTiming.Read(frames[1]!["tires"])[424040];
    Equal(10, mixed.FrontLeft?.AgeInLaps);
    Equal(6, mixed.FrontRight?.AgeInLaps);
    Equal(10, mixed.RearLeft?.AgeInLaps);
    Equal(10, mixed.RearRight?.AgeInLaps);
    Equal(10, JsonSupport.Int(frames[1]!["completedLaps"]));

    var bootstrap = SectorFixture("wec-barcelona-fp1.json")["frames"]![0]!["bootstrap"]!.DeepClone().AsObject();
    var row = frames[1]!["tires"]![0]!.DeepClone().AsObject();
    row["pid"] = 424030;
    bootstrap["tires"] = new JsonArray(row);
    var session = new JsonObject { ["isRunning"] = true };
    var standing = GriiipTiming.BuildSnapshot(session, bootstrap, DateTimeOffset.UtcNow,
        ConnectionState.Connected, GriiipSeries.Wec).Competitors.Single(value => value.DriverId == "21");
    Equal(mixed, standing.CurrentTyres);
    True(standing.Lap != standing.CurrentTyres?.FrontRight?.AgeInLaps);
    var unsupported = GriiipTiming.BuildSnapshot(session, bootstrap, DateTimeOffset.UtcNow,
        ConnectionState.Connected, GriiipSeries.Elms);
    True(unsupported.Competitors.All(value => value.CurrentTyres is null));

    var changed = row.DeepClone().AsObject();
    changed["elapsedTimeMillis"] = (JsonSupport.Int(row["elapsedTimeMillis"]) ?? 0) + 1;
    changed["tires"] = JsonNode.Parse("""
        [{"id":"frontLeft","compound":"SOFT","ageInLaps":0},
         {"id":"frontRight","ageInLaps":-1},{"id":"rearLeft","compound":"MEDIUM"}]
        """);
    // Explicit provider ages may reset to zero; older records cannot restore the old age.
    var latest = WecTyreTiming.Read(new JsonArray(changed, row.DeepClone()))[424030];
    Equal(0, latest.FrontLeft?.AgeInLaps);
    Equal("SOFT", latest.FrontLeft?.Compound);
    Equal<int?>(null, latest.FrontRight?.AgeInLaps);
    Equal<int?>(null, latest.RearLeft?.AgeInLaps);
    Equal<WheelTyre?>(null, latest.RearRight);
    Equal(0, WecTyreTiming.Read(null).Count);
    bootstrap["tires"] = new JsonArray();
    True(GriiipTiming.BuildSnapshot(session, bootstrap, DateTimeOffset.UtcNow,
        ConnectionState.Connected, GriiipSeries.Wec).Competitors.All(value => value.CurrentTyres is null));
}

static void WecSectorTiming()
{
    var fixture = SectorFixture("wec-barcelona-fp1.json");
    var session = fixture["session"]!.AsObject();
    var frames = fixture["frames"]!.AsArray();
    var sectors = new GriiipSectorTiming();
    TimingSnapshot Read(int index) => GriiipTiming.BuildSnapshot(session, frames[index]!["bootstrap"]!.AsObject(),
        DateTimeOffset.Parse(JsonSupport.String(frames[index]!["timestamp"])!), ConnectionState.Connected, GriiipSeries.Wec, sectors);
    var first = Read(0);
    var entry = first.Competitors.Single(value => value.DriverId == "21");
    Equal("30.270", entry.CurrentSectors?.Sector1?.Value);
    Equal(SectorTimingState.PersonalBest, entry.CurrentSectors?.Sector1?.State);
    Equal("40.818", entry.CurrentSectors?.Sector2?.Value);
    Equal(SectorTimingState.OverallBest, entry.CurrentSectors?.Sector2?.State);
    Equal("30.332", entry.CurrentSectors?.Sector3?.Value);
    Equal(entry.CurrentSectors, entry.BestLapSectors);
    var best = entry.BestLapSectors;
    var hypercar = first.Competitors.Single(value => value.DriverId == "50");
    Equal("26.616", hypercar.CurrentSectors?.Sector1?.Value);
    Equal<SectorTiming?>(null, hypercar.CurrentSectors?.Sector2);
    Equal<SectorTiming?>(null, hypercar.CurrentSectors?.Sector3);

    entry = Read(1).Competitors.Single(value => value.DriverId == "21");
    Equal("31.001", entry.CurrentSectors?.Sector1?.Value);
    Equal(SectorTimingState.Slower, entry.CurrentSectors?.Sector1?.State);
    Equal<SectorTiming?>(null, entry.CurrentSectors?.Sector2);
    Equal<SectorTiming?>(null, entry.CurrentSectors?.Sector3);
    Equal(best, entry.BestLapSectors);
    True(Read(1).Competitors.All(value => value.CurrentMiniSectors is null));
    entry = Read(2).Competitors.Single(value => value.DriverId == "21");
    Equal("33.072", entry.CurrentSectors?.Sector1?.Value);
    Equal("44.836", entry.CurrentSectors?.Sector2?.Value);
    Equal(SectorTimingState.None, entry.CurrentSectors?.Sector2?.State);
    Equal("39.713", entry.CurrentSectors?.Sector3?.Value);
    Equal(best, entry.BestLapSectors);

    // The bounded provider history no longer contains lap four when joining the later snapshot.
    sectors.Reset();
    Equal<SectorTimingSet?>(null, Read(1).Competitors.Single(value => value.DriverId == "21").BestLapSectors);
    foreach (var series in new[] { GriiipSeries.Dtm, GriiipSeries.Elms, GriiipSeries.MichelinLeMansCup })
    {
        var unsupported = GriiipTiming.BuildSnapshot(session, frames[0]!["bootstrap"]!.AsObject(),
            DateTimeOffset.UtcNow, ConnectionState.Connected, series, sectors);
        True(unsupported.Competitors.All(value => value.CurrentSectors is null && value.BestLapSectors is null));
    }
}

static void WecSectorValidation()
{
    var fixture = SectorFixture("wec-barcelona-fp1.json");
    var session = fixture["session"]!.AsObject();
    var first = fixture["frames"]![0]!["bootstrap"]!.AsObject();
    var later = fixture["frames"]![1]!["bootstrap"]!.AsObject();
    var sectors = new GriiipSectorTiming();
    TimingSnapshot Read(JsonObject bootstrap) => GriiipTiming.BuildSnapshot(session, bootstrap,
        DateTimeOffset.UtcNow, ConnectionState.Connected, GriiipSeries.Wec, sectors);
    var best = Read(first).Competitors.Single(value => value.DriverId == "21").BestLapSectors;
    True(best is not null);
    var bestLap = later["bestLaps"]!.AsArray().OfType<JsonObject>().Single(value => JsonSupport.Int(value["pid"]) == 424030);
    bestLap["lapNumber"] = 16;
    bestLap["lapTimeMillis"] = 100000;
    Equal<SectorTimingSet?>(null, Read(later).Competitors.Single(value => value.DriverId == "21").BestLapSectors);

    // Matching the lap number is insufficient if the sectors do not sum to its published time.
    sectors.Reset();
    bestLap = first["bestLaps"]!.AsArray().OfType<JsonObject>().Single(value => JsonSupport.Int(value["pid"]) == 424030);
    bestLap["lapTimeMillis"] = 101421;
    Equal<SectorTimingSet?>(null, Read(first).Competitors.Single(value => value.DriverId == "21").BestLapSectors);
    bestLap["lapTimeMillis"] = 101420;
    var samples = first["sectors"]!.AsArray();
    var sector = samples.OfType<JsonObject>().Single(value => JsonSupport.Int(value["pid"]) == 424030 &&
        JsonSupport.Int(value["lapNumber"]) == 4 && JsonSupport.Int(value["sectorNumber"]) == 1);
    sector["isValid"] = false;
    Equal<SectorTimingSet?>(null, Read(first).Competitors.Single(value => value.DriverId == "21").BestLapSectors);
    sector["isValid"] = true;
    var duplicate = sector.DeepClone().AsObject();
    duplicate["sectorTimeMillis"] = 12345;
    duplicate["ts"] = "2026-10-16T09:00:00+00:00";
    samples.Add(duplicate);
    Equal(best, Read(first).Competitors.Single(value => value.DriverId == "21").BestLapSectors);
    // An explicit invalidation must clear a previously retained best-lap sector set as well.
    sector["isValid"] = false;
    Equal<SectorTimingSet?>(null, Read(first).Competitors.Single(value => value.DriverId == "21").BestLapSectors);
    sector["isValid"] = true;
    Equal(best, Read(first).Competitors.Single(value => value.DriverId == "21").BestLapSectors);
    first["bestLaps"] = new JsonArray();
    first["laps"] = new JsonArray();
    first["sectors"] = new JsonArray();
    first["currentLapSectors"] = new JsonObject();
    var empty = Read(first);
    True(empty.Competitors.All(value => value.CurrentSectors is null && value.BestLapSectors is null));
}

static void GriiipSessionSelection()
{
    var sessions = JsonNode.Parse("""
        [{"id":1,"name":"DTM","isRunning":true,"series":{"name":"DTM"}},
         {"id":2,"name":"WEC Future","isRunning":false,"series":{"name":"FIA World Endurance Championship"}},
         {"id":3,"name":"WEC Live","isRunning":true,"startTime":"2026-09-04T16:30:00Z","series":{"name":"FIA World Endurance Championship"}},
         {"id":4,"name":"ELMS Live","isRunning":true,"startTime":"2026-09-04T16:30:00Z","series":{"name":"European Le Mans Series"}},
         {"id":5,"name":"Le Mans Cup Live","isRunning":true,"startTime":"2026-09-04T16:30:00Z","series":{"name":"Michelin Le Mans Cup"}}]
        """)!.AsArray();
    Equal(3, JsonSupport.Int(GriiipLiveTimingSource.ActiveSession(sessions, GriiipSeries.Wec)?["id"]));
    Equal(4, JsonSupport.Int(GriiipLiveTimingSource.ActiveSession(sessions, GriiipSeries.Elms)?["id"]));
    Equal(5, JsonSupport.Int(GriiipLiveTimingSource.ActiveSession(sessions, GriiipSeries.MichelinLeMansCup)?["id"]));

    var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
    var options = new GriiipPollingOptions(
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(15), TimeSpan.FromHours(6),
        TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(1));
    var scheduled = JsonNode.Parse("""
        [{"id":6,"name":"WEC Later","isRunning":false,"startTime":"2026-09-04T16:00:00Z","series":{"name":"FIA World Endurance Championship"}}]
        """)!.AsArray();
    Equal(TimeSpan.FromHours(3.5), GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Wec, now, options));
    Equal(TimeSpan.FromSeconds(15), GriiipLiveTimingSource.NextIdleDelay(
        scheduled, GriiipSeries.Wec, now.AddHours(3.75), options));
    var start = now.AddHours(4);
    Equal(options.NearSessionInterval, GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Wec, start.AddSeconds(-1), options));
    Equal(options.NearSessionInterval, GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Wec, start, options));
    Equal(options.NearSessionInterval, GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Wec, start.AddMinutes(5), options));
    // A missing or invalid end time gets only a bounded recent-start grace.
    Equal(options.ScheduleRefreshInterval, GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Wec, start.Add(options.PreSessionLead), options));
    foreach (var invalidEnd in new[] { "not-a-date", start.ToString("O"), start.AddMinutes(-1).ToString("O") })
    {
        scheduled[0]!["endTime"] = invalidEnd;
        Equal(options.NearSessionInterval, GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Wec, start.AddMinutes(5), options));
        Equal(options.ScheduleRefreshInterval, GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Wec, start.Add(options.PreSessionLead), options));
    }
    scheduled[0]!["endTime"] = start.AddHours(1).ToString("O");
    Equal(options.NearSessionInterval, GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Wec, start.AddMinutes(45), options));
    Equal(options.ScheduleRefreshInterval, GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Wec, start.AddHours(1), options));
    scheduled[0]!["hideFromUsers"] = true;
    Equal(options.ScheduleRefreshInterval, GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Wec, start.AddMinutes(5), options));
    scheduled[0]!["hideFromUsers"] = false;
    Equal(options.ScheduleRefreshInterval, GriiipLiveTimingSource.NextIdleDelay(scheduled, GriiipSeries.Elms, start.AddMinutes(5), options));
    Equal(TimeSpan.FromHours(6), GriiipLiveTimingSource.NextIdleDelay([], GriiipSeries.Wec, now, options));
}

static void IndyCarPracticeTiming()
{
    var snapshot = IndyCarTiming.BuildSnapshot(IndyCarPracticePayload(),
        new DateTimeOffset(2026, 8, 15, 14, 25, 0, TimeSpan.Zero), ConnectionState.Connected);

    Equal(Championship.IndyCar, snapshot.Championship);
    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal("Ontario Honda Dealers Indy at Markham", snapshot.Meeting);
    Equal("Practice 1 - All Cars", snapshot.Session);
    Equal("Streets of Markham", snapshot.Circuit);
    Equal(10, snapshot.CurrentLap);
    Equal("00:21:52", snapshot.TimeRemaining);
    Equal("FP1 21:52", TaskbarSessionDisplay.Progress(snapshot));
    Equal(TrackCondition.AllClear, snapshot.TrackCondition);
    Equal(3, snapshot.Competitors.Count);
    Equal("KIR", snapshot.Competitors[0].Code);
    Equal("Andretti Global", snapshot.Competitors[0].Team);
    Equal("Honda", snapshot.Competitors[0].Manufacturer);
    Equal("LEAD", snapshot.Competitors[0].GapToLeader);
    True(snapshot.Competitors[0].InPit);
    True(snapshot.Competitors[0].IsOverallFastest);
    Equal("+0.3455", snapshot.Competitors[1].GapToLeader);
    Equal("+0.3455", snapshot.Competitors[1].IntervalToPositionAhead);
    Equal("Chevrolet", snapshot.Competitors[2].Manufacturer);
    Equal("P", snapshot.Competitors[2].Tyre);

    var otherSeries = IndyCarPracticePayload();
    otherSeries["timing_results"]!["heartbeat"]!["Series"] = "N";
    Equal(SessionLifecycle.OffSession, IndyCarTiming.BuildSnapshot(otherSeries,
        new DateTimeOffset(2026, 8, 15, 14, 25, 0, TimeSpan.Zero), ConnectionState.Connected).Lifecycle);
}

static void IndyCarSourceLifecycle()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 15, 14, 25, 0, TimeSpan.Zero));
    var api = new FakeIndyCarApiClient(new(IndyCarPracticePayload(), clock.UtcNow));
    var delay = new BlockingDelay();
    var alerts = new AlertArbiter(clock);
    var source = new IndyCarLiveTimingSource(api, clock, delay, alerts,
        new IndyCarPollingOptions(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)));
    List<TimingSnapshot> snapshots = [];
    List<FeedFailure> failures = [];
    var deltas = 0;
    source.SnapshotReceived += snapshots.Add;
    source.DeltaReceived += _ => deltas++;
    source.Failed += failures.Add;

    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();

    Equal(SessionLifecycle.Live, snapshots.Last().Lifecycle);
    Equal(Championship.IndyCar, snapshots.Last().Championship);
    Equal(TimeSpan.FromSeconds(2), delay.Duration);
    Equal(1, deltas);
    Equal(AlertKind.SessionStart, alerts.Current?.Kind);
    Equal(0, failures.Count);
}

static void IndyCarColdFeedLifecycle()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 16, 16, 0, 0, TimeSpan.Zero));
    var payload = IndyCarPracticePayload();
    payload["timing_results"]!["heartbeat"]!["SessionName"] = "Warmup";
    payload["timing_results"]!["heartbeat"]!["SessionStatus"] = "COLD";
    payload["timing_results"]!["heartbeat"]!["currentFlag"] = "COLD";
    var api = new FakeIndyCarApiClient(new(payload, clock.UtcNow));
    var delay = new BlockingDelay();
    var source = new IndyCarLiveTimingSource(api, clock, delay, new AlertArbiter(clock),
        new IndyCarPollingOptions(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)));
    List<TimingSnapshot> snapshots = [];
    List<ConnectionState> connections = [];
    List<FeedFailure> failures = [];
    source.SnapshotReceived += snapshots.Add;
    source.ConnectionChanged += connections.Add;
    source.Failed += failures.Add;

    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();

    Equal(SessionLifecycle.OffSession, snapshots.Last().Lifecycle);
    Equal(Championship.IndyCar, snapshots.Last().Championship);
    True(connections.Contains(ConnectionState.Connected));
    Equal(TimeSpan.FromMinutes(1), delay.Duration);
    Equal(0, failures.Count);
}

static void IndyCarStaleFeedLifecycle()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 8, 15, 14, 30, 0, TimeSpan.Zero));
    var api = new FakeIndyCarApiClient(new(IndyCarPracticePayload(), clock.UtcNow.AddMinutes(-3)));
    var delay = new BlockingDelay();
    var source = new IndyCarLiveTimingSource(api, clock, delay, new AlertArbiter(clock),
        new IndyCarPollingOptions(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)));
    List<TimingSnapshot> snapshots = [];
    List<ConnectionState> connections = [];
    source.SnapshotReceived += snapshots.Add;
    source.ConnectionChanged += connections.Add;

    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();

    Equal(SessionLifecycle.OffSession, snapshots.Last().Lifecycle);
    Equal(ConnectionState.Stale, snapshots.Last().ConnectionState);
    True(connections.Contains(ConnectionState.Stale));
    Equal(TimeSpan.FromMinutes(1), delay.Duration);
}

static JsonObject NascarFixture(string name) => JsonNode.Parse(File.ReadAllText(
    Path.Combine(AppContext.BaseDirectory, "Fixtures", "Nascar", name)))!.AsObject();

static NascarFeedOptions TestNascarFeedOptions() => new(
    TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(30), TimeSpan.FromHours(4), TimeSpan.FromHours(6), TimeSpan.FromSeconds(30));

static void F1AcademyCalendar()
{
    var races = F1AcademyScheduleClient.ReadRaces(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "F1Academy", "calendar-2026.html")));
    var utc = TimeZoneInfo.Utc;

    // During the Austin round, its remaining confirmed sessions keep their published times.
    var austin = ChampionshipCalendars.F1AcademyRound(races, new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero), utc);
    Equal(2, austin.Count);
    Equal("United States Grand Prix", austin[0].EventName);
    Equal("Circuit of the Americas", austin[0].Venue);
    Equal("Reverse Grid Race", austin[0].SessionName);
    True(austin[0].HasStartTime);
    Equal(new DateTimeOffset(2026, 10, 24, 23, 0, 0, TimeSpan.Zero), austin[0].StartUtc);
    Equal(Championship.F1Academy, austin[0].Championship);

    // Once Austin is over, Las Vegas follows. The series has not confirmed its times (its page shows TBC), so each
    // session keeps only its local date rather than the placeholder clock time.
    var lasVegas = ChampionshipCalendars.F1AcademyRound(races, new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero), utc);
    Equal(4, lasVegas.Count);
    Equal("Las Vegas Grand Prix", lasVegas[0].EventName);
    Equal("Free Practice", lasVegas[0].SessionName);
    False(lasVegas[0].HasStartTime);
    Equal(new DateOnly(2026, 11, 19), lasVegas[0].StartDate);
    Equal(new DateOnly(2026, 11, 21), lasVegas[^1].StartDate);

    // Every round of the published season names its grand prix rather than its circuit.
    foreach (var (circuit, grandPrix) in new[]
    {
        ("Shanghai", "Chinese Grand Prix"),
        ("Montreal", "Canadian Grand Prix"),
        ("Silverstone", "British Grand Prix"),
        ("Zandvoort", "Dutch Grand Prix"),
        ("Austin", "United States Grand Prix"),
        ("Las Vegas", "Las Vegas Grand Prix")
    })
        Equal(grandPrix, ChampionshipCalendars.F1AcademyGrandPrixName(circuit));
    Equal(null, ChampionshipCalendars.F1AcademyGrandPrixName("Sepang"));
    Equal(null, ChampionshipCalendars.F1AcademyGrandPrixName(null));

    // After the final round there is nothing left to show.
    Equal(0, ChampionshipCalendars.F1AcademyRound(races, new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), utc).Count);
    Equal(0, ChampionshipCalendars.F1AcademyRound(null, DateTimeOffset.UtcNow, utc).Count);
    // A page without its data block is a layout change, reported rather than read as an empty season.
    try { F1AcademyScheduleClient.ReadRaces("<html></html>"); True(false); }
    catch (InvalidDataException) { }
}

/// <summary>
/// The published season list naming one race at the given time, in the shape the provider uses: the race list's own
/// dates are US Eastern wall-clock times, and the UTC start comes from the race's <c>schedule[]</c>.
/// </summary>
static JsonArray NascarRaceList(DateTimeOffset start) => JsonNode.Parse($$"""
    [ { "race_id": 5546, "race_name": "Daytona 500", "track_name": "Daytona International Speedway",
        "date_scheduled": "1900-01-01T00:00:00", "race_date": "1900-01-01T00:00:00",
        "schedule": [ { "run_type": 3, "start_time_utc": "{{start.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss")}}" } ] } ]
    """)!.AsArray();

static HttpClient NascarFeedHttpClient(NascarFakeHttp handler) =>
    new(handler) { BaseAddress = new Uri("https://cf.nascar.com/") };

static (
    NascarLiveTimingSource Source,
    List<TimingSnapshot> Snapshots,
    List<ConnectionState> Connections,
    List<FeedFailure> Failures) AttachNascarSource(NascarFeed feed, FakeClock clock, NascarSeries? series = null,
    AlertArbiter? alerts = null)
{
    var source = new NascarLiveTimingSource(feed, series ?? NascarSeries.Cup, clock, alerts ?? new AlertArbiter(clock));
    List<TimingSnapshot> snapshots = [];
    List<ConnectionState> connections = [];
    List<FeedFailure> failures = [];
    source.SnapshotReceived += snapshots.Add;
    source.ConnectionChanged += connections.Add;
    source.Failed += failures.Add;
    return (source, snapshots, connections, failures);
}

/// <summary>One tick of a feed whose race started ten minutes ago, against <paramref name="payload"/>.</summary>
static (List<TimingSnapshot> Snapshots, List<ConnectionState> Connections, List<FeedFailure> Failures, TimeSpan? Delay)
    RunNascarTick(JsonObject payload, FakeClock clock, bool failLiveFeed = false)
{
    var delay = new BlockingDelay();
    var http = new NascarFakeHttp("live/feeds/live-feed.json", payload.ToJsonString()) { FailLiveFeed = failLiveFeed };
    var schedule = new FakeNascarScheduleClient(NascarRaceList(clock.UtcNow.AddMinutes(-10)));
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    var rig = AttachNascarSource(feed, clock);
    rig.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    if (!delay.WaitUntilCalled(TimeSpan.FromSeconds(2))) throw new Exception("The NASCAR feed never ticked.");
    rig.Source.StopAsync().GetAwaiter().GetResult();
    rig.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    return (rig.Snapshots, rig.Connections, rig.Failures, delay.Duration);
}

static void NascarCupRaceTiming()
{
    var snapshot = NascarTiming.BuildSnapshot(NascarFixture("cup-race.json"),
        new DateTimeOffset(2026, 10, 22, 18, 35, 0, TimeSpan.Zero), ConnectionState.Connected);

    Equal(Championship.NascarCup, snapshot.Championship);
    Equal(SessionLifecycle.Live, snapshot.Lifecycle);
    Equal("DAYTONA 500", snapshot.Meeting);
    Equal("Race", snapshot.Session);
    Equal("Daytona International Speedway", snapshot.Circuit);
    Equal(45, snapshot.CurrentLap);
    Equal(200, snapshot.TotalLaps);
    Equal(null, snapshot.TimeRemaining);
    Equal(TrackCondition.AllClear, snapshot.TrackCondition);
    Equal(1, snapshot.StageNumber);
    Equal(65, snapshot.StageFinishLap);
    Equal(65, snapshot.StageLength);
    Equal(6, snapshot.Competitors.Count);

    var leader = snapshot.Competitors[0];
    Equal("5", leader.CarNumber);
    Equal("LAR", leader.Code);
    Equal("Kyle Larson", leader.Name);
    Equal("HendrickCars.com", leader.Team);
    Equal("Chevrolet", leader.Manufacturer);
    Equal("LEAD", leader.GapToLeader);
    Equal(null, leader.IntervalToPositionAhead);
    Equal(45, leader.Lap);
    False(leader.InPit);
    Equal("0:46.312", leader.ResultTime);
    Equal("0:46.588", leader.LastLapTime);
    Equal(true, leader.IsPlayoffEligible);
    True(leader.IsOverallFastest);
    Equal(null, leader.StatusLabel);

    var second = snapshot.Competitors[1];
    Equal("ELL", second.Code);
    Equal("Ford", second.Manufacturer);
    Equal("+1.842", second.GapToLeader);
    Equal("+1.842", second.IntervalToPositionAhead);
    Equal("0:46.400", second.ResultTime);
    Equal(true, second.IsPlayoffEligible);
    False(second.IsOverallFastest);

    Equal("HAM", snapshot.Competitors[2].Code);
    Equal("Toyota", snapshot.Competitors[2].Manufacturer);
    Equal("+2.105", snapshot.Competitors[2].GapToLeader);
    Equal("+0.263", snapshot.Competitors[2].IntervalToPositionAhead);
    Equal("0:46.351", snapshot.Competitors[2].ResultTime);

    // DVP parks a damaged car for repairs, so it is a status label rather than a pit stop.
    var damaged = snapshot.Competitors[3];
    Equal("DVP", damaged.StatusLabel);
    False(damaged.InPit);
    Equal(false, damaged.IsPlayoffEligible);

    Equal(44, snapshot.Competitors[4].Lap);

    // A car that has left the session keeps its position but reports no more lap times.
    var outCar = snapshot.Competitors[5];
    Equal("OUT", outCar.StatusLabel);
    Equal(null, outCar.LastLapTime);
    Equal("0:46.981", outCar.ResultTime);
}

static void NascarCupTimedSessionTiming()
{
    var now = new DateTimeOffset(2026, 10, 21, 21, 10, 0, TimeSpan.Zero);
    var practice = NascarTiming.BuildSnapshot(NascarFixture("cup-practice.json"), now, ConnectionState.Connected);
    Equal("Practice", practice.Session);
    Equal(null, practice.TotalLaps);
    Equal(null, practice.TimeRemaining);
    // Practice publishes a placeholder stage, which is not a stage being run.
    Equal(null, practice.StageNumber);
    Equal(null, practice.StageFinishLap);
    Equal(null, practice.StageLength);
    Equal(2, practice.Competitors.Count);
    Equal("Ryan Blaney", practice.Competitors[0].Name);
    Equal("0:46.912", practice.Competitors[0].ResultTime);
    True(practice.Competitors[0].IsOverallFastest);
    Equal(null, practice.Competitors[1].ResultTime);
    Equal(null, practice.Competitors[1].GapToLeader);

    var qualifying = NascarTiming.BuildSnapshot(NascarFixture("cup-qualifying.json"), now, ConnectionState.Connected);
    Equal("Qualifying", qualifying.Session);
    Equal(null, qualifying.TotalLaps);
    Equal(null, qualifying.TimeRemaining);
    // Qualifying can still carry a bogus stage object; stages only apply to races.
    qualifying = NascarTiming.BuildSnapshot(qualifyingPayloadWithStage(), now, ConnectionState.Connected);
    Equal(null, qualifying.StageNumber);
    Equal(null, qualifying.StageFinishLap);
    Equal(null, qualifying.StageLength);
    Equal("Joey Logano", qualifying.Competitors[0].Name);
    Equal("0:47.204", qualifying.Competitors[0].ResultTime);
    Equal("+0.147", qualifying.Competitors[1].GapToLeader);
    Equal(null, qualifying.Competitors[2].ResultTime);
}

static JsonObject qualifyingPayloadWithStage()
{
    var payload = NascarFixture("cup-qualifying.json");
    payload["stage"] = JsonNode.Parse("""{"stage_num":1,"finish_at_lap":25,"laps_in_stage":25}""");
    return payload;
}

static void NascarCupFlagsAndLifecycle()
{
    // Only the green, yellow and red codes are mapped; everything else stays unproven.
    Equal(TrackCondition.AllClear, NascarTiming.TrackState(1));
    Equal(TrackCondition.AllClear, NascarTiming.TrackState(4));
    Equal(TrackCondition.AllClear, NascarTiming.TrackState(5));
    Equal(TrackCondition.FullCourseYellow, NascarTiming.TrackState(2));
    Equal(TrackCondition.RedFlag, NascarTiming.TrackState(3));
    Equal(TrackCondition.Unknown, NascarTiming.TrackState(0));
    Equal(TrackCondition.Unknown, NascarTiming.TrackState(9));
    Equal(TrackCondition.Unknown, NascarTiming.TrackState(null));

    var now = new DateTimeOffset(2026, 10, 22, 18, 35, 0, TimeSpan.Zero);

    // The endpoint keeps answering between sessions with an empty field.
    var empty = NascarFixture("cup-race.json");
    empty["vehicles"] = new JsonArray();
    Equal(SessionLifecycle.OffSession, NascarTiming.BuildSnapshot(empty, now, ConnectionState.Connected).Lifecycle);

    // Xfinity and Trucks share the endpoint, so only the Cup series id starts a session.
    var otherSeries = NascarFixture("cup-race.json");
    otherSeries["series_id"] = 2;
    Equal(SessionLifecycle.OffSession, NascarTiming.BuildSnapshot(otherSeries, now, ConnectionState.Connected).Lifecycle);

    var checkered = NascarFixture("cup-race.json");
    checkered["flag_state"] = 5;
    var ended = NascarTiming.BuildSnapshot(checkered, now, ConnectionState.Connected);
    Equal(SessionLifecycle.Ended, ended.Lifecycle);
    Equal(TrackCondition.AllClear, ended.TrackCondition);

    // The chequered code is not always published; the last lap with nothing left to run ends it too.
    var finalLap = NascarFixture("cup-race.json");
    finalLap["laps_to_go"] = 0;
    finalLap["lap_number"] = 200;
    Equal(SessionLifecycle.Ended, NascarTiming.BuildSnapshot(finalLap, now, ConnectionState.Connected).Lifecycle);

    // A practice run also carries laps_to_go 0, which only ends a race.
    Equal(SessionLifecycle.Live, NascarTiming.BuildSnapshot(NascarFixture("cup-practice.json"), now,
        ConnectionState.Connected).Lifecycle);

    // Flag 8 was recorded an hour before green and between qualifying rounds: the field is listed but
    // nothing is on track, so neither a race nor qualifying is live yet and no track state is claimed.
    Equal(TrackCondition.Unknown, NascarTiming.TrackState(8));
    var gridded = NascarFixture("cup-race.json");
    gridded["flag_state"] = 8;
    gridded["lap_number"] = 0;
    var preRace = NascarTiming.BuildSnapshot(gridded, now, ConnectionState.Connected);
    Equal(SessionLifecycle.PreSession, preRace.Lifecycle);
    Equal(TrackCondition.Unknown, preRace.TrackCondition);
    var betweenRounds = NascarFixture("cup-qualifying.json");
    betweenRounds["flag_state"] = 8;
    Equal(SessionLifecycle.PreSession, NascarTiming.BuildSnapshot(betweenRounds, now,
        ConnectionState.Connected).Lifecycle);

    // The live endpoint uses stopped (9) to terminate practice and qualifying too.
    var stoppedQualifying = NascarFixture("cup-qualifying.json");
    stoppedQualifying["flag_state"] = 9;
    Equal(SessionLifecycle.Ended, NascarTiming.BuildSnapshot(stoppedQualifying, now,
        ConnectionState.Connected).Lifecycle);
}

static void NascarPitRoad()
{
    var now = new DateTimeOffset(2026, 10, 22, 18, 35, 0, TimeSpan.Zero);
    JsonObject Race(Action<JsonArray> change, int flag = 1)
    {
        var payload = NascarFixture("cup-race.json");
        payload["flag_state"] = flag;
        change(payload["vehicles"]!.AsArray());
        return payload;
    }
    CompetitorStanding Car(TimingSnapshot snapshot, JsonObject vehicle) =>
        snapshot.Competitors.Single(value => value.CarNumber == vehicle["vehicle_number"]!.GetValue<string>());

    // A running car the feed reports off track during the session is on pit road.
    JsonObject? pitted = null;
    var live = NascarTiming.BuildSnapshot(Race(vehicles =>
    {
        pitted = vehicles.OfType<JsonObject>().First(value => JsonSupport.Int(value["status"]) == 1 &&
            !JsonSupport.Bool(value["is_on_dvp"]) && JsonSupport.Bool(value["is_on_track"]));
        pitted["is_on_track"] = false;
    }), now, ConnectionState.Connected);
    Equal(SessionLifecycle.Live, live.Lifecycle);
    True(Car(live, pitted!).InPit);
    Equal(null, Car(live, pitted!).StatusLabel);
    Equal(1, live.Competitors.Count(value => value.InPit));

    // DVP and out-of-session cars keep their own status even off track, and a missing flag proves nothing.
    JsonObject? damaged = null, retired = null, unknown = null;
    var mixed = NascarTiming.BuildSnapshot(Race(vehicles =>
    {
        var running = vehicles.OfType<JsonObject>()
            .Where(value => JsonSupport.Int(value["status"]) == 1 && !JsonSupport.Bool(value["is_on_dvp"])).ToList();
        damaged = running[0];
        damaged["is_on_dvp"] = true;
        damaged["is_on_track"] = false;
        unknown = running[1];
        unknown.Remove("is_on_track");
        retired = vehicles.OfType<JsonObject>().First(value => JsonSupport.Int(value["status"]) == 0);
        retired["is_on_track"] = false;
    }), now, ConnectionState.Connected);
    False(Car(mixed, damaged!).InPit);
    Equal("DVP", Car(mixed, damaged!).StatusLabel);
    False(Car(mixed, retired!).InPit);
    Equal("OUT", Car(mixed, retired!).StatusLabel);
    False(Car(mixed, unknown!).InPit);

    // The grid forms off track before green, the field leaves the track after the stop flag, and a red flag parks it
    // (often on pit road) while the session stays live.
    foreach (var flag in new[] { 8, 9, 3 })
    {
        var parked = NascarTiming.BuildSnapshot(Race(vehicles =>
        {
            foreach (var vehicle in vehicles.OfType<JsonObject>()) vehicle["is_on_track"] = false;
        }, flag), now, ConnectionState.Connected);
        True(parked.Lifecycle != SessionLifecycle.Live || parked.TrackCondition == TrackCondition.RedFlag);
        False(parked.Competitors.Any(value => value.InPit));
    }
}

static JsonArray NascarCupRaceList() => JsonNode.Parse("""
    [
      { "race_name": "Daytona 500", "track_name": "Daytona International Speedway",
        "date_scheduled": "2026-02-15T14:30:00", "race_date": "1900-01-01T00:00:00" },
      { "race_name": "Auto Club 400", "track_name": "Auto Club Speedway",
        "date_scheduled": "1900-01-01T00:00:00", "race_date": "1900-01-01T00:00:00" },
      { "race_name": "Shriners Children's 500", "track_name": "Phoenix Raceway",
        "date_scheduled": "2026-02-08T15:00:00", "race_date": "1900-01-01T00:00:00" },
      { "race_name": "Pennzoil 400", "track_name": "Las Vegas Motor Speedway",
        "date_scheduled": "2026-03-01T00:00:00", "race_date": "2026-03-01T00:00:00" },
      { "race_name": "EchoPark Automotive Grand Prix", "track_name": "Circuit of the Americas",
        "date_scheduled": "2026-03-08T15:30:00", "race_date": "1900-01-01T00:00:00" }
    ]
    """)!.AsArray();

static void NascarCupCalendarSelection()
{
    var races = NascarCupRaceList();

    // The released race carries a clock time; the 1900-01-01 sentinel names a date not out yet.
    var daytona = ChampionshipCalendars.NextNascarCup(races, new DateTimeOffset(2026, 2, 10, 12, 0, 0, TimeSpan.Zero));
    Equal(Championship.NascarCup, daytona!.Championship);
    Equal("Daytona 500", daytona.EventName);
    Equal(null, daytona.SessionName);
    Equal("Daytona International Speedway", daytona.Venue);
    // The race list's own dates are US Eastern wall-clock times: 14:30 EST is 19:30 UTC.
    Equal(new DateTimeOffset(2026, 2, 15, 19, 30, 0, TimeSpan.Zero), daytona.StartUtc);
    True(daytona.HasStartTime);
    Equal(null, daytona.StartDate);

    // A race published without a clock time names a day instead.
    var lasVegas = ChampionshipCalendars.NextNascarCup(races, new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero));
    Equal("Pennzoil 400", lasVegas!.EventName);
    False(lasVegas.HasStartTime);
    Equal(new DateOnly(2026, 3, 1), lasVegas.StartDate);

    // The dated race stays selected for its whole published day, then the following round takes over.
    Equal("Pennzoil 400", ChampionshipCalendars.NextNascarCup(races,
        new DateTimeOffset(2026, 3, 1, 22, 0, 0, TimeSpan.Zero))!.EventName);
    var austin = ChampionshipCalendars.NextNascarCup(races, new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero));
    Equal("EchoPark Automotive Grand Prix", austin!.EventName);
    True(austin.HasStartTime);

    // With no race ahead there is nothing to count down to.
    Equal(null, ChampionshipCalendars.NextNascarCup(races, new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero)));
    Equal(null, ChampionshipCalendars.NextNascarCup(null, new DateTimeOffset(2026, 2, 10, 12, 0, 0, TimeSpan.Zero)));
}

static void NascarWeekendCalendarSessions()
{
    var races = JsonNode.Parse("""
        [ { "race_id": 5631, "race_name": "YellaWood 500", "track_name": "Talladega Superspeedway",
            "date_scheduled": "2026-10-24T16:00:00", "schedule": [
              { "run_type": 3, "start_time_utc": "2026-10-25T18:00:00" } ] } ]
        """)!.AsArray();
    var weekend = JsonNode.Parse("""
        { "weekend_race": [ { "schedule": [
            { "event_name": "Garage Hours", "run_type": 0, "start_time_utc": "2026-10-24T15:00:00" },
            { "event_name": "Qualifying (Impound)", "run_type": 2, "start_time_utc": "2026-10-24T17:00:00" },
            { "event_name": "Race", "run_type": 3, "start_time_utc": "2026-10-25T18:00:00" }
        ] } ] }
        """)!.AsObject();

    var sessions = ChampionshipCalendars.NascarEvent(
        races, weekend, new DateTimeOffset(2026, 10, 24, 16, 15, 0, TimeSpan.Zero), Championship.NascarXfinity);
    Equal(2, sessions.Count);
    Equal(Championship.NascarXfinity, sessions[0].Championship);
    Equal("Qualifying (Impound)", sessions[0].SessionName);
    Equal(new DateTimeOffset(2026, 10, 24, 17, 0, 0, TimeSpan.Zero), sessions[0].StartUtc);
    Equal(null, sessions[1].SessionName);
    Equal(new DateTimeOffset(2026, 10, 25, 18, 0, 0, TimeSpan.Zero), sessions[1].StartUtc);
}

static void NascarCupSettings()
{
    Equal(16, (int)Championship.NascarCup);

    // The feed carries no tyres and no team (its only team-like field is the sponsor), so neither column shows,
    // even for a profile that turned them on before. INDYCAR does publish tyres and keeps its column.
    var everything = new ChampionshipDisplaySettings
    {
        FlyoutShowTyre = true, FlyoutShowTeamNames = true, TaskbarUseManufacturerLogos = true, FlyoutUseManufacturerLogos = true
    };
    var profiles = new ChampionshipDisplayProfiles();
    foreach (var series in new[] { Championship.NascarCup, Championship.NascarXfinity, Championship.NascarTrucks, Championship.IndyCar })
        profiles = profiles.With(series, everything);
    var stored = new UserSettings { ChampionshipDisplay = profiles };
    foreach (var series in new[] { Championship.NascarCup, Championship.NascarXfinity, Championship.NascarTrucks })
    {
        False(stored.DisplayFor(series).FlyoutShowTyre);
        False(stored.DisplayFor(series).FlyoutShowTeamNames);
        False(stored.DisplayFor(series).FlyoutShowSectorTimes);
        // The car number identifies an entry; no manufacturer mark replaces or accompanies it.
        False(stored.DisplayFor(series).TaskbarUseManufacturerLogos);
        False(stored.DisplayFor(series).FlyoutUseManufacturerLogos);
        True(stored.DisplayFor(series).FlyoutShowIntervals);
        True(stored.DisplayFor(series).FlyoutShowCategoryOrLap);
    }
    True(stored.DisplayFor(Championship.IndyCar).FlyoutShowTyre);
    True(stored.DisplayFor(Championship.IndyCar).FlyoutShowTeamNames);
    True(stored.DisplayFor(Championship.IndyCar).TaskbarUseManufacturerLogos);

    var defaults = new UserSettings().Normalize();
    False(defaults.EnableNascarCup);
    False(defaults.IsChampionshipEnabled(Championship.NascarCup));
    False(defaults.IsChampionshipVisible(Championship.NascarCup));

    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock.Tests.{Guid.NewGuid():N}");
    var path = Path.Combine(directory, "settings.json");
    try
    {
        var store = new UserSettingsStore(path);
        // A file written before NASCAR existed must not start polling it behind the user's back.
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, """{"Version":2,"HiddenChampionships":[]}""");
        var upgraded = store.Load();
        Equal(UserSettingsStore.CurrentVersion, upgraded.Version);
        False(upgraded.IsChampionshipVisible(Championship.NascarCup));
        False(upgraded.EnableNascarCup);
        False(upgraded.IsChampionshipVisible(Championship.NascarXfinity));
        False(upgraded.IsChampionshipVisible(Championship.NascarTrucks));
        False(upgraded.IsChampionshipVisible(Championship.F1Academy));

        // Once the user shows and enables it, the choice survives a reload.
        store.Save(new UserSettings { EnableNascarCup = true, HiddenChampionships = [Championship.Dtm] });
        var chosen = store.Load();
        True(chosen.IsChampionshipVisible(Championship.NascarCup));
        True(chosen.EnableNascarCup);
        False(chosen.IsChampionshipVisible(Championship.Dtm));
        Equal(UserSettingsStore.CurrentVersion, chosen.Version);
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

static void NascarCupSourceLifecycle()
{
    var now = new DateTimeOffset(2026, 10, 22, 18, 33, 0, TimeSpan.Zero);
    var clock = new FakeClock(now);
    var delay = new BlockingDelay();
    var alerts = new AlertArbiter(clock);
    var http = new NascarFakeHttp("live/feeds/live-feed.json", NascarFixture("cup-race.json").ToJsonString());
    var schedule = new FakeNascarScheduleClient(NascarRaceList(now.AddMinutes(-10)));
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    var rig = AttachNascarSource(feed, clock, alerts: alerts);
    var deltas = 0;
    rig.Source.DeltaReceived += _ => deltas++;

    rig.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    // A second start is a no-op, so the shared feed is requested once per interval.
    rig.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    rig.Source.StopAsync().GetAwaiter().GetResult();
    rig.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();

    Equal(SessionLifecycle.Live, rig.Snapshots.Last().Lifecycle);
    Equal(Championship.NascarCup, rig.Snapshots.Last().Championship);
    Equal(TimeSpan.FromSeconds(2), delay.Duration);
    Equal(1, http.LiveCalls);
    Equal(1, deltas);
    Equal(AlertKind.SessionStart, alerts.Current?.Kind);
    Equal(0, rig.Failures.Count);
    True(rig.Connections.Contains(ConnectionState.Connected));
    Equal(ConnectionState.Disconnected, rig.Connections[^1]);
}

/// <summary>A season shaped like the 2026 list: repeated names, Duels without a schedule, Eastern dates.</summary>
static JsonArray NascarRepeatedNameRaceList() => JsonNode.Parse("""
    [ { "race_id": 5594, "race_name": "America 250 Florida Duel at DAYTONA", "track_name": "Daytona International Speedway",
        "date_scheduled": "2026-02-12T19:00:00", "race_date": "2026-02-12T19:00:00" },
      { "race_id": 5595, "race_name": "America 250 Florida Duel at DAYTONA", "track_name": "Daytona International Speedway",
        "date_scheduled": "2026-02-12T20:45:00", "race_date": "2026-02-12T20:45:00" },
      { "race_id": 5610, "race_name": "Cook Out 400", "track_name": "Richmond Raceway",
        "date_scheduled": "2026-04-12T15:00:00",
        "schedule": [ { "run_type": 3, "start_time_utc": "2026-04-12T19:00:00" } ] },
      { "race_id": 5625, "race_name": "Cook Out 400", "track_name": "Richmond Raceway",
        "date_scheduled": "2026-08-15T20:00:00",
        "schedule": [ { "run_type": 3, "start_time_utc": "2026-08-16T00:00:00" } ] },
      { "race_id": 5631, "race_name": "YellaWood 500", "track_name": "Talladega Superspeedway",
        "date_scheduled": "2026-10-25T14:00:00", "race_date": "2026-10-25T14:00:00" } ]
    """)!.AsArray();

static void NascarRaceListTimesAndIdentity()
{
    var races = NascarRepeatedNameRaceList();

    // The Duels publish no schedule[]; their Eastern times are converted, and 19:00 EST is midnight UTC, which
    // is a real start time rather than a date-only round.
    var firstDuel = ChampionshipCalendars.NextNascarCup(races, new DateTimeOffset(2026, 2, 12, 12, 0, 0, TimeSpan.Zero))!;
    Equal(new DateTimeOffset(2026, 2, 13, 0, 0, 0, TimeSpan.Zero), firstDuel.StartUtc);
    True(firstDuel.HasStartTime);
    Equal<int?>(5594, ChampionshipCalendars.NextNascarRaceId(races,
        new DateTimeOffset(2026, 2, 12, 12, 0, 0, TimeSpan.Zero), Championship.NascarCup));

    // Once the first Duel has started, the second one with the same name is selected by its own id.
    var afterFirst = new DateTimeOffset(2026, 2, 13, 0, 30, 0, TimeSpan.Zero);
    var secondDuel = ChampionshipCalendars.NextNascarCup(races, afterFirst)!;
    Equal("America 250 Florida Duel at DAYTONA", secondDuel.EventName);
    Equal(new DateTimeOffset(2026, 2, 13, 1, 45, 0, TimeSpan.Zero), secondDuel.StartUtc);
    Equal<int?>(5595, ChampionshipCalendars.NextNascarRaceId(races, afterFirst, Championship.NascarCup));

    // The second "Cook Out 400" is selected by id, and its UTC schedule start at midnight stays a clock time.
    var summer = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    var secondCookOut = ChampionshipCalendars.NextNascarCup(races, summer)!;
    Equal(new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero), secondCookOut.StartUtc);
    True(secondCookOut.HasStartTime);
    Equal<int?>(5625, ChampionshipCalendars.NextNascarRaceId(races, summer, Championship.NascarCup));

    // Daylight time: 14:00 EDT is 18:00 UTC, matching the provider's own schedule[] start for this race.
    var talladega = ChampionshipCalendars.NextNascarCup(races, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero))!;
    Equal(new DateTimeOffset(2026, 10, 25, 18, 0, 0, TimeSpan.Zero), talladega.StartUtc);

    // A clock time skipped by the spring-forward change resolves to the hour after it.
    Equal(new DateTimeOffset(2026, 3, 8, 7, 30, 0, TimeSpan.Zero),
        ChampionshipCalendars.EasternToUtc(new DateTime(2026, 3, 8, 2, 30, 0)));
    Equal<int?>(null, ChampionshipCalendars.NextNascarRaceId(null, summer, Championship.NascarCup));
}

static JsonObject NascarWeekendFeed(string practiceName, DateTimeOffset practiceStart) => JsonNode.Parse($$"""
    { "weekend_race": [ { "schedule": [
        { "event_name": "{{practiceName}}", "run_type": 1,
          "start_time_utc": "{{practiceStart.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss")}}" } ] } ] }
    """)!.AsObject();

static void NascarCalendarWeekendByRaceId()
{
    var schedule = new FakeNascarScheduleClient(NascarRepeatedNameRaceList());
    schedule.Weekends[5610] = NascarWeekendFeed("Spring Practice", new DateTimeOffset(2026, 4, 11, 17, 0, 0, TimeSpan.Zero));
    schedule.Weekends[5625] = NascarWeekendFeed("Summer Practice", new DateTimeOffset(2026, 8, 15, 17, 0, 0, TimeSpan.Zero));
    var calendar = new NascarCalendar(schedule, NascarSeries.Cup);

    var sessions = calendar.GetEventScheduleAsync(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
        CancellationToken.None).GetAwaiter().GetResult();

    // The spring round shares the name, but the summer round's own weekend feed is the one read.
    Equal((2026, 1, (int?)5625), schedule.WeekendRequests.Single());
    Equal("Summer Practice", sessions.Single().SessionName);
    Equal("Cook Out 400", sessions.Single().EventName);
}

static void NascarFeedWeekendByRaceId()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    var delay = new HeldDelay();
    var http = new NascarFakeHttp("live/feeds/live-feed.json", "{}");
    var schedule = new FakeNascarScheduleClient(NascarRepeatedNameRaceList());
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    var cup = AttachNascarSource(feed, clock);
    cup.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(1, TimeSpan.FromSeconds(2)));

    Equal((2026, 1, (int?)5625), schedule.WeekendRequests.Single());
    Equal(0, http.LiveCalls);
    cup.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void NascarPreSessionWaitsForGreen()
{
    var now = new DateTimeOffset(2026, 10, 22, 18, 33, 0, TimeSpan.Zero);
    var clock = new FakeClock(now);
    var delay = new HeldDelay();
    var alerts = new AlertArbiter(clock);
    var gridded = NascarFixture("cup-race.json");
    gridded["flag_state"] = 8;
    gridded["lap_number"] = 0;
    var http = new NascarFakeHttp("live/feeds/live-feed.json", gridded.ToJsonString());
    var schedule = new FakeNascarScheduleClient(NascarRaceList(now.AddMinutes(20)));
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    var rig = AttachNascarSource(feed, clock, alerts: alerts);
    rig.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(1, TimeSpan.FromSeconds(2)));

    // The listed field before green is pre-session: no start alert and nothing for the composite to show.
    Equal(1, http.LiveCalls);
    Equal(SessionLifecycle.PreSession, rig.Snapshots.Last().Lifecycle);
    Equal(null, alerts.Current);

    // Green starts the session and announces it once.
    http.Body = NascarFixture("cup-race.json").ToJsonString();
    delay.Release();
    True(delay.WaitUntilCalled(2, TimeSpan.FromSeconds(2)));
    Equal(SessionLifecycle.Live, rig.Snapshots.Last().Lifecycle);
    Equal(AlertKind.SessionStart, alerts.Current?.Kind);
    rig.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

/// <summary>
/// Enabling a series while the feed sleeps toward another series' distant race must not wait out that sleep.
/// </summary>
static void NascarFeedWakesForNewSeries()
{
    var now = new DateTimeOffset(2026, 10, 22, 18, 33, 0, TimeSpan.Zero);
    var clock = new FakeClock(now);
    var delay = new HeldDelay();
    var http = new NascarFakeHttp("live/feeds/live-feed.json", NascarFixture("cup-race.json").ToJsonString());
    var schedule = new FakeNascarScheduleClient(null);
    schedule.BySeries[NascarSeries.Trucks.SeriesId] = NascarRaceList(now.AddMinutes(10));
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    var cup = AttachNascarSource(feed, clock);
    cup.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(1, TimeSpan.FromSeconds(2)));
    // Cup has no race ahead, so the feed parks for a full schedule refresh.
    Equal(TestNascarFeedOptions().ScheduleRefreshInterval, delay.Durations[0]);
    Equal(0, http.LiveCalls);

    // Trucks is enabled with a race ten minutes out: the sleep is cut short without being released.
    var trucks = AttachNascarSource(feed, clock, NascarSeries.Trucks);
    trucks.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(2, TimeSpan.FromSeconds(2)));
    Equal(1, http.LiveCalls);
    Equal(TestNascarFeedOptions().PollInterval, delay.Durations[1]);

    // Trucks is disabled again: the feed returns to the long Cup sleep.
    trucks.Source.StopAsync().GetAwaiter().GetResult();
    delay.Release();
    True(delay.WaitUntilCalled(3, TimeSpan.FromSeconds(2)));
    Equal(TestNascarFeedOptions().ScheduleRefreshInterval, delay.Durations[2]);

    // A settings change restarts every source; the restarted Trucks source interrupts that sleep too.
    cup.Source.StopAsync().GetAwaiter().GetResult();
    trucks.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    cup.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(4, TimeSpan.FromSeconds(2)));
    Equal(2, http.LiveCalls);
    Equal(TestNascarFeedOptions().PollInterval, delay.Durations[3]);
    trucks.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    cup.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

/// <summary>
/// Payloads at each state change of the 24 Oct recording (vehicles trimmed to the leaders plus a lapped and a DVP
/// car): the Xfinity race from the pre-race grid to the stop flag, and Cup pole qualifying across both rounds.
/// </summary>
static IReadOnlyList<(DateTimeOffset Captured, JsonObject Payload)> NascarRecordedSteps(string session) =>
    NascarFixture("recorded-transitions.json")[session]!.AsArray()
        .Select(entry => (DateTimeOffset.Parse(entry!["captured"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture),
            entry["payload"]!.AsObject()))
        .ToList();

static void NascarRecordedTransitions()
{
    var race = NascarRecordedSteps("race")
        .Select(step => NascarTiming.BuildSnapshot(step.Payload, step.Captured, ConnectionState.Connected, NascarSeries.Xfinity))
        .ToList();
    Equal(13, race.Count);
    Equal(Championship.NascarXfinity, race[0].Championship);
    Equal("Race", race[0].Session);
    Equal(94, race[0].TotalLaps);
    // The grid an hour before green, every green and caution period, the final lap, then the stop flag.
    Equal("PreSession,Live,Live,Live,Live,Live,Live,Live,Live,Live,Live,Ended,Ended",
        string.Join(",", race.Select(snapshot => snapshot.Lifecycle)));
    Equal("Unknown,AllClear,FullCourseYellow,AllClear,FullCourseYellow,AllClear,FullCourseYellow,AllClear,FullCourseYellow,AllClear,AllClear,AllClear,Unknown",
        string.Join(",", race.Select(snapshot => snapshot.TrackCondition)));
    // Stage 1 ends at lap 25 and stage 2 at lap 50; the provider moves the stage on with the caution.
    Equal("1,1,2,2,3,3,3,3,3,3,3,3,3", string.Join(",", race.Select(snapshot => snapshot.StageNumber)));
    Equal(94, race[^1].StageFinishLap);
    Equal("RAC 94/94 · S3", TaskbarSessionDisplay.Progress(race[^2]));

    // Lapped cars publish laps down as a negative delta, which is not a time gap and is not shown as one.
    var lapped = race[2].Competitors.Single(competitor => competitor.Position == 38);
    Equal(null, lapped.GapToLeader);
    Equal("DVP", lapped.StatusLabel);
    Equal("LEAD", race[2].Competitors[0].GapToLeader);
    Equal("+0.055", race[2].Competitors[1].GapToLeader);
    Equal("+0.081", race[2].Competitors[2].IntervalToPositionAhead);
    True(race[2].Competitors.All(competitor => competitor.IsPlayoffEligible is not null));

    var qualifying = NascarRecordedSteps("qualifying")
        .Select(step => NascarTiming.BuildSnapshot(step.Payload, step.Captured, ConnectionState.Connected, NascarSeries.Cup))
        .ToList();
    // Round 1 stops, the final round waits on flag 8, runs, and stops.
    Equal("Live,Ended,PreSession,Live,Ended", string.Join(",", qualifying.Select(snapshot => snapshot.Lifecycle)));
    True(qualifying.All(snapshot => snapshot.Session == "Qualifying" && snapshot.Championship == Championship.NascarCup));
    // Qualifying carries a placeholder stage and distance, neither of which is reported.
    True(qualifying.All(snapshot => snapshot.StageNumber is null && snapshot.TotalLaps is null));
    Equal("Q", TaskbarSessionDisplay.Progress(qualifying[0]));
}

static void NascarRecordedRaceAlerts()
{
    var steps = NascarRecordedSteps("race");
    var clock = new FakeClock(steps[0].Captured);
    var delay = new HeldDelay();
    var alerts = new RecordingAlerts();
    var http = new NascarFakeHttp("live/feeds/live-feed.json", steps[0].Payload.ToJsonString());
    var schedule = new FakeNascarScheduleClient(null);
    schedule.BySeries[NascarSeries.Xfinity.SeriesId] = NascarRaceList(new DateTimeOffset(2026, 10, 24, 19, 30, 0, TimeSpan.Zero));
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    var source = new NascarLiveTimingSource(feed, NascarSeries.Xfinity, clock, alerts);
    List<TimingSnapshot> snapshots = [];
    var deltas = 0;
    source.SnapshotReceived += snapshots.Add;
    source.DeltaReceived += _ => deltas++;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(1, TimeSpan.FromSeconds(2)));
    for (var index = 1; index < steps.Count; index++)
    {
        clock.Advance(steps[index].Captured - clock.UtcNow);
        http.Body = steps[index].Payload.ToJsonString();
        delay.Release();
        True(delay.WaitUntilCalled(index + 1, TimeSpan.FromSeconds(2)));
    }
    // The provider re-serves the same assembly until it builds a new one: that repeat is not a new delta.
    clock.Advance(TimeSpan.FromSeconds(10));
    delay.Release();
    True(delay.WaitUntilCalled(steps.Count + 1, TimeSpan.FromSeconds(2)));
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();

    Equal(steps.Count + 1, http.LiveCalls);
    Equal(steps.Count, deltas);
    Equal(SessionLifecycle.Ended, snapshots.Last(snapshot => snapshot.Competitors.Count > 0).Lifecycle);
    // The start is announced at green, not on the pre-race grid, and the finish exactly once.
    var start = alerts.Accepted.Single(alert => alert.Kind == AlertKind.SessionStart);
    Equal("NASCAR Xfinity SESSION STARTED", start.Title);
    Equal(0, start.Lap);
    var chequered = alerts.Accepted.Single(alert => alert.Kind == AlertKind.Chequered);
    Equal(94, chequered.Lap);
    // Four cautions and their restarts; the white flag and the stop flag claim no new track state.
    Equal(4, alerts.TrackConditions.Count(condition => condition == TrackCondition.FullCourseYellow));
    Equal(5, alerts.TrackConditions.Count(condition => condition == TrackCondition.AllClear));
}

static void NascarScheduleCache()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    var schedule = new FakeNascarScheduleClient(NascarRepeatedNameRaceList());
    schedule.Weekends[5625] = NascarWeekendFeed("Summer Practice", new DateTimeOffset(2026, 8, 15, 17, 0, 0, TimeSpan.Zero));
    schedule.BySeries[NascarSeries.Trucks.SeriesId] = null;
    using var cached = new CachedNascarScheduleClient(schedule, clock, TimeSpan.FromMinutes(30));
    var cup = new NascarCalendar(cached, NascarSeries.Cup);
    var now = clock.UtcNow;

    // The upcoming and event-schedule queries read the race list and the weekend feed once between them.
    Equal(1, cup.GetUpcomingAsync(now, CancellationToken.None).GetAwaiter().GetResult().Count);
    Equal("Summer Practice", cup.GetEventScheduleAsync(now, CancellationToken.None).GetAwaiter().GetResult().Single().SessionName);
    Equal(1, schedule.Calls);
    Equal(1, schedule.WeekendRequests.Count);

    // A season the provider has not released (no list for this year or next) is remembered too.
    var trucks = new NascarCalendar(cached, NascarSeries.Trucks);
    Equal(0, trucks.GetUpcomingAsync(now, CancellationToken.None).GetAwaiter().GetResult().Count);
    Equal(3, schedule.Calls);
    Equal(0, trucks.GetEventScheduleAsync(now, CancellationToken.None).GetAwaiter().GetResult().Count);
    Equal(3, schedule.Calls);

    // Once the lifetime passes, the next refresh asks again.
    clock.Advance(TimeSpan.FromMinutes(31));
    cup.GetUpcomingAsync(clock.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
    Equal(4, schedule.Calls);
    Equal(2, schedule.WeekendRequests.Count);

    // A failed read is not kept: the caller sees the failure and the next read retries the provider.
    clock.Advance(TimeSpan.FromMinutes(31));
    schedule.FailRaceLists = 1;
    var failed = false;
    try { cup.GetUpcomingAsync(clock.UtcNow, CancellationToken.None).GetAwaiter().GetResult(); }
    catch (HttpRequestException) { failed = true; }
    True(failed);
    Equal(1, cup.GetUpcomingAsync(clock.UtcNow, CancellationToken.None).GetAwaiter().GetResult().Count);
    Equal(6, schedule.Calls);
    // The weekend feed read at the previous refresh has expired by now as well.
    Equal(3, schedule.WeekendRequests.Count);

    // A race without an id has no weekend feed to read.
    Equal(null, cached.GetWeekendFeedAsync(2026, 1, null, CancellationToken.None).GetAwaiter().GetResult());
    Equal(3, schedule.WeekendRequests.Count);
}

/// <summary>
/// Every published title the calendars can deliver, shortened the way the schedule and countdown draw it. The
/// fixture is the 2026 schedule of each series that publishes one, so a title that stops fitting fails here
/// instead of arriving in the app clipped. Series whose calendar is not published are covered by their own tests.
/// </summary>
static void ChampionshipEventNames()
{
    var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "EventNames", "published-2026.json");
    var entries = JsonNode.Parse(File.ReadAllText(fixture))!.AsArray();
    True(entries.Count > 170);
    foreach (var entry in entries.OfType<JsonObject>())
    {
        var series = entry["series"]!.GetValue<string>();
        var championship = Enum.Parse<Championship>(series);
        var raw = entry["event"]!.GetValue<string>();
        var shown = ChampionshipDisplay.ShortEventName(championship, raw);
        Equal(entry["shown"]!.GetValue<string>(), shown);
        if (shown.Length > 26) throw new Exception($"{series}: {raw} -> {shown} ({shown.Length} chars) does not fit the schedule.");
    }
    // A title no table covers is left as published rather than guessed at.
    Equal("Mystery 500 presented by Nobody",
        ChampionshipDisplay.ShortEventName(Championship.NascarCup, "Mystery 500 presented by Nobody"));
}

/// <summary>A cached read belongs to the cache: the first caller giving up must not cancel it for the next.</summary>
static void NascarScheduleCacheSurvivesCallerCancellation()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    var inner = new HeldNascarScheduleClient();
    using var cached = new CachedNascarScheduleClient(inner, clock, TimeSpan.FromMinutes(30));

    using var first = new CancellationTokenSource();
    var abandoned = cached.GetRaceListAsync(NascarSeries.Cup.SeriesId, 2026, first.Token);
    first.Cancel();
    var cancelled = false;
    try { abandoned.GetAwaiter().GetResult(); }
    catch (OperationCanceledException) { cancelled = true; }
    True(cancelled);

    // The shared read outlives that caller, so the next one waits for it instead of asking the provider again.
    var shared = cached.GetRaceListAsync(NascarSeries.Cup.SeriesId, 2026, CancellationToken.None);
    True(!shared.IsCompleted);
    inner.Release([]);
    Equal(0, shared.GetAwaiter().GetResult()!.Count);
    Equal(1, inner.Calls);
}

static void NascarSeriesIdentifiers()
{
    Equal(Championship.NascarCup, NascarSeries.Cup.Championship);
    Equal(1, NascarSeries.Cup.SeriesId);
    Equal("NASCAR Cup", NascarSeries.Cup.Label);
    Equal(Championship.NascarXfinity, NascarSeries.Xfinity.Championship);
    Equal(2, NascarSeries.Xfinity.SeriesId);
    Equal(Championship.NascarTrucks, NascarSeries.Trucks.Championship);
    Equal(3, NascarSeries.Trucks.SeriesId);
}

/// <summary>Two series on one feed: one request per tick serves both and each gets its own routing.</summary>
static void NascarFeedServesEverySubscriberFromOneRequest()
{
    var now = new DateTimeOffset(2026, 10, 22, 18, 33, 0, TimeSpan.Zero);
    var clock = new FakeClock(now);
    var delay = new HeldDelay();
    var http = new NascarFakeHttp("live/feeds/live-feed.json", NascarFixture("cup-race.json").ToJsonString());
    var schedule = new FakeNascarScheduleClient(NascarRaceList(now.AddMinutes(-10)));
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    var cup = AttachNascarSource(feed, clock);
    var xfinity = AttachNascarSource(feed, clock, new NascarSeries(Championship.Unknown, 2, "Xfinity"));

    cup.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(1, TimeSpan.FromSeconds(2)));
    xfinity.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    delay.Release();
    True(delay.WaitUntilCalled(2, TimeSpan.FromSeconds(2)));
    cup.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    xfinity.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();

    // Two ticks, two requests, and the season read once per series for the whole cache lifetime.
    Equal(2, http.LiveCalls);
    Equal(2, schedule.Calls);
    Equal(3, cup.Snapshots.Count + xfinity.Snapshots.Count);

    // The Cup payload belongs to Cup; the other series is handed a hidden session instead of that field.
    True(cup.Snapshots.All(snapshot => snapshot.Lifecycle == SessionLifecycle.Live));
    Equal(6, cup.Snapshots.Last().Competitors.Count);
    True(xfinity.Snapshots.All(snapshot => snapshot.Lifecycle == SessionLifecycle.OffSession));
    Equal(0, xfinity.Snapshots.Last().Competitors.Count);
    Equal(Championship.Unknown, xfinity.Snapshots.Last().Championship);
    Equal(ConnectionState.Connected, xfinity.Snapshots.Last().ConnectionState);
}

/// <summary>Five hours to the next race: nothing is requested and the feed sleeps to the lead.</summary>
static void NascarFeedSleepsToTheNextRaceLead()
{
    var now = new DateTimeOffset(2026, 10, 22, 18, 33, 0, TimeSpan.Zero);
    var clock = new FakeClock(now);
    var delay = new HeldDelay();
    var http = new NascarFakeHttp("live/feeds/live-feed.json", NascarFixture("cup-race.json").ToJsonString());
    var schedule = new FakeNascarScheduleClient(NascarRaceList(now.AddHours(5)));
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    var cup = AttachNascarSource(feed, clock);
    cup.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(1, TimeSpan.FromSeconds(2)));

    Equal(0, http.LiveCalls);
    Equal(TimeSpan.FromHours(4) + TimeSpan.FromMinutes(30), delay.Durations[0]);

    // The lead has come when that sleep ends, so the next tick polls.
    clock.Advance(TimeSpan.FromHours(4) + TimeSpan.FromMinutes(30));
    delay.Release();
    True(delay.WaitUntilCalled(2, TimeSpan.FromSeconds(2)));
    Equal(1, http.LiveCalls);
    Equal(TestNascarFeedOptions().PollInterval, delay.Durations[1]);
    Equal(TimeSpan.FromSeconds(2), delay.Durations[1]);
    cup.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void NascarFeedStopsRequestingWithoutSubscribers()
{
    var now = new DateTimeOffset(2026, 10, 22, 18, 33, 0, TimeSpan.Zero);
    var clock = new FakeClock(now);
    var delay = new HeldDelay();
    var http = new NascarFakeHttp("live/feeds/live-feed.json", NascarFixture("cup-race.json").ToJsonString());
    var schedule = new FakeNascarScheduleClient(NascarRaceList(now.AddMinutes(-10)));
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    var cup = AttachNascarSource(feed, clock);
    cup.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(1, TimeSpan.FromSeconds(2)));
    Equal(1, http.LiveCalls);

    // The last subscriber has gone: the feed parks, so neither the schedule nor the live feed is read.
    cup.Source.StopAsync().GetAwaiter().GetResult();
    delay.Release();
    Thread.Sleep(200);
    Equal(1, http.LiveCalls);
    Equal(1, schedule.Calls);

    cup.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(2, TimeSpan.FromSeconds(2)));
    Equal(2, http.LiveCalls);
    Equal(1, schedule.Calls);
    cup.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void NascarFeedIsolatesFailures()
{
    var now = new DateTimeOffset(2026, 10, 22, 18, 33, 0, TimeSpan.Zero);
    var clock = new FakeClock(now);
    var delay = new HeldDelay();
    var http = new NascarFakeHttp("live/feeds/live-feed.json", NascarFixture("cup-race.json").ToJsonString());
    var schedule = new FakeNascarScheduleClient(NascarRaceList(now.AddMinutes(-10)));
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    // A subscriber that throws must not stop the feed or its neighbours.
    var threw = 0;
    using var broken = feed.Subscribe(NascarSeries.Cup, (_, _) => { threw++; throw new InvalidOperationException("source"); });
    var cup = AttachNascarSource(feed, clock);
    cup.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(1, TimeSpan.FromSeconds(2)));
    Equal(1, threw);
    Equal(SessionLifecycle.Live, cup.Snapshots.Last().Lifecycle);
    Equal(0, cup.Failures.Count);

    // A broken request is a feed failure, not a crash: the widget hides and the feed backs off.
    http.FailLiveFeed = true;
    delay.Release();
    True(delay.WaitUntilCalled(2, TimeSpan.FromSeconds(2)));
    Equal(1, cup.Failures.Count);
    True(cup.Failures[0].Message.Contains("relay refused", StringComparison.Ordinal));
    Equal(ConnectionState.Faulted, cup.Snapshots.Last().ConnectionState);
    Equal(SessionLifecycle.OffSession, cup.Snapshots.Last().Lifecycle);
    Equal(TestNascarFeedOptions().FailureInterval, delay.Durations[1]);

    // The loop survived both the throwing subscriber and the failed request.
    http.FailLiveFeed = false;
    delay.Release();
    True(delay.WaitUntilCalled(3, TimeSpan.FromSeconds(2)));
    Equal(2, threw);
    Equal(SessionLifecycle.Live, cup.Snapshots.Last().Lifecycle);
    Equal(1, cup.Failures.Count);
    cup.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();
}

static void NascarFeedPollsDateOnlyRacesAllDay()
{
    // A round published without a clock time names a day, so the feed keeps polling through it instead of
    // stopping four hours after midnight UTC whatever the race time turns out to be.
    var clock = new FakeClock(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));
    var delay = new BlockingDelay();
    var http = new NascarFakeHttp("live/feeds/live-feed.json", NascarFixture("cup-race.json").ToJsonString());
    var schedule = new FakeNascarScheduleClient(JsonNode.Parse("""
        [ { "race_name": "Pennzoil 400", "track_name": "Las Vegas Motor Speedway",
            "date_scheduled": "1900-01-01T00:00:00", "race_date": "2026-10-22T00:00:00" } ]
        """)!.AsArray());
    using var httpClient = NascarFeedHttpClient(http);
    using var feed = new NascarFeed(clock, TestNascarFeedOptions(), delay, schedule, httpClient);
    var rig = AttachNascarSource(feed, clock);
    rig.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    rig.Source.StopAsync().GetAwaiter().GetResult();
    rig.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();

    // Midday on the published day is inside the window, so the live feed was actually requested.
    Equal(1, http.LiveCalls);
    Equal(TestNascarFeedOptions().PollInterval, delay.Duration);

    // The morning before it only waits for the lead.
    var before = new FakeClock(new DateTimeOffset(2026, 10, 21, 23, 0, 0, TimeSpan.Zero));
    var waiting = new BlockingDelay();
    var idleHttp = new NascarFakeHttp("live/feeds/live-feed.json", "{}");
    using var idleClient = NascarFeedHttpClient(idleHttp);
    using var idleFeed = new NascarFeed(before, TestNascarFeedOptions(), waiting, new FakeNascarScheduleClient(JsonNode.Parse("""
        [ { "race_name": "Pennzoil 400", "track_name": "Las Vegas Motor Speedway",
            "date_scheduled": "1900-01-01T00:00:00", "race_date": "2026-10-22T00:00:00" } ]
        """)!.AsArray()), idleClient);
    var idleRig = AttachNascarSource(idleFeed, before);
    idleRig.Source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(waiting.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    idleRig.Source.StopAsync().GetAwaiter().GetResult();
    idleRig.Source.DisposeAsync().AsTask().GetAwaiter().GetResult();

    Equal(0, idleHttp.LiveCalls);
    Equal(TimeSpan.FromMinutes(30), waiting.Duration);
}

static void NascarFeedIdleDelay()
{
    var now = new DateTimeOffset(2026, 10, 22, 18, 33, 0, TimeSpan.Zero);
    var options = TestNascarFeedOptions();
    // Inside a window, and from its lead on, the poll interval applies.
    Equal(options.PollInterval, NascarFeed.NextIdleDelay([(now.AddMinutes(-10), now.AddHours(3))], now, options));
    Equal(options.PollInterval, NascarFeed.NextIdleDelay([(now.AddMinutes(20), now.AddHours(4))], now, options));
    // Far from it: sleep to the lead, capped at the schedule refresh interval.
    Equal(TimeSpan.FromHours(1), NascarFeed.NextIdleDelay([(now.AddHours(1).AddMinutes(30), now.AddHours(5))], now, options));
    Equal(options.ScheduleRefreshInterval, NascarFeed.NextIdleDelay([(now.AddDays(2), now.AddDays(2))], now, options));
    // Nothing published, or a window already over: refresh the season instead of spinning.
    Equal(options.ScheduleRefreshInterval, NascarFeed.NextIdleDelay([], now, options));
    Equal(options.ScheduleRefreshInterval, NascarFeed.NextIdleDelay([(now.AddHours(-6), now.AddHours(-2))], now, options));
}

static void NascarCupSourceHiddenAndStaleFeeds()
{
    var now = new DateTimeOffset(2026, 10, 22, 18, 33, 0, TimeSpan.Zero);

    // Between sessions the feed answers with an empty field: the widget stays hidden.
    var empty = NascarFixture("cup-race.json");
    empty["vehicles"] = new JsonArray();
    var idle = RunNascarTick(empty, new FakeClock(now));
    Equal(SessionLifecycle.OffSession, idle.Snapshots.Last().Lifecycle);
    Equal("", idle.Snapshots.Last().Meeting);
    Equal(0, idle.Snapshots.Last().Competitors.Count);
    Equal(Championship.NascarCup, idle.Snapshots.Last().Championship);
    True(idle.Connections.Contains(ConnectionState.Connected));
    Equal(0, idle.Failures.Count);

    // After a session the endpoint keeps serving its last assembled field; only the assembly time
    // proves whether it is still running, so an old one never presents as live.
    var stale = RunNascarTick(NascarFixture("cup-race.json"), new FakeClock(now.AddMinutes(8)));
    True(stale.Snapshots.All(snapshot => snapshot.Lifecycle != SessionLifecycle.Live));
    Equal(SessionLifecycle.OffSession, stale.Snapshots.Last().Lifecycle);
    Equal(ConnectionState.Stale, stale.Snapshots.Last().ConnectionState);
    True(stale.Connections.Contains(ConnectionState.Stale));
    Equal(0, stale.Failures.Count);

    // A broken transcript is a feed failure, not a crash: the widget hides and the feed backs off.
    var faulted = RunNascarTick(NascarFixture("cup-race.json"), new FakeClock(now), failLiveFeed: true);
    Equal(1, faulted.Failures.Count);
    Equal(ConnectionState.Faulted, faulted.Snapshots.Last().ConnectionState);
    Equal(SessionLifecycle.OffSession, faulted.Snapshots.Last().Lifecycle);
    Equal(TestNascarFeedOptions().FailureInterval, faulted.Delay);
}

static JsonObject IndyCarPracticePayload() => JsonNode.Parse("""
    {"timing_results":{"heartbeat":{
      "eventName":"Ontario Honda Dealers Indy at Markham","trackName":"Streets of Markham","currentFlag":"GREEN",
      "overallTimeToGo":"21:52","Series":"I","SessionName":"Practice 1 - All Cars","SessionType":"P",
      "SessionStatus":"GREEN","EventID":"5520","EventSessionID":"6911","EventSessionLabel":"Practice 1"},
     "Item":[
      {"DriverID":954,"rank":1,"diff":"0.0000","gap":"0.0000","bestLapTime":"1:15.6757","laps":"10","status":"Active","marker":"InPit","no":"27","firstName":"Kyle","lastName":"Kirkwood","equipment":"D/H/F","team":"Andretti Global","Tire":"P"},
      {"DriverID":271,"rank":2,"diff":"0.3455","gap":"0.3455","bestLapTime":"1:16.0212","laps":"10","status":"Active","marker":"None","no":"28","firstName":"Marcus","lastName":"Ericsson","equipment":"D/H/F","team":"Andretti Global","Tire":"P"},
      {"DriverID":999,"rank":3,"diff":"0.5461","gap":"0.2006","bestLapTime":"1:16.2218","laps":"8","status":"Active","marker":"None","no":"19","firstName":"Dennis","lastName":"Hauger","equipment":"D/C/F","team":"Dale Coyne Racing","Tire":"P"}
     ]}}
    """)!.AsObject();

static JsonObject ImsaSessionReference(string value) => new() { ["$type"] = "oid", ["$value"] = value };

static JsonObject DdpAdded(string collection, string id, JsonObject fields) => new()
{
    ["msg"] = "added",
    ["collection"] = collection,
    ["id"] = id,
    ["fields"] = fields
};

static JsonObject DdpChanged(string collection, string id, JsonObject fields) => new()
{
    ["msg"] = "changed",
    ["collection"] = collection,
    ["id"] = id,
    ["fields"] = fields
};

static string GtwcEnvelope(string fullJson) => new JsonObject
{
    ["uid"] = 1,
    ["hash"] = "fixture",
    ["content"] = new JsonObject { ["full"] = JsonNode.Parse(fullJson) }
}.ToJsonString();

static void WrcSourceIdleLifecycle()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 7, 29, 20, 0, 0, TimeSpan.Zero));
    using var handler = new RouteHttpHandler(new Dictionary<string, Queue<HttpResponseMessage>>
    {
        ["seasons.json"] = Responses(FixtureText("seasons.json")),
        ["season-detail.json?seasonId=47"] = Responses(FixtureText("season-detail.json"))
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var delay = new BlockingDelay();
    var source = new WrcLiveTimingSource(api, clock, delay, new AlertArbiter(clock), TestWrcOptions());
    List<TimingSnapshot> snapshots = [];
    List<ConnectionState> connections = [];
    source.SnapshotReceived += snapshots.Add;
    source.ConnectionChanged += connections.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    Equal(TimeSpan.FromHours(1), delay.Duration);
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    True(snapshots.Count > 0);
    Equal(SessionLifecycle.OffSession, snapshots[^1].Lifecycle);
    Equal(ConnectionState.Disconnected, snapshots[^1].ConnectionState);
    Equal(ConnectionState.Disconnected, connections[^1]);
    Equal(1, handler.Paths.Count(path => path == "seasons.json"));
    False(handler.Paths.Any(path => path.StartsWith("events/", StringComparison.Ordinal)));
}

static void WrcSourceStageSchedule()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 7, 30, 15, 55, 0, TimeSpan.Zero));
    using var handler = new RouteHttpHandler(new Dictionary<string, Queue<HttpResponseMessage>>
    {
        ["seasons.json"] = Responses(FixtureText("seasons.json")),
        ["season-detail.json?seasonId=47"] = Responses(FixtureText("season-detail.json")),
        ["events/644.json"] = Responses(FixtureText("event-finland.json")),
        ["events/644/rallies/712/entries.json"] = Responses(FixtureText("entries-finland.json")),
        ["events/644/stages.json"] = Responses(FixtureText("stages-pre-event.json"))
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var delay = new BlockingDelay();
    var source = new WrcLiveTimingSource(api, clock, delay, new AlertArbiter(clock), TestWrcOptions());
    List<TimingSnapshot> snapshots = [];
    source.SnapshotReceived += snapshots.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    Equal(TimeSpan.FromMinutes(5), delay.Duration);
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    True(snapshots.All(snapshot => snapshot.Lifecycle == SessionLifecycle.OffSession));
    True(handler.Paths.Contains("events/644/stages.json"));
}

static void WrcSourceFailureLifecycle()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 7, 30, 17, 0, 0, TimeSpan.Zero));
    using var handler = new RouteHttpHandler(new Dictionary<string, Queue<HttpResponseMessage>>
    {
        ["seasons.json"] = Responses(FixtureText("seasons.json")),
        ["season-detail.json?seasonId=47"] = Responses(FixtureText("season-detail.json"))
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var delay = new BlockingDelay();
    var source = new WrcLiveTimingSource(api, clock, delay, new AlertArbiter(clock), TestWrcOptions());
    List<TimingSnapshot> snapshots = [];
    List<ConnectionState> connections = [];
    List<FeedFailure> failures = [];
    source.SnapshotReceived += snapshots.Add;
    source.ConnectionChanged += connections.Add;
    source.Failed += failures.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    Equal(1, failures.Count);
    True(failures[0].Message.Contains("discovery failed", StringComparison.OrdinalIgnoreCase));
    True(snapshots.Any(snapshot => snapshot.ConnectionState == ConnectionState.Faulted));
    True(connections.Contains(ConnectionState.Faulted));
}

static void WrcSourceStageLifecycle()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 7, 30, 17, 0, 0, TimeSpan.Zero));
    const string runningStage = """
        [{"stageId":11307,"eventId":644,"number":1,"code":"SS1","name":"Harju 1","status":"Running",
          "controls":[{"type":"StageStart","firstCarDueDateTime":"2026-07-30T16:05:00"}]}]
        """;
    const string stageTimes = """
        [{"entryId":62978,"position":1,"status":"Completed","elapsedDurationMs":120000,"diffFirstMs":0,"diffPrevMs":0},
         {"entryId":62979,"status":"Running"}]
        """;
    using var handler = new RouteHttpHandler(new Dictionary<string, Queue<HttpResponseMessage>>
    {
        ["seasons.json"] = Responses(FixtureText("seasons.json")),
        ["season-detail.json?seasonId=47"] = Responses(FixtureText("season-detail.json")),
        ["events/644.json"] = Responses(FixtureText("event-finland.json")),
        ["events/644/rallies/712/entries.json"] = Responses(FixtureText("entries-finland.json")),
        ["events/644/stages.json"] = Responses(runningStage),
        ["events/644/stages/11307/stagetimes.json?rallyId=712"] = Responses(stageTimes),
        ["events/644/stages/11307/results.json?rallyId=712"] = Responses("[]")
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var delay = new BlockingDelay();
    var alerts = new AlertArbiter(clock);
    var options = TestWrcOptions() with { PollInterval = TimeSpan.FromSeconds(15) };
    var source = new WrcLiveTimingSource(api, clock, delay, alerts, options);
    List<TimingSnapshot> snapshots = [];
    source.SnapshotReceived += snapshots.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitUntilCalled(TimeSpan.FromSeconds(2)));
    Equal(TimeSpan.FromSeconds(15), delay.Duration);
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    var live = snapshots.Single(snapshot => snapshot.Lifecycle == SessionLifecycle.Live);
    Equal("Secto Rally Finland", live.Meeting);
    Equal("SS1  Harju 1", live.Session);
    Equal("AAA", live.Competitors[0].Code);
    Equal(Championship.WorldRallyChampionship, live.Championship);
    Equal("wrc:event:644:stage:11307:start", alerts.Current?.DeduplicationKey);
    True(handler.Paths.Contains("events/644/stages/11307/stagetimes.json?rallyId=712"));
}

static void EndedFeedsClearSelection()
{
    var clock = new FakeClock();
    var source = new CompositeLiveTimingSource([
        () => new FakeTimingSource(Snapshot("F1", SessionLifecycle.Ended)),
        () => new FakeTimingSource(Snapshot("WRC", SessionLifecycle.Ended))
    ], clock, TimeSpan.FromMilliseconds(25));
    List<TimingSnapshot> snapshots = [];
    source.SnapshotReceived += snapshots.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    True(snapshots.Count > 0);
    Equal(SessionLifecycle.OffSession, snapshots[^1].Lifecycle);
}

static TimingSnapshot Snapshot(string meeting, SessionLifecycle lifecycle = SessionLifecycle.Live) => new(meeting, "Live", "Test", 0, null, TrackCondition.AllClear,
    [new CompetitorStanding("1", 1, "AAA", "Driver", "Team", "LEAD", null, null, 0, false, false, false, false)],
    DateTimeOffset.UtcNow, lifecycle);

static TimingSnapshot FeedSnapshot(string meeting, Championship championship, SessionLifecycle lifecycle = SessionLifecycle.Live) =>
    Snapshot(meeting, lifecycle) with { Championship = championship };

static void WrcRunningStageHidesWhenWrc1Done()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 7, 31, 7, 30, 0, TimeSpan.Zero));
    var alerts = new AlertArbiter(clock);
    var delay = new StepDelay(5);
    // The provider keeps reporting the stage as Running (support/national classes still on the road) while
    // the WRC1 field has finished. The stage must settle and hide once WRC1 is complete and stable.
    var stageRunning = """
    [{"stageId": 11307, "eventId": 644, "number": 1, "code": "SS1", "name": "Harju 1", "status": "Running",
      "controls": [{"type": "StageStart", "firstCarDueDateTime": "2026-07-31T07:05:00Z"}]}]
    """;
    var timesSettled = """
    [{"entryId": 62978, "position": 1, "elapsedDurationMs": 125400, "diffFirstMs": 0, "diffPrevMs": 0, "status": "Completed"},
     {"entryId": 62979, "position": 2, "elapsedDurationMs": 126200, "diffFirstMs": 800, "diffPrevMs": 800, "status": "Completed"}]
    """;
    var resultsSettled = """
    [{"entryId": 62978, "position": 1, "totalTimeMs": 125400},
     {"entryId": 62979, "position": 2, "totalTimeMs": 126200}]
    """;
    using var handler = new SequenceHttpHandler(new()
    {
        ["seasons.json"] = [FixtureText("seasons.json")],
        ["season-detail.json?seasonId=47"] = [FixtureText("season-detail.json")],
        ["events/644.json"] = [FixtureText("event-finland.json")],
        ["events/644/rallies/712/entries.json"] = [FixtureText("entries-finland.json")],
        ["events/644/stages.json"] = [stageRunning, stageRunning, stageRunning],
        ["events/644/stages/11307/stagetimes.json?rallyId=712"] = [timesSettled, timesSettled, timesSettled],
        ["events/644/stages/11307/results.json?rallyId=712"] = [resultsSettled, resultsSettled, resultsSettled]
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var options = TestWrcOptions() with { ResultStablePolls = 2 };
    var source = new WrcLiveTimingSource(api, clock, delay, alerts, options);
    List<TimingSnapshot> snapshots = [];
    source.SnapshotReceived += snapshots.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitForCalls(3, TimeSpan.FromSeconds(2)));
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    // Live while the WRC1 rows are stabilising, then hidden once they settle even though the stage still runs.
    True(snapshots.Any(s => s.Lifecycle == SessionLifecycle.Live && s.Session == "SS1  Harju 1"));
    var settled = snapshots.FindLast(s => s.ConnectionState == ConnectionState.Connected);
    True(settled is not null && settled.Lifecycle != SessionLifecycle.Live);
}

static void WrcRetiredWrc1CrewSettlesStage()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 7, 31, 7, 30, 0, TimeSpan.Zero));
    var alerts = new AlertArbiter(clock);
    var delay = new StepDelay(5);
    // A WRC1 crew retires on the road (terminal but never classified in results). The field is still
    // "complete" for settlement — the stage must hide once the remaining rows stabilise, not hang Live.
    var stageRunning = """
    [{"stageId": 11307, "eventId": 644, "number": 1, "code": "SS1", "name": "Harju 1", "status": "Running",
      "controls": [{"type": "StageStart", "firstCarDueDateTime": "2026-07-31T07:05:00Z"}]}]
    """;
    var timesSettled = """
    [{"entryId": 62978, "position": 1, "elapsedDurationMs": 125400, "diffFirstMs": 0, "diffPrevMs": 0, "status": "Completed"},
     {"entryId": 62979, "position": null, "elapsedDurationMs": null, "diffFirstMs": null, "diffPrevMs": null, "status": "Retired"}]
    """;
    var resultsSettled = """
    [{"entryId": 62978, "position": 1, "totalTimeMs": 125400}]
    """;
    using var handler = new SequenceHttpHandler(new()
    {
        ["seasons.json"] = [FixtureText("seasons.json")],
        ["season-detail.json?seasonId=47"] = [FixtureText("season-detail.json")],
        ["events/644.json"] = [FixtureText("event-finland.json")],
        ["events/644/rallies/712/entries.json"] = [FixtureText("entries-finland.json")],
        ["events/644/stages.json"] = [stageRunning, stageRunning, stageRunning],
        ["events/644/stages/11307/stagetimes.json?rallyId=712"] = [timesSettled, timesSettled, timesSettled],
        ["events/644/stages/11307/results.json?rallyId=712"] = [resultsSettled, resultsSettled, resultsSettled]
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var options = TestWrcOptions() with { ResultStablePolls = 2 };
    var source = new WrcLiveTimingSource(api, clock, delay, alerts, options);
    List<TimingSnapshot> snapshots = [];
    source.SnapshotReceived += snapshots.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitForCalls(3, TimeSpan.FromSeconds(2)));
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    // A retired crew never classifies, but the WRC1 field is still complete, so the stage settles and hides.
    True(snapshots.Any(s => s.Lifecycle == SessionLifecycle.Live && s.Session == "SS1  Harju 1"));
    var settled = snapshots.FindLast(s => s.ConnectionState == ConnectionState.Connected);
    True(settled is not null && settled.Lifecycle != SessionLifecycle.Live);
}

static void WrcCompletedToRunningReopens()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 7, 31, 7, 30, 0, TimeSpan.Zero));
    var alerts = new AlertArbiter(clock);
    var delay = new StepDelay(3);
    var stageCompleted = """
    [{"stageId": 11307, "eventId": 644, "number": 1, "code": "SS1", "name": "Harju 1", "status": "Completed",
      "controls": [{"type": "StageStart", "firstCarDueDateTime": "2026-07-31T07:05:00Z"}]}]
    """;
    var stageRunning = """
    [{"stageId": 11307, "eventId": 644, "number": 1, "code": "SS1", "name": "Harju 1", "status": "Running",
      "controls": [{"type": "StageStart", "firstCarDueDateTime": "2026-07-31T07:05:00Z"}]}]
    """;
    // Both WRC1 entries terminal and classified so the completed polls settle and blacklist the stage.
    var timesComplete = """
    [{"entryId": 62978, "position": 1, "elapsedDurationMs": 125400, "diffFirstMs": 0, "status": "Completed"},
     {"entryId": 62979, "position": 2, "elapsedDurationMs": 126200, "diffFirstMs": 800, "status": "Completed"}]
    """;
    var resultsComplete = """
    [{"entryId": 62978, "position": 1, "totalTimeMs": 125400},
     {"entryId": 62979, "position": 2, "totalTimeMs": 126200}]
    """;
    var timesActive = """
    [{"entryId": 62978, "position": 1, "elapsedDurationMs": 125400, "diffFirstMs": 0, "status": "Completed"},
     {"entryId": 62979, "position": null, "elapsedDurationMs": 100000, "diffFirstMs": null, "status": "Running"}]
    """;
    using var handler = new SequenceHttpHandler(new()
    {
        ["seasons.json"] = [FixtureText("seasons.json")],
        ["season-detail.json?seasonId=47"] = [FixtureText("season-detail.json")],
        ["events/644.json"] = [FixtureText("event-finland.json")],
        ["events/644/rallies/712/entries.json"] = [FixtureText("entries-finland.json")],
        // Running (goes active) -> Completed -> Completed (settles, blacklists) -> Running (must reopen).
        ["events/644/stages.json"] = [stageRunning, stageCompleted, stageCompleted, stageRunning],
        ["events/644/stages/11307/stagetimes.json?rallyId=712"] = [timesComplete, timesComplete, timesComplete, timesActive],
        ["events/644/stages/11307/results.json?rallyId=712"] = [resultsComplete, resultsComplete, resultsComplete, "[]"]
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var options = TestWrcOptions() with { ResultStablePolls = 2 };
    var source = new WrcLiveTimingSource(api, clock, delay, alerts, options);
    List<TimingSnapshot> snapshots = [];
    source.SnapshotReceived += snapshots.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitForCalls(4, TimeSpan.FromSeconds(2)));
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    // The stage settled (hidden) on the third poll; a Live snapshot published afterwards proves it reopened.
    True(snapshots.Any(s => s.Lifecycle == SessionLifecycle.Live));
    True(snapshots.Any(s => s.Lifecycle != SessionLifecycle.Live && s.ConnectionState == ConnectionState.Connected));
    Equal(SessionLifecycle.Live, snapshots[^1].Lifecycle);
    Equal("SS1  Harju 1", snapshots[^1].Session);
}

static void WrcFailureRecoveryConfirmation()
{
    var clock = new FakeClock(new DateTimeOffset(2026, 7, 31, 7, 30, 0, TimeSpan.Zero));
    var alerts = new AlertArbiter(clock);
    var delay = new StepDelay(2);
    // A future ToRun stage keeps the recovered poll idle: without the confirmation fix it would return
    // to the 5-minute idle interval; the fix must instead force one 15-second confirmation poll.
    var stageIdle = """
    [{"stageId": 11308, "eventId": 644, "number": 2, "code": "SS2", "name": "Laukaa 1", "status": "ToRun",
      "controls": [{"type": "StageStart", "firstCarDueDateTime": "2026-08-01T05:03:00Z"}]}]
    """;
    var stageRunning = """
    [{"stageId": 11307, "eventId": 644, "number": 1, "code": "SS1", "name": "Harju 1", "status": "Running",
      "controls": [{"type": "StageStart", "firstCarDueDateTime": "2026-07-31T07:05:00Z"}]}]
    """;
    var times = """
    [{"entryId": 62978, "position": 1, "elapsedDurationMs": 125400, "diffFirstMs": 0, "status": "Running"}]
    """;
    using var handler = new SequenceHttpHandler(new()
    {
        ["seasons.json"] = [FixtureText("seasons.json")],
        ["season-detail.json?seasonId=47"] = [FixtureText("season-detail.json")],
        ["events/644.json"] = [FixtureText("event-finland.json")],
        ["events/644/rallies/712/entries.json"] = [FixtureText("entries-finland.json")],
        ["events/644/stages.json"] = ["__FAIL__", stageIdle, stageRunning, stageRunning],
        ["events/644/stages/11307/stagetimes.json?rallyId=712"] = [times, times],
        ["events/644/stages/11307/results.json?rallyId=712"] = ["[]", "[]"]
    });
    using var api = new WrcApiClient(new HttpClient(handler));
    var options = TestWrcOptions() with
    {
        PollInterval = TimeSpan.FromSeconds(15),
        IdlePollInterval = TimeSpan.FromMinutes(5),
        PollBackoff = [TimeSpan.FromSeconds(1)]
    };
    var source = new WrcLiveTimingSource(api, clock, delay, alerts, options);
    List<FeedFailure> failures = [];
    source.Failed += failures.Add;
    List<ConnectionState> connections = [];
    source.ConnectionChanged += connections.Add;
    List<TimingSnapshot> snapshots = [];
    source.SnapshotReceived += snapshots.Add;
    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    True(delay.WaitForCalls(3, TimeSpan.FromSeconds(3)));
    source.StopAsync().GetAwaiter().GetResult();
    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    Equal(1, failures.Count);
    True(failures[0].Message.Contains("polling failed"));
    var delays = delay.Durations.ToList();
    True(delays.Count >= 3);
    Equal(TimeSpan.FromSeconds(1), delays[0]);
    Equal(TimeSpan.FromSeconds(15), delays[1]);
    True(connections.Contains(ConnectionState.Faulted));
    var live = snapshots.Where(s => s.Lifecycle == SessionLifecycle.Live).ToList();
    True(live.Count >= 1);
    Equal("SS1  Harju 1", live[^1].Session);
}

/// <summary>
/// The flag letters are black or white by WCAG contrast against the strip colour. Amber and red both read better with
/// black letters, and a dark or light background picks the colour that reads on it.
/// </summary>
static void FlagLetterContrast()
{
    // Amber (caution) and red (red flag) are the two strip colours the widget draws.
    True(TaskbarContrast.PrefersBlackText(0xE3, 0xB3, 0x41));
    True(TaskbarContrast.PrefersBlackText(0xF8, 0x51, 0x49));
    // Black and white backgrounds take the opposite letter, which pins down the direction of the rule.
    False(TaskbarContrast.PrefersBlackText(0x00, 0x00, 0x00));
    True(TaskbarContrast.PrefersBlackText(0xFF, 0xFF, 0xFF));
    // The measured ratios behind the choice: amber 10.8:1 with black against 1.95:1 with white.
    True(Math.Abs(TaskbarContrast.RelativeLuminance(0xE3, 0xB3, 0x41) - 0.50) < 0.02);
}

static void WrcHandlerBounds()
{
    using var handler = WrcApiClient.CreateDefaultHandler();
    Equal(TimeSpan.FromMinutes(2), handler.PooledConnectionLifetime);
    Equal(TimeSpan.FromMinutes(1), handler.PooledConnectionIdleTimeout);
    Equal(TimeSpan.FromSeconds(10), handler.ConnectTimeout);
}

static (FakeClock clock, AlertArbiter alerts, TimingStateProcessor processor) Fixture() { var c = new FakeClock(); var a = new AlertArbiter(c); return (c, a, new TimingStateProcessor(c, a)); }
static JsonObject BaseState() => JsonNode.Parse("""{"SessionInfo":{"Name":"Race","Meeting":{"Name":"Test","Circuit":{"ShortName":"Ring"}}},"TrackStatus":{"Status":"1"},"DriverList":{"1":{"Tla":"AAA"},"2":{"Tla":"BBB"}},"TimingData":{"Lines":{"1":{"Position":1,"NumberOfLaps":1},"2":{"Position":2,"GapToLeader":"+1.0","NumberOfLaps":1}}}}""")!.AsObject();
static void Track(TimingStateProcessor p, string status) => p.ProcessDelta("TrackStatus", new JsonObject { ["Status"] = status }, DateTimeOffset.UtcNow);
static void True(bool value, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0) { if (!value) throw new Exception($"Expected true (line {line})."); }
static void False(bool value, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0) { if (value) throw new Exception($"Expected false (line {line})."); }
static void Equal<T>(T expected, T actual, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected '{expected}', got '{actual}' (line {line})."); }
static string FixtureText(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Wrc", name));
static JsonObject SectorFixture(string name) => JsonNode.Parse(File.ReadAllText(
    Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sectors", name)))!.AsObject();
static Queue<HttpResponseMessage> Responses(params string[] bodies) => new(bodies.Select(body => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) }));
static WrcPollingOptions TestWrcOptions() => new(
    TimeSpan.FromHours(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), TimeSpan.FromHours(6),
    TimeSpan.FromHours(1), TimeSpan.FromMinutes(2), 3, 2,
    [TimeSpan.FromHours(1)], [TimeSpan.FromHours(1)]);

sealed class RecordingAlerts : IAlertArbiter
{
    public event Action<RaceAlert?>? VisibleAlertChanged;
    public RaceAlert? Current { get; private set; }
    public List<RaceAlert> Accepted { get; } = [];
    public List<TrackCondition> TrackConditions { get; } = [];
    public void Accept(RaceAlert alert)
    {
        Accepted.Add(alert);
        Current = alert;
        VisibleAlertChanged?.Invoke(alert);
    }
    public void ApplyTrackCondition(TrackCondition condition, string? detail, int? lap, DateTimeOffset timestamp) =>
        TrackConditions.Add(condition);
    public void Tick() { }
    public void Clear() => Current = null;
    public void Dismiss(AlertKind kind) { if (Current?.Kind == kind) Clear(); }
}

sealed class FakeClock : IClock
{
    public FakeClock() : this(new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero)) { }
    public FakeClock(DateTimeOffset now) => UtcNow = now;
    public DateTimeOffset UtcNow { get; private set; }
    public void Advance(TimeSpan duration) => UtcNow += duration;
}

sealed class BlockingDelay : IAsyncDelay
{
    private readonly ManualResetEventSlim _called = new();
    public TimeSpan? Duration { get; private set; }
    public async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        Duration = duration;
        _called.Set();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
    public bool WaitUntilCalled(TimeSpan timeout) => _called.Wait(timeout);
}

sealed class StepDelay(int autoRelease) : IAsyncDelay
{
    private readonly Lock _gate = new();
    private readonly List<TimeSpan> _durations = [];
    private int _calls;
    public IReadOnlyList<TimeSpan> Durations { get { lock (_gate) return _durations.ToArray(); } }
    public async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        bool release;
        lock (_gate) { _durations.Add(duration); release = ++_calls <= autoRelease; }
        if (release) return;
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
    public bool WaitForCalls(int count, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            lock (_gate) if (_calls >= count) return true;
            Thread.Sleep(10);
        }
        lock (_gate) return _calls >= count;
    }
}

/// <summary>A delay that only returns when the test releases it, so each tick can be inspected on its own.</summary>
sealed class HeldDelay : IAsyncDelay
{
    private readonly Lock _gate = new();
    private readonly List<TimeSpan> _durations = [];
    private readonly SemaphoreSlim _release = new(0);
    public IReadOnlyList<TimeSpan> Durations { get { lock (_gate) return _durations.ToArray(); } }
    public async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        lock (_gate) _durations.Add(duration);
        await _release.WaitAsync(cancellationToken);
    }
    public bool WaitUntilCalled(int count, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            lock (_gate) if (_durations.Count >= count) return true;
            Thread.Sleep(10);
        }
        lock (_gate) return _durations.Count >= count;
    }
    public void Release()
    {
        if (_release.CurrentCount <= 0) _release.Release();
    }
}

sealed class StaticJsonHandler(string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
}

// Returns each path's queued bodies in order, repeating the last body once the queue is exhausted.
// A body equal to "__FAIL__" makes the request throw, simulating a transport failure.
sealed class SequenceHttpHandler(Dictionary<string, string[]> routes) : HttpMessageHandler
{
    private readonly Dictionary<string, int> _index = [];
    public List<string> Paths { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery.TrimStart('/');
        const string prefix = "rb-wrccom-lintegration-yv-prod/api/";
        if (path.StartsWith(prefix, StringComparison.Ordinal)) path = path[prefix.Length..];
        Paths.Add(path);
        if (!routes.TryGetValue(path, out var bodies) || bodies.Length == 0)
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        var at = _index.TryGetValue(path, out var i) ? i : 0;
        _index[path] = Math.Min(at + 1, bodies.Length - 1);
        var body = bodies[Math.Min(at, bodies.Length - 1)];
        if (body == "__FAIL__") throw new HttpRequestException("Simulated WRC transport failure.");
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}

sealed class AdvancingDelay(FakeClock clock, TimeSpan step, int autoRelease) : IAsyncDelay
{
    private readonly Lock _gate = new();
    private int _calls;
    public async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        bool release;
        lock (_gate) { clock.Advance(step); release = ++_calls <= autoRelease; }
        if (release) return;
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}

// Serves each GTWC endpoint in order, repeating the last body; a body of "__404__" answers Not Found.
sealed class GtwcSequenceHandler(Dictionary<string, string[]> routes) : HttpMessageHandler
{
    private readonly Dictionary<string, int> _index = [];
    public List<string> Paths { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var file = request.RequestUri!.AbsolutePath.Split('/').Last();
        Paths.Add(file);
        if (!routes.TryGetValue(file, out var bodies) || bodies.Length == 0)
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        var at = _index.TryGetValue(file, out var i) ? i : 0;
        _index[file] = Math.Min(at + 1, bodies.Length - 1);
        var body = bodies[Math.Min(at, bodies.Length - 1)];
        if (body == "__404__") return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}

sealed class GtwcHttpHandler(Dictionary<string, string> routes) : HttpMessageHandler
{
    public List<string> Paths { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Paths.Add(request.RequestUri!.PathAndQuery);
        var file = request.RequestUri.AbsolutePath.Split('/').Last();
        var body = routes.GetValueOrDefault(file) ?? "{\"content\":{\"full\":{}}}";
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}

sealed class FakeIndyCarApiClient(IndyCarTimingPayload payload) : IIndyCarApiClient
{
    public Task<IndyCarTimingPayload> GetTimingAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new IndyCarTimingPayload((JsonObject)payload.Data.DeepClone(), payload.LastModified));
    public void Dispose() { }
}

/// <summary>The live feed endpoint: every request answers with the same body unless it is told to fail.</summary>
sealed class NascarFakeHttp(string path, string body) : HttpMessageHandler
{
    public int LiveCalls { get; private set; }
    public bool FailLiveFeed { get; set; }
    public string Body { get; set; } = body;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.RequestUri!.AbsolutePath.EndsWith(path, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected request to {request.RequestUri}.");
        LiveCalls++;
        if (FailLiveFeed) throw new HttpRequestException("relay refused");
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(Body) });
    }
}

/// <summary>A race-list read that stays pending until the test releases it.</summary>
sealed class HeldNascarScheduleClient : INascarScheduleClient
{
    private readonly TaskCompletionSource<JsonArray?> _races = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls { get; private set; }
    public Task<JsonArray?> GetRaceListAsync(int seriesId, int year, CancellationToken cancellationToken)
    {
        Calls++;
        return _races.Task;
    }
    public Task<JsonObject?> GetWeekendFeedAsync(int year, int seriesId, int? raceId, CancellationToken cancellationToken) =>
        Task.FromResult<JsonObject?>(null);
    public void Release(JsonArray races) => _races.SetResult(races);
    public void Dispose() { }
}

sealed class FakeNascarScheduleClient(JsonArray? races) : INascarScheduleClient
{
    private readonly Lock _gate = new();
    private readonly List<(int Year, int SeriesId, int? RaceId)> _weekendRequests = [];
    public int Calls { get; private set; }
    /// <summary>Per-series race lists; a series not listed answers with the shared list.</summary>
    public Dictionary<int, JsonArray?> BySeries { get; } = [];
    /// <summary>Weekend feeds by race id; an id not listed has no published weekend feed.</summary>
    public Dictionary<int, JsonObject> Weekends { get; } = [];
    public IReadOnlyList<(int Year, int SeriesId, int? RaceId)> WeekendRequests
    {
        get { lock (_gate) return _weekendRequests.ToArray(); }
    }
    /// <summary>The number of race-list requests still to fail, simulating a transport error.</summary>
    public int FailRaceLists { get; set; }
    public Task<JsonArray?> GetRaceListAsync(int seriesId, int year, CancellationToken cancellationToken)
    {
        Calls++;
        if (FailRaceLists > 0)
        {
            FailRaceLists--;
            throw new HttpRequestException("schedule unavailable");
        }
        return Task.FromResult(BySeries.TryGetValue(seriesId, out var own) ? own : races);
    }
    public Task<JsonObject?> GetWeekendFeedAsync(int year, int seriesId, int? raceId, CancellationToken cancellationToken)
    {
        lock (_gate) _weekendRequests.Add((year, seriesId, raceId));
        return Task.FromResult(raceId is { } id && Weekends.TryGetValue(id, out var feed) ? feed : null);
    }
    public void Dispose() { }
}

sealed class FakeAlkamelDdpClient(IEnumerable<JsonObject> messages) : IAlkamelDdpClient
{
    private readonly Lock _gate = new();
    private readonly Queue<JsonObject> _messages = new(messages.Select(message => (JsonObject)message.DeepClone()));
    private readonly List<JsonObject> _sent = [];
    public IReadOnlyList<JsonObject> Sent { get { lock (_gate) return _sent.Select(message => (JsonObject)message.DeepClone()).ToList(); } }
    public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public async Task<JsonObject?> ReceiveAsync(CancellationToken cancellationToken)
    {
        lock (_gate) if (_messages.Count > 0) return _messages.Dequeue();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return null;
    }
    public Task SendAsync(JsonObject message, CancellationToken cancellationToken)
    {
        lock (_gate) _sent.Add((JsonObject)message.DeepClone());
        return Task.CompletedTask;
    }
    public int SubscriptionCount(string name)
    {
        lock (_gate) return _sent.Count(message => JsonSupport.String(message["msg"]) == "sub" && JsonSupport.String(message["name"]) == name);
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class RouteHttpHandler(Dictionary<string, Queue<HttpResponseMessage>> routes) : HttpMessageHandler
{
    public List<string> Paths { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery.TrimStart('/');
        const string prefix = "rb-wrccom-lintegration-yv-prod/api/";
        if (path.StartsWith(prefix, StringComparison.Ordinal)) path = path[prefix.Length..];
        Paths.Add(path);
        if (!routes.TryGetValue(path, out var responses) || responses.Count == 0)
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        return Task.FromResult(responses.Dequeue());
    }
}

sealed class FakeTimingSource(TimingSnapshot snapshot) : ILiveTimingSource
{
    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived { add { } remove { } }
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed { add { } remove { } }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ConnectionChanged?.Invoke(ConnectionState.Connected);
        SnapshotReceived?.Invoke(snapshot);
        return Task.CompletedTask;
    }
    public void Publish(TimingSnapshot value) => SnapshotReceived?.Invoke(value);
    public Task StopAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class EventScheduleCalendar(UpcomingSession next, IReadOnlyList<UpcomingSession> sessions) : IChampionshipCalendar
{
    public IReadOnlyList<Championship> Championships { get; } = [next.Championship];
    public int UpcomingCalls { get; private set; }
    public int EventCalls { get; private set; }
    public Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        UpcomingCalls++;
        return Task.FromResult<IReadOnlyList<UpcomingSession>>([next]);
    }
    public Task<IReadOnlyList<UpcomingSession>> GetEventScheduleAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        EventCalls++;
        return Task.FromResult(sessions);
    }
}

sealed class StubCalendar : IChampionshipCalendar
{
    private readonly IReadOnlyList<UpcomingSession> _sessions;
    private readonly Exception? _failure;
    private readonly TimeSpan _delay;
    public StubCalendar(IReadOnlyList<UpcomingSession> sessions)
    {
        _sessions = sessions;
        Championships = sessions.Select(session => session.Championship).Distinct().ToList();
    }
    public StubCalendar(Championship championship, Exception failure) { _sessions = []; _failure = failure; Championships = [championship]; }
    public StubCalendar(Championship championship, TimeSpan delay) { _sessions = []; _delay = delay; Championships = [championship]; }
    public IReadOnlyList<Championship> Championships { get; }
    public int Calls { get; private set; }
    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        Calls++;
        if (_failure is not null) throw _failure;
        if (_delay > TimeSpan.Zero) await Task.Delay(_delay, cancellationToken);
        return _sessions;
    }
}
