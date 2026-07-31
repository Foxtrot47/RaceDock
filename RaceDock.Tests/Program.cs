// SPDX-License-Identifier: GPL-3.0-or-later
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
    ("active feeds rotate at the display interval", FeedRotation),
    ("WRC categories follow championship eligibility", WrcCategoryEligibility),
    ("WRC season discovery excludes ERC and finds the next rally", WrcSeasonDiscovery),
    ("WRC provider keeps event rally and itinerary ids distinct", WrcProviderIdentifiers),
    ("WRC stage selection requires provider activity", WrcStageSelection),
    ("WRC stage scheduling uses bounded provider reconciliation", WrcStageScheduling),
    ("WRC stage fingerprints ignore provider row ordering", WrcStageFingerprintOrdering),
    ("WRC shakedown standings use actual best runs", WrcShakedownTiming),
    ("WRC missing shakedown data is not a feed failure", WrcOptionalShakedown),
    ("championship labels use compact taskbar and full flyout names", ChampionshipLabels),
    ("user settings persist and normalize safe ranges", UserSettingsPersistence),
    ("F3 qualifying publishes aborted live data", F3QualifyingSnapshot),
    ("F3 standings include roster teams", F3RosterTeams),
    ("F2 standings include roster teams", F2RosterTeams),
    ("F1 practice timing derives missing live gaps", F1PracticeTiming),
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

static void ChampionshipLabels()
{
    var now = DateTimeOffset.UtcNow;
    var f1 = new TimingSnapshot("Belgian Grand Prix", "Race", "Spa-Francorchamps", 1, 44,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.Formula1);
    var wrc = new TimingSnapshot("Secto Rally Finland", "SS1", "", 0, null,
        TrackCondition.AllClear, [], now, SessionLifecycle.Live, Championship: Championship.WorldRallyChampionship);
    Equal("F1 Spa", ChampionshipDisplay.CompactEvent(f1));
    Equal("Formula 1 · Belgian Grand Prix", ChampionshipDisplay.FullEvent(f1));
    Equal("WRC Secto Rally Finland", ChampionshipDisplay.CompactEvent(wrc));
    Equal("World Rally Championship · Secto Rally Finland", ChampionshipDisplay.FullEvent(wrc));
}

static void UserSettingsPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"RaceDock.Tests.{Guid.NewGuid():N}");
    var path = Path.Combine(directory, "settings.json");
    try
    {
        var store = new UserSettingsStore(path);
        store.Save(new UserSettings
        {
            EnableFormula1 = false,
            EnableFormula2 = false,
            EnableFormula3 = false,
            EnableWrc = false,
            DriverCount = 99,
            RotationSeconds = 1,
            UseManufacturerLogos = true
        });
        var loaded = store.Load();
        True(loaded.EnableFormula1);
        Equal(5, loaded.DriverCount);
        Equal(2, loaded.RotationSeconds);
        True(loaded.UseManufacturerLogos);
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

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
    public Task StopAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
