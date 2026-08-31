// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using System.Text.Json.Nodes;
using RaceDock.Core;

var tests = new (string Name, Action Run)[]
{
    ("deep merge and mixed primitive fields", DeepMerge),
    ("leader gaps, intervals, ordering and missing values", TimingParsing),
    ("tied F1 times produce one leader", TiedF1Times),
    ("hidden F1 substitutions are excluded", HiddenF1Substitution),
    ("retired and fastest lap detection", FastestAndRetired),
    ("numeric F1 status does not override flags", NumericF1Status),
    ("track status mappings and alert priority", AlertPriority),
    ("taskbar flags follow the displayed track state", TaskbarFlagsFollowSnapshot),
    ("duplicate alerts and injectable expiry clock", DeduplicationAndExpiry),
    ("render throttling keeps newest immediate state", Throttling),
    ("DPI geometry and Explorer recovery", GeometryAndRecovery),
    ("stale and off-session transitions", FreshnessAndVisibility),
    ("archived F1 startup data remains hidden", ArchivedF1Startup),
    ("scenario timeline integration", ScenarioIntegration),
    ("Timing71 ZIP archives replay full and incremental states", TimingArchiveZipReplay),
    ("Timing71 MotoGP archives map Bike manufacturers", TimingArchiveMotoGp),
    ("Timing71 archives recognize INDYCAR", TimingArchiveIndyCar),
    ("Timing71 HAR archives load embedded manifest and states", TimingArchiveHarReplay),
    ("Timing71 archive patches enforce complexity limits", TimingArchivePatchLimits),
    ("Timing71 columns resolve by abbreviation and description", TimingArchiveAbbreviatedColumns),
    ("active feeds rotate at the display interval", FeedRotation),
    ("completed classifications remain visible for the configured interval", CompletedClassificationRetention),
    ("WRC categories follow championship eligibility", WrcCategoryEligibility),
    ("WRC season discovery excludes ERC and finds the next rally", WrcSeasonDiscovery),
    ("WRC provider keeps event rally and itinerary ids distinct", WrcProviderIdentifiers),
    ("WRC stage selection requires provider activity", WrcStageSelection),
    ("WRC stage scheduling uses bounded provider reconciliation", WrcStageScheduling),
    ("WRC stage fingerprints ignore provider row ordering", WrcStageFingerprintOrdering),
    ("WRC shakedown standings use actual best runs", WrcShakedownTiming),
    ("WRC missing shakedown data is not a feed failure", WrcOptionalShakedown),
    ("championship labels use compact taskbar and full flyout names", ChampionshipLabels),
    ("sponsored event titles have stable display names", EventNameShortening),
    ("practice and qualifying report best laps, races report last laps", SessionLapTimeMode),
    ("stored classification column orders are repaired", FlyoutColumnOrderNormalization),
    ("user settings persist and normalize safe ranges", UserSettingsPersistence),
    ("hidden championships stay disabled and out of sight", ChampionshipVisibility),
    ("the experimental flyout blur stays off until asked for", FlyoutBlurSettings),
    ("a feed dump records payloads verbatim per source", FeedDumpCapture),
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
    ("changelog entries drive release notes and update checks", ReleaseNotesAndUpdateCheck),
    ("Run values resolve the executable with or without the startup flag", RunValueExecutablePaths),
    ("GTWC discovery filters support sessions", GtwcDiscoveryFiltering),
    ("GTWC standings map teams manufacturers and classes", GtwcStandings),
    ("GTWC flags and statuses follow Swiss Timing values", GtwcFlagsAndStatuses),
    ("GTWC client uppercases ids and busts caches", GtwcClientPaths),
    ("GTWC source publishes an active classification", GtwcSourceLifecycle),
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
    ("INDYCAR practice maps official timing", IndyCarPracticeTiming),
    ("INDYCAR source publishes captured practice timing", IndyCarSourceLifecycle),
    ("INDYCAR source stays hidden while the feed is cold", IndyCarColdFeedLifecycle),
    ("INDYCAR source rejects stale active data", IndyCarStaleFeedLifecycle),
    ("F3 qualifying publishes aborted live data", F3QualifyingSnapshot),
    ("MotoGP lite payload maps Moto3 timing", MotoGpLitePayload),
    ("MotoGP source filters categories and publishes lifecycle", MotoGpSourceLifecycle),
    ("F3 standings include roster teams", F3RosterTeams),
    ("F2 standings include roster teams", F2RosterTeams),
    ("F1 practice timing derives missing live gaps", F1PracticeTiming),
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
    ("WRC production handler has bounded connection lifetime", WrcHandlerBounds)
};
var failures = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
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
    Equal(null, TaskbarFlagDisplay.Resolve(TrackCondition.AllClear, AlertKind.Yellow));
    Equal(AlertKind.Yellow, TaskbarFlagDisplay.Resolve(TrackCondition.Yellow));
    Equal(AlertKind.DoubleYellow, TaskbarFlagDisplay.Resolve(TrackCondition.DoubleYellow));
    Equal(AlertKind.RedFlag, TaskbarFlagDisplay.Resolve(TrackCondition.RedFlag));
    Equal(AlertKind.Chequered, TaskbarFlagDisplay.Resolve(TrackCondition.AllClear, AlertKind.Chequered));
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

