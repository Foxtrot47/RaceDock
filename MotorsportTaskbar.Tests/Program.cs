// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;
using MotorsportTaskbar.Core;

var tests = new (string Name, Action Run)[]
{
    ("deep merge and mixed primitive fields", DeepMerge),
    ("leader gaps, intervals, ordering and missing values", TimingParsing),
    ("retired and fastest lap detection", FastestAndRetired),
    ("track status mappings and alert priority", AlertPriority),
    ("duplicate alerts and injectable expiry clock", DeduplicationAndExpiry),
    ("render throttling keeps newest immediate state", Throttling),
    ("DPI geometry and Explorer recovery", GeometryAndRecovery),
    ("stale and off-session transitions", FreshnessAndVisibility),
    ("scenario timeline integration", ScenarioIntegration),
    ("active feeds rotate at the display interval", FeedRotation),
    ("WRC categories follow championship eligibility", WrcCategoryEligibility),
    ("F1 practice timing derives missing live gaps", F1PracticeTiming),
    ("WRC stage timing preserves live API values", WrcStageTiming)
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

static void FastestAndRetired()
{
    var (_, alerts, p) = Fixture(); p.ProcessInitial(BaseState());
    p.ProcessDelta("TimingData", JsonNode.Parse("""{"Lines":{"1":{"Retired":"true","Status":68,"BestLapTime":{"Value":"1:20.500","OverallFastest":"true"}}}}""")!, DateTimeOffset.UtcNow);
    True(p.Current.Competitors.Single(x => x.DriverId == "1").Retired); Equal(AlertKind.FastestLap, alerts.Current?.Kind); True(p.Current.Competitors.Single(x => x.DriverId == "1").IsOverallFastest);
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
    var noSafeGap = TaskbarGeometry.CalculateLeftChild(new(0, 0, 1280, 48), 96, [new(0, 0, 240, 48)]); Equal<(int X, int Y, int Width, int Height)?>(null, noSafeGap);
    var hi = TaskbarGeometry.CalculateLeftChild(new(0, 0, 3840, 96), 192)!.Value; Equal(16, hi.X); Equal(1040, hi.Width); Equal(80, hi.Height);
    True(TaskbarRecovery.NeedsReattach(10, 0, false)); True(TaskbarRecovery.NeedsReattach(10, 10, true)); False(TaskbarRecovery.NeedsReattach(10, 10, false));
}

static void FreshnessAndVisibility()
{
    var (clock, _, p) = Fixture(); Equal(SessionLifecycle.OffSession, p.Current.Lifecycle); p.ProcessInitial(BaseState()); Equal(SessionLifecycle.Live, p.Current.Lifecycle);
    clock.Advance(TimeSpan.FromSeconds(13)); p.MarkStale(TimeSpan.FromSeconds(12)); Equal(ConnectionState.Stale, p.Current.ConnectionState);
    p.ProcessInitial(new JsonObject()); Equal(SessionLifecycle.OffSession, p.Current.Lifecycle);
    p.ProcessInitial(BaseState());
    p.ProcessDelta("SessionData", new JsonObject { ["StatusSeries"] = new JsonArray(new JsonObject { ["SessionStatus"] = "Finished" }) }, clock.UtcNow);
    Equal(SessionLifecycle.Ended, p.Current.Lifecycle);
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
    static JsonObject Entry(string group, string eligibility, string eventClass) => new()
    {
        ["group"] = new JsonObject { ["name"] = group },
        ["eligibility"] = eligibility,
        ["eventClasses"] = new JsonArray(new JsonObject { ["name"] = eventClass })
    };

    Equal("WRC2", WrcLiveTimingSource.RallyCategory(Entry("Rally2", "WRC2 (DC/CC)", "RC2")));
    Equal("RC2", WrcLiveTimingSource.RallyCategory(Entry("Rally2", "", "RC2")));
    Equal("WRC3", WrcLiveTimingSource.RallyCategory(Entry("Rally3", "WRC3", "RC3")));
    Equal("RC3", WrcLiveTimingSource.RallyCategory(Entry("Rally3", "", "RC3")));
    Equal("WRC1", WrcLiveTimingSource.RallyCategory(Entry("Rally1", "", "RC1")));
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
    static JsonObject Entry(int id, string code, int order) => new()
    {
        ["entryId"] = id,
        ["entryListOrder"] = order,
        ["driver"] = new JsonObject { ["code"] = code, ["fullName"] = code },
        ["manufacturer"] = new JsonObject { ["name"] = "Test" },
        ["group"] = new JsonObject { ["name"] = "Rally1" }
    };
    Dictionary<int, JsonObject> entries = new()
    {
        [1] = Entry(1, "AAA", 1),
        [2] = Entry(2, "BBB", 2),
        [3] = Entry(3, "CCC", 3)
    };
    var times = JsonNode.Parse("""
        [{"entryId":1,"position":2,"status":"Completed","elapsedDurationMs":516600,"diffFirstMs":4100,"diffPrevMs":2100},
         {"entryId":2,"position":1,"status":"Completed","elapsedDurationMs":512500,"diffFirstMs":0,"diffPrevMs":0},
         {"entryId":3,"status":"ToRun"}]
        """)!.AsArray();
    var standings = WrcLiveTimingSource.BuildStandings(entries, times);
    Equal("BBB", standings[0].Code);
    Equal("8:32.5", standings[0].ResultTime);
    Equal("AAA", standings[1].Code);
    Equal("+0:04.1", standings[1].GapToLeader);
    Equal("+0:02.1", standings[1].IntervalToPositionAhead);
    Equal("8:36.6", standings[1].ResultTime);
    Equal("DUE", standings[2].StatusLabel);
}

static TimingSnapshot Snapshot(string meeting) => new(meeting, "Live", "Test", 0, null, TrackCondition.AllClear,
    [new CompetitorStanding("1", 1, "AAA", "Driver", "Team", "LEAD", null, null, 0, false, false, false, false)],
    DateTimeOffset.UtcNow, SessionLifecycle.Live);

static (FakeClock clock, AlertArbiter alerts, TimingStateProcessor processor) Fixture() { var c = new FakeClock(); var a = new AlertArbiter(c); return (c, a, new TimingStateProcessor(c, a)); }
static JsonObject BaseState() => JsonNode.Parse("""{"SessionInfo":{"Name":"Race","Meeting":{"Name":"Test","Circuit":{"ShortName":"Ring"}}},"TrackStatus":{"Status":"1"},"DriverList":{"1":{"Tla":"AAA"},"2":{"Tla":"BBB"}},"TimingData":{"Lines":{"1":{"Position":1,"NumberOfLaps":1},"2":{"Position":2,"GapToLeader":"+1.0","NumberOfLaps":1}}}}""")!.AsObject();
static void Track(TimingStateProcessor p, string status) => p.ProcessDelta("TrackStatus", new JsonObject { ["Status"] = status }, DateTimeOffset.UtcNow);
static void True(bool value) { if (!value) throw new Exception("Expected true."); }
static void False(bool value) { if (value) throw new Exception("Expected false."); }
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected '{expected}', got '{actual}'."); }

sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    public void Advance(TimeSpan duration) => UtcNow += duration;
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