/// <summary>
/// Feeds abbreviate headers inconsistently: Formula 1 and INDYCAR name the tyre column "T" and only
/// the description says "Tyre", so columns must resolve under both.
/// </summary>
static void TimingArchiveAbbreviatedColumns()
{
    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock-archive-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "abbreviated.zip");
    try
    {
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            WriteZipJson(archive, "manifest.json", new JsonObject
            {
                ["version"] = 1,
                ["name"] = "Formula 1",
                ["description"] = "Las Vegas Grand Prix - Qualifying",
                ["colSpec"] = new JsonArray
                {
                    new JsonArray("Num", "text", "Car number"), new JsonArray("State", "text"),
                    new JsonArray("Driver", "text"), new JsonArray("Team", "text"),
                    new JsonArray("Laps", "numeric"), new JsonArray("T", "text", "Tyre"),
                    new JsonArray("TS", "numeric", "Tyre stint - laps on these tyres since last stop"),
                    new JsonArray("Gap", "delta", "Gap to leader"), new JsonArray("Int", "delta", "Interval to car in front"),
                    new JsonArray("Spd", "numeric", "Last lap speed"),
                    new JsonArray("Last", "laptime", "Last lap time"), new JsonArray("Best", "laptime", "Best lap time")
                },
                ["trackDataSpec"] = new JsonArray()
            });
            WriteZipJson(archive, "01700000000.json", new JsonObject
            {
                ["cars"] = new JsonArray
                {
                    new JsonArray("1", "RUN", "VERSTAPPEN, Max", "Red Bull Racing", 11, "W", 4, 0.0, 0.0, 210.5,
                        new JsonArray(113.5, ""), new JsonArray(113.302, ""))
                },
                ["session"] = new JsonObject { ["flagState"] = "green", ["timeRemain"] = 600 }
            });
        }

        var source = new TimingArchiveReplaySource(path, new FakeClock());
        TimingSnapshot? snapshot = null;
        source.SnapshotReceived += value => snapshot = value;
        source.Speed = 20;
        source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        Thread.Sleep(10);
        source.Pause();
        Thread.Sleep(100);

        var standing = snapshot?.Competitors[0];
        // "T" resolves through its description rather than the unrelated "Tyre stint" column.
        Equal("W", standing?.Tyre);
        Equal("1:53.302", standing?.ResultTime);
        Equal("1:53.500", standing?.LastLapTime);
        source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    finally { Directory.Delete(directory, true); }
}

static void TimingArchiveZipReplay()
{
    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock-archive-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "sample.zip");
    try
    {
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            WriteZipJson(archive, "manifest.json", ArchiveManifest());
            WriteZipJson(archive, "01700000000.json", ArchiveState(10, 1.25));
            WriteZipJson(archive, "01700000001i.json", new JsonObject
            {
                ["cars"] = new JsonArray
                {
                    new JsonArray("add", "", new JsonArray
                    {
                        new JsonArray(2, new JsonArray("60", "RUN", "GTP", 3, "BRAUN, Colin", "Acura Meyer Shank Racing", "ACURA ARX-06", 10, 2.5, 1.75, new JsonArray(113.4, "")))
                    }),
                    new JsonArray("change", new JsonArray(0, 7), new JsonArray(10, 11)),
                    new JsonArray("change", new JsonArray(1, 8), new JsonArray(1.25, 0.75)),
                    new JsonArray("remove", "", new JsonArray { new JsonArray(1, "removed row") })
                },
                ["session"] = new JsonArray
                {
                    new JsonArray("change", "timeRemain", new JsonArray(3600, 3599))
                }
            });
        }

        var source = new TimingArchiveReplaySource(path, new FakeClock());
        TimingSnapshot? snapshot = null;
        var received = 0;
        source.SnapshotReceived += value => { snapshot = value; Interlocked.Increment(ref received); };
        source.Speed = 20;
        source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        Thread.Sleep(10);
        source.Pause();
        Thread.Sleep(100);
        Equal(1, received);
        Equal(2, source.FrameCount);
        Equal(Championship.Imsa, snapshot?.Championship);
        Equal("Endurance Grand Prix", snapshot?.Meeting);
        Equal("Race", snapshot?.Session);
        Equal("Marco Wittmann", snapshot?.Competitors[0].Name);
        Equal("WIT", snapshot?.Competitors[0].Code);
        Equal("BMW M Team WRT", snapshot?.Competitors[0].Team);
        Equal("LEAD", snapshot?.Competitors[0].GapToLeader);
        Equal("+1.250", snapshot?.Competitors[1].GapToLeader);
        Equal("1:00:00", snapshot?.TimeRemaining);
        source.Step();
        Equal(11, snapshot?.CurrentLap);
        Equal(2, snapshot?.Competitors.Count);
        Equal("60", snapshot?.Competitors[1].DriverId);
        Equal(3, snapshot?.Competitors[1].Position);
        Equal("+2.500", snapshot?.Competitors[1].GapToLeader);
        Equal("59:59", snapshot?.TimeRemaining);
        True(source.IsCompleted);
        var completedLap = snapshot?.CurrentLap;
        source.Step();
        Equal(completedLap, snapshot?.CurrentLap);
        Equal(2, source.FrameIndex);
        source.Restart();
        Equal(10, snapshot?.CurrentLap);
        Equal(2, snapshot?.Competitors.Count);
        Equal("25", snapshot?.Competitors[0].DriverId);
        Equal("7", snapshot?.Competitors[1].DriverId);
        Equal(1, source.FrameIndex);
        Task.WhenAll(Enumerable.Range(0, 8).Select(_ => source.DisposeAsync().AsTask())).GetAwaiter().GetResult();
        try
        {
            source.Step();
            throw new Exception("Expected stepping a disposed replay to fail.");
        }
        catch (ObjectDisposedException) { }
    }
    finally { Directory.Delete(directory, true); }
}

static void TimingArchiveMotoGp()
{
    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock-motogp-archive-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "motogp.zip");
    try
    {
        var manifest = new JsonObject
        {
            ["version"] = 1,
            ["name"] = "MotoGP",
            ["description"] = "Qatar Airways Grand Prix of Great Britain - Grand Prix",
            ["colSpec"] = new JsonArray
            {
                new JsonArray("Num", "text"), new JsonArray("State", "text"), new JsonArray("Driver", "text"),
                new JsonArray("Team", "text"), new JsonArray("Bike", "text"), new JsonArray("Laps", "numeric"),
                new JsonArray("Gap", "delta"), new JsonArray("Int", "delta"),
                new JsonArray("Last", "laptime"), new JsonArray("Best", "laptime")
            }
        };
        var state = new JsonObject
        {
            ["cars"] = new JsonArray
            {
                new JsonArray("25", "RUN", "FERNANDEZ, Raul", "SuperFile Trackhouse MotoGP Team", "Aprilia", 1, "", "", new JsonArray(120.991, ""), new JsonArray(198.173, "")),
                new JsonArray("49", "RUN", "DI GIANNANTONIO, Fabio", "Pertamina Enduro VR46 Racing Team", "Ducati", 1, 1.5, 1.5, new JsonArray(121.5, ""), new JsonArray(199.673, "")),
                new JsonArray("73", "RUN", "MARQUEZ, Alex", "BK8 Gresini Racing MotoGP", "Ducati", 1, 2.0, 0.5, new JsonArray(122.0, ""), new JsonArray(200.173, ""))
            },
            ["session"] = new JsonObject { ["flagState"] = "green", ["lapsRemain"] = 19 },
            ["messages"] = new JsonArray(),
            ["highlight"] = new JsonArray()
        };
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            WriteZipJson(archive, "manifest.json", manifest);
            WriteZipJson(archive, "01786276651.json", state);
        }

        var source = new TimingArchiveReplaySource(path, new FakeClock());
        TimingSnapshot? snapshot = null;
        source.SnapshotReceived += value => snapshot = value;
        source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        source.Pause();
        Equal(Championship.MotoGp, snapshot?.Championship);
        Equal("Aprilia", snapshot?.Competitors[0].Manufacturer);
        Equal("Ducati", snapshot?.Competitors[1].Manufacturer);
        Equal("Ducati", snapshot?.Competitors[2].Manufacturer);
        source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    finally { Directory.Delete(directory, true); }
}

static void TimingArchiveIndyCar()
{
    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock-indycar-archive-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "indycar.zip");
    try
    {
        var manifest = ArchiveManifest();
        manifest["name"] = "NTT INDYCAR SERIES";
        manifest["description"] = "Ontario Honda Dealers Indy at Markham - Practice 1";
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            WriteZipJson(archive, "manifest.json", manifest);
            WriteZipJson(archive, "01700000000.json", ArchiveState(10, 1.25));
        }
        var source = new TimingArchiveReplaySource(path, new FakeClock());
        TimingSnapshot? snapshot = null;
        source.SnapshotReceived += value => snapshot = value;
        source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        source.Pause();
        Equal(Championship.IndyCar, snapshot?.Championship);
        source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    finally { Directory.Delete(directory, true); }
}

static void TimingArchiveHarReplay()
{
    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock-har-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "sample.har");
    try
    {
        var embeddedState = ArchiveState(10, 1.25);
        var manifest = ArchiveManifest();
        manifest["description"] = "Motul SportsCar Endurance Grand Prix - Race";
        embeddedState["manifest"] = manifest;
        var entries = new JsonArray(HarEntry("2026-08-02T20:00:01Z", embeddedState));
        File.WriteAllText(path, new JsonObject { ["log"] = new JsonObject { ["entries"] = entries } }.ToJsonString());
        var source = new TimingArchiveReplaySource(path, new FakeClock());
        TimingSnapshot? snapshot = null;
        source.SnapshotReceived += value => snapshot = value;
        source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        source.Pause();
        Equal(1, source.FrameCount);
        Equal(2, snapshot?.Competitors.Count);
        Equal(TrackCondition.AllClear, snapshot?.TrackCondition);
        // Replay retains the captured value, while every presentation path receives its canonical name.
        Equal("Motul SportsCar Endurance Grand Prix", snapshot?.Meeting);
        Equal("IMSA WeatherTech SportsCar Championship · SportsCar Endurance Grand Prix",
            ChampionshipDisplay.FullEvent(snapshot!));
        source.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    finally { Directory.Delete(directory, true); }
}

static void TimingArchivePatchLimits()
{
    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock-archive-limit-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "unsafe.zip");
    try
    {
        var operations = new JsonArray();
        for (var index = 0; index < 30; index++)
        {
            var key = $"sparse{index}";
            operations.Add(new JsonArray("add", "", new JsonArray { new JsonArray(key, new JsonArray()) }));
            operations.Add(new JsonArray("add", key, new JsonArray { new JsonArray(10_000, "x") }));
        }
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            WriteZipJson(archive, "manifest.json", ArchiveManifest());
            WriteZipJson(archive, "01700000000.json", ArchiveState(10, 1.25));
            WriteZipJson(archive, "01700000001i.json", new JsonObject
            {
                ["cars"] = new JsonArray(),
                ["session"] = operations
            });
        }

        try
        {
            _ = new TimingArchiveReplaySource(path, new FakeClock());
            throw new Exception("Expected an unsafe sparse patch to be rejected.");
        }
        catch (InvalidDataException ex)
        {
            True(ex.Message.Contains("node limit", StringComparison.OrdinalIgnoreCase));
        }
    }
    finally { Directory.Delete(directory, true); }
}

static JsonObject ArchiveManifest() => new()
{
    ["version"] = 1,
    ["name"] = "IMSA WeatherTech SportsCar Championship",
    ["description"] = "Endurance Grand Prix - Race",
    ["colSpec"] = new JsonArray
    {
        new JsonArray("Num", "text"), new JsonArray("State", "text"), new JsonArray("Class", "class"),
        new JsonArray("PIC", "numeric"), new JsonArray("Driver", "text"), new JsonArray("Team", "text"),
        new JsonArray("Car", "text"), new JsonArray("Laps", "numeric"), new JsonArray("Gap", "delta"),
        new JsonArray("Int", "delta"), new JsonArray("Best", "laptime")
    },
    ["trackDataSpec"] = new JsonArray()
};

static JsonObject ArchiveState(int laps, double secondGap) => new()
{
    ["cars"] = new JsonArray
    {
        new JsonArray("25", "RUN", "GTP", 1, "WITTMANN, Marco", "BMW M Team WRT", "BMW M HYBRID V8", laps, "", "", new JsonArray(112.1, "sb")),
        new JsonArray("7", "PIT", "GTP", 2, "NASR, Felipe", "Porsche Penske Motorsport", "PORSCHE 963", laps, secondGap, secondGap, new JsonArray(113.2, ""))
    },
    ["session"] = new JsonObject { ["flagState"] = "green", ["timeRemain"] = 3600, ["timeElapsed"] = 1800 },
    ["messages"] = new JsonArray(),
    ["highlight"] = new JsonArray()
};

static JsonObject HarEntry(string timestamp, JsonObject content) => new()
{
    ["startedDateTime"] = timestamp,
    ["response"] = new JsonObject { ["content"] = new JsonObject { ["text"] = content.ToJsonString() } }
};

static void WriteZipJson(ZipArchive archive, string name, JsonObject value)
{
    using var writer = new StreamWriter(archive.CreateEntry(name).Open());
    writer.Write(value.ToJsonString());
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

    feed.Publish(TimingSnapshot.Hidden(clock.UtcNow));
    clock.Advance(TimeSpan.FromMinutes(5));
    Thread.Sleep(30);
    Equal("British Grand Prix", snapshots[^1].Meeting);
    clock.Advance(TimeSpan.FromMinutes(1));
    Thread.Sleep(30);
    Equal(SessionLifecycle.OffSession, snapshots[^1].Lifecycle);

    source.DisposeAsync().AsTask().GetAwaiter().GetResult();
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

static void F1PracticeTiming()
{
    var (_, _, processor) = Fixture();
    processor.ProcessInitial(JsonNode.Parse("""
        {"DriverList":{"1":{"Tla":"AAA"},"2":{"Tla":"BBB"},"3":{"Tla":"CCC"}},
         "TimingData":{"Lines":{
           "1":{"Position":1,"NumberOfLaps":10,"BestLapTime":{"Value":"1:47.000"}},
           "2":{"Position":2,"NumberOfLaps":11,"BestLapTime":{"Value":"1:47.250"}},
           "3":{"Position":3,"NumberOfLaps":9,"BestLapTime":{"Value":"1:47.800"}}}},
         "TimingAppData":{"Lines":{"2":{"Stints":[{"Compound":"MEDIUM"},{"Compound":"SOFT"}]}}}}
        """)!.AsObject());
    Equal("LEAD", processor.Current.Competitors[0].GapToLeader);
    Equal("+0.250", processor.Current.Competitors[1].GapToLeader);
    Equal("+0.250", processor.Current.Competitors[1].IntervalToPositionAhead);
    Equal("+0.800", processor.Current.Competitors[2].GapToLeader);
    Equal("+0.550", processor.Current.Competitors[2].IntervalToPositionAhead);
    Equal("SOFT", processor.Current.Competitors[1].Tyre);
}

static void F1QualifyingParts()
{
    var (_, _, processor) = Fixture();
    var state = BaseState();
    state["SessionInfo"]!["Name"] = "Qualifying";
    state["SessionInfo"]!["Type"] = "Qualifying";
    state["TimingData"]!["SessionPart"] = 1;
    processor.ProcessInitial(state);
    Equal("Qualifying 1", processor.Current.Session);

    processor.ProcessDelta("TimingData", new JsonObject { ["SessionPart"] = 2 }, DateTimeOffset.UtcNow);
    Equal("Qualifying 2", processor.Current.Session);
    processor.ProcessDelta("TimingData", new JsonObject { ["SessionPart"] = 3 }, DateTimeOffset.UtcNow);
    Equal("Qualifying 3", processor.Current.Session);

    var (_, _, fallback) = Fixture();
    state = BaseState();
    state["SessionInfo"]!["Name"] = "Sprint Qualifying";
    state["SessionInfo"]!["Type"] = "Qualifying";
    state["SessionData"] = new JsonObject
    {
        ["Series"] = new JsonArray(
            new JsonObject { ["QualifyingPart"] = 1 },
            new JsonObject { ["QualifyingPart"] = 2 })
    };
    fallback.ProcessInitial(state);
    Equal("Sprint Qualifying 2", fallback.Current.Session);
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

static void FlyoutColumnOrderNormalization()
{
    // A hand-edited order that repeats a column, names one that no longer exists, and omits four others.
    var repaired = new ChampionshipDisplaySettings
    {
        FlyoutColumnOrder = [FlyoutColumn.Status, FlyoutColumn.Status, (FlyoutColumn)99, FlyoutColumn.Tyre]
    }.Normalize().FlyoutColumnOrder;
    // The columns that were named keep their position, then the missing ones follow in the canonical order.
    Equal("Status,Tyre,Gap,Interval,LapTime,Category", string.Join(",", repaired));

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
        var order = new[] { FlyoutColumn.Status, FlyoutColumn.LapTime, FlyoutColumn.Tyre, FlyoutColumn.Category, FlyoutColumn.Interval, FlyoutColumn.Gap };
        store.Save(new UserSettings
        {
            ChampionshipDisplay = new ChampionshipDisplayProfiles()
                .With(Championship.Formula1, new ChampionshipDisplaySettings { FlyoutColumnOrder = order })
        });
        // The enum is written by name, so the file stays readable to anyone editing it by hand.
        True(File.ReadAllText(path).Contains("\"Status\""));
        Equal(string.Join(",", order), string.Join(",", store.Load().DisplayFor(Championship.Formula1).FlyoutColumnOrder));
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
        True(File.ReadAllText(targetPath).Contains("\"Version\": 1", StringComparison.Ordinal));
    }
    finally
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
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
    var now = DateTimeOffset.UtcNow;
    var f1 = new TimingSnapshot("Belgian Grand Prix", "Race", "Spa-Francorchamps", 1, 44,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.Formula1);
    var wrc = new TimingSnapshot("Secto Rally Finland", "SS1", "", 0, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.WorldRallyChampionship);
    var gtwc = new TimingSnapshot("Magny-Cours", "Qualifying 1 Group B", "Magny-Cours", 3, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.GtWorldChallengeEurope);
    var imsa = new TimingSnapshot("Motul SportsCar Endurance Grand Prix", "Race", "Road America", 33, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.Imsa);
    var moto3 = new TimingSnapshot("Qatar Airways Grand Prix of Great Britain", "Moto3 Practice", "Silverstone Circuit", 3, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.Moto3);
    var formulaE = new TimingSnapshot("London E-Prix", "Free Practice 2", "London", 22, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.FormulaE);
    var dtm = new TimingSnapshot("Nürburgring", "Race 1", "Nürburgring", 9, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.Dtm);
    var indyCar = new TimingSnapshot("Ontario Honda Dealers Indy at Markham", "Practice 1", "Streets of Markham", 10, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.IndyCar);
    Equal("F1 Spa", ChampionshipDisplay.CompactEvent(f1));
    Equal("Formula 1 · Belgian Grand Prix", ChampionshipDisplay.FullEvent(f1));
    Equal("WRC Finland", ChampionshipDisplay.CompactEvent(wrc));
    Equal("World Rally Championship · Rally Finland", ChampionshipDisplay.FullEvent(wrc));
    Equal("GTWC Magny", ChampionshipDisplay.CompactEvent(gtwc));
    Equal("GT World Challenge Europe · Magny-Cours", ChampionshipDisplay.FullEvent(gtwc));
    Equal("IMSA Road America", ChampionshipDisplay.CompactEvent(imsa));
    Equal("IMSA WeatherTech SportsCar Championship · SportsCar Endurance Grand Prix", ChampionshipDisplay.FullEvent(imsa));
    Equal("Moto3 Silverstone", ChampionshipDisplay.CompactEvent(moto3));
    Equal("Moto3 · British Grand Prix", ChampionshipDisplay.FullEvent(moto3));
    Equal("FE London", ChampionshipDisplay.CompactEvent(formulaE));
    Equal("Formula E · London E-Prix", ChampionshipDisplay.FullEvent(formulaE));
    Equal("DTM Nürburgring", ChampionshipDisplay.CompactEvent(dtm));
    Equal("DTM · Nürburgring", ChampionshipDisplay.FullEvent(dtm));
    Equal("INDYCAR Streets of Markham", ChampionshipDisplay.CompactEvent(indyCar));
    Equal("NTT INDYCAR SERIES · Markham Indy", ChampionshipDisplay.FullEvent(indyCar));
    // Provider and sponsor boilerplate is shortened consistently everywhere it is presented.
    Equal("Sprint Cup · Round 8", ChampionshipDisplay.ShortEventName(Championship.GtWorldChallengeEurope, "GT WorldChEU pwrd by AWS Sprint Cup Round 8"));
    Equal("San Marino Grand Prix", ChampionshipDisplay.ShortEventName(Championship.MotoGp, "RED Bull Grand Prix of SAN Marino and the Rimini Riviera"));
    Equal("Rally Paraguay", ChampionshipDisplay.ShortEventName(Championship.WorldRallyChampionship, "Ueno Rally Del Paraguay"));
    Equal("Italian Grand Prix", ChampionshipDisplay.ShortEventName(Championship.Formula1, "Italian Grand Prix"));
    Equal("MotoGP Silverstone", ChampionshipDisplay.CompactEvent(moto3 with
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
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "{\"EnableFormula1\":true}");
        var legacy = store.Load();
        True(legacy.AutoStartOnWindowsBoot);
        True(legacy.EnableGtWorldChallengeEurope);
        False(legacy.EnableImsa);
        store.Save(new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableGtWorldChallengeEurope = false,
            EnableWrc = false,
            EnableImsa = false,
            DriverCount = 99,
            RotationSeconds = 1,
            CompletedSessionRetentionMinutes = 99,
            UseManufacturerLogos = true,
            AutoStartOnWindowsBoot = false
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
        True(loaded.DisplayFor(Championship.Moto3).FlyoutUseManufacturerLogos);
        False(loaded.DisplayFor(Championship.Moto3).FlyoutShowTyre);
        False(loaded.DisplayFor(Championship.WorldRallyChampionship).FlyoutShowTeamNames);
        False(loaded.AutoStartOnWindowsBoot);
        False(loaded.EnableGtWorldChallengeEurope);
        var gtwcOnly = new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableGtWorldChallengeEurope = true,
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
            EnableWrc = false,
            EnableImsa = false,
            EnableIndyCar = true
        }.Normalize();
        False(indyCarOnly.EnableFormula1);
        True(indyCarOnly.EnableIndyCar);

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
            Championship.GtWorldChallengeEurope, Championship.WorldRallyChampionship, Championship.Imsa,
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

        // A first run draws manufacturer marks on the widget, while the retired global flag only
        // still speaks for the flyout.
        var fresh = new UserSettings().Normalize();
        True(fresh.DisplayFor(Championship.Formula1).TaskbarUseManufacturerLogos);
        False(fresh.DisplayFor(Championship.Formula1).FlyoutUseManufacturerLogos);
        False(fresh.DisplayFor(Championship.Formula2).TaskbarUseManufacturerLogos);
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

    var dtm = ChampionshipCalendars.NextDtm(JsonNode.Parse("""
    [
      { "name": "Race 2", "startTime": "2026-09-13T12:00:00+00:00", "event": { "name": "07 Sachsenring" } },
      { "name": "Free Practice", "startTime": "2026-09-11T07:00:00+00:00", "event": { "name": "07 Sachsenring", "trackConfig": { "track": { "name": "Sachsenring" } } } }
    ]
    """) as JsonArray, now);
    Equal("07 Sachsenring", dtm!.EventName);
    Equal("Free Practice", dtm.SessionName);
    Equal("Sachsenring", dtm.Venue);

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
        "u1": { "Name": "Free Practice 1", "CompetitionId": "c1", "BeginTime": "29.08.2026 10:50:00" },
        "u2": { "Name": "Super Trofeo Race", "CompetitionId": "c2", "BeginTime": "29.08.2026 09:00:00" },
        "u3": { "Name": "Qualifying 1", "CompetitionId": "c1", "BeginTime": "30.08.2026 09:45:00" }
      }
    }
    """;
    var meeting = ChampionshipCalendars.NextGtWorldChallengeMeeting(JsonNode.Parse(gtwcSeason) as JsonObject, now);
    Equal("Round 7", meeting!.Name);
    Equal(new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero), meeting.StartUtc);

    // Nurburgring runs on central European summer time, so 10:50 local is 08:50 UTC.
    var gtwc = ChampionshipCalendars.NextGtWorldChallenge(meeting, JsonNode.Parse(gtwcSchedule) as JsonObject, now);
    Equal("Round 7", gtwc.EventName);
    Equal("Free Practice 1", gtwc.SessionName);
    Equal("Nurburgring", gtwc.Venue);
    Equal(new DateTimeOffset(2026, 8, 29, 8, 50, 0, TimeSpan.Zero), gtwc.StartUtc);

    // Once a session has started the countdown moves to the next one.
    var duringRaceWeekend = new DateTimeOffset(2026, 8, 29, 9, 0, 0, TimeSpan.Zero);
    var runningGtwc = ChampionshipCalendars.NextGtWorldChallenge(
        ChampionshipCalendars.NextGtWorldChallengeMeeting(JsonNode.Parse(gtwcSeason) as JsonObject, duringRaceWeekend)!,
        JsonNode.Parse(gtwcSchedule) as JsonObject,
        duringRaceWeekend);
    Equal("Round 7", runningGtwc.EventName);
    Equal("Qualifying 1", runningGtwc.SessionName);

    // Without a timetable the round still shows, carrying its meeting date and no session name.
    var withoutSchedule = ChampionshipCalendars.NextGtWorldChallenge(meeting, null, now);
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
           "support":{"Id":"support","Name":"F4 Race","CompetitionId":"f4c","State":1}}}
        """)!.AsObject();
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
    Equal(TrackCondition.VirtualSafetyCar, fcy.Condition);
    Equal("FULL COURSE YELLOW", fcy.Detail);
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

static void ImsaFlagsAndClock()
{
    Equal(TrackCondition.AllClear, ImsaTiming.TrackState(JsonNode.Parse("{\"currentFlag\":\"GF\"}")!.AsObject()).Condition);
    var fcy = ImsaTiming.TrackState(JsonNode.Parse("{\"currentFlag\":\"FCY\"}")!.AsObject());
    Equal(TrackCondition.Yellow, fcy.Condition);
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
static void True(bool value) { if (!value) throw new Exception("Expected true."); }
static void False(bool value) { if (value) throw new Exception("Expected false."); }
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected '{expected}', got '{actual}'."); }
static string FixtureText(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Wrc", name));
static Queue<HttpResponseMessage> Responses(params string[] bodies) => new(bodies.Select(body => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) }));
static WrcPollingOptions TestWrcOptions() => new(
    TimeSpan.FromHours(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), TimeSpan.FromHours(6),
    TimeSpan.FromHours(1), TimeSpan.FromMinutes(2), 3, 2,
    [TimeSpan.FromHours(1)], [TimeSpan.FromHours(1)]);

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
