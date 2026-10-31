// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Nodes;
using RaceDock.Core;

internal static class FeedDumpReplayTests
{
    public static void GtwcTransport()
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Sectors", "gtwc-portimao-fp1.json")))!.AsObject();
        var timing = fixture["timing"]!.AsObject();
        var detail = fixture["detail"]!.AsObject();
        var unitId = JsonSupport.String(timing["UnitId"])!;
        var schedule = new JsonObject
        {
            ["MeetingId"] = "meeting",
            ["Series"] = new JsonObject { ["gt"] = new JsonObject { ["Name"] = "GTWorldChEU pwrd by AWS Endurance Cup" } },
            ["Competitions"] = new JsonObject { ["competition"] = new JsonObject { ["SerieId"] = "gt" } },
            ["Units"] = new JsonObject { [unitId] = new JsonObject { ["Name"] = "Free Practice 1", ["CompetitionId"] = "competition" } }
        };
        var season = new JsonObject
        {
            ["Meetings"] = new JsonObject { ["meeting"] = new JsonObject { ["TrackName"] = "Portimao" } }
        };
        var ended = timing.DeepClone().AsObject();
        ended["UntInfo"]!["ChequeredFlag"] = true;
        var path = TemporaryPath();
        try
        {
            var start = DateTimeOffset.Parse("2026-10-16T07:49:23Z");
            Write(path, [Entry(start, timing), Entry(start.AddMilliseconds(100), detail),
                Entry(start.AddMilliseconds(200), season), Entry(start.AddMilliseconds(300), schedule),
                Entry(start.AddSeconds(10), ended), Entry(start.AddSeconds(10.1), detail)]);
            var clock = new TestClock();
            var source = FeedDumpReplayLoader.CreateAsync(path, clock).GetAwaiter().GetResult();
            List<TimingSnapshot> snapshots = [];
            source.SnapshotReceived += snapshots.Add;
            try
            {
                source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                Check(source.IsPaused && source.FrameCount == 2 && source.FrameIndex == 1, "GTWC must start paused on its first frame");
                var first = snapshots.Single();
                Check(first.Championship == Championship.GtWorldChallengeEurope && first.Meeting == "Portimao", "GTWC metadata must survive replay");
                Check(first.Lifecycle == SessionLifecycle.Live && first.FreshnessTimestamp == clock.UtcNow, "Recorded flags, not wall time, determine lifecycle");
                Check(first.Competitors[0].CurrentSectors?.Sector3?.Value == "38.050", "Sector data must survive replay");
                Check(first.Competitors[0].CarNumber == "10", "Car numbers must survive replay");
                source.Step();
                Check(source.IsCompleted && snapshots[^1].Lifecycle == SessionLifecycle.Ended, "Step must reach recorded chequered state");
                source.Restart();
                Check(source.IsPaused && !source.IsCompleted && source.FrameIndex == 1, "Restart must reset playback paused");
                Check(snapshots[^1].Lifecycle == SessionLifecycle.Live, "Restart must not retain terminal flags");
                source.Speed = 100;
                Check(source.Speed == 20, "Speed must retain its validation range");
                source.Resume();
                Check(SpinWait.SpinUntil(() => source.IsCompleted, TimeSpan.FromSeconds(5)), "Playback must complete");
            }
            finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            Write(path, [Entry(start, timing), Entry(start, season), Entry(start, schedule)]);
            Reject(path);
            var mismatched = detail.DeepClone().AsObject();
            mismatched["UnitId"] = "different-session";
            Write(path, [Entry(start, timing), Entry(start, mismatched), Entry(start, season), Entry(start, schedule)]);
            Reject(path);
        }
        finally { File.Delete(path); }
    }

    public static void NascarTransport()
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Nascar", "recorded-transitions.json")))!.AsObject();
        List<FeedDumpEntry> entries = [];
        foreach (var session in new[] { "qualifying", "race" })
            foreach (var step in fixture[session]!.AsArray())
                entries.Add(Raw(DateTimeOffset.Parse(step!["captured"]!.GetValue<string>()), step["payload"]!));
        // The endpoint re-serves an assembly until it builds a new one, and answers between sessions with an empty
        // field; neither is a frame.
        entries.Insert(1, Raw(entries[0].Timestamp.AddSeconds(10), JsonNode.Parse(entries[0].Payload)!));
        entries.Insert(6, Raw(entries[5].Timestamp.AddSeconds(10), new JsonObject { ["series_id"] = 1, ["vehicles"] = new JsonArray() }));
        var path = TemporaryPath();
        try
        {
            Write(path, entries);
            var source = FeedDumpReplayLoader.CreateAsync(path, new TestClock()).GetAwaiter().GetResult();
            List<TimingSnapshot> snapshots = [];
            source.SnapshotReceived += snapshots.Add;
            try
            {
                source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                Check(source is TimingArchiveReplaySource && source.ArchiveName.StartsWith("NASCAR · ", StringComparison.Ordinal),
                    "NASCAR dumps must use the NASCAR replay");
                Check(source.IsPaused && source.FrameCount == 18 && source.FrameIndex == 1,
                    "Re-served assemblies and empty fields must not become frames");
                for (var index = 1; index < source.FrameCount; index++) source.Step();
                Check(source.IsCompleted, "Stepping must reach the last recorded frame");
                Check(string.Join(",", snapshots.Select(value => value.Lifecycle)) ==
                    "Live,Ended,PreSession,Live,Ended,PreSession,Live,Live,Live,Live,Live,Live,Live,Live,Live,Live,Ended,Ended",
                    "Recorded flags must drive the NASCAR lifecycle in replay");
                Check(snapshots.Take(5).All(value => value.Championship == Championship.NascarCup) &&
                    snapshots.Skip(5).All(value => value.Championship == Championship.NascarXfinity),
                    "Each payload must replay as the series it belongs to");
                Check(snapshots[^1].TotalLaps == 94 && snapshots[^2].StageNumber == 3 &&
                    snapshots.All(value => value.ConnectionState == ConnectionState.Connected),
                    "Race distance, stage, and connection must survive replay");
            }
            finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            Write(path, [entries[6]]);
            Reject(path);
        }
        finally { File.Delete(path); }
    }

    public static void FiaTransport()
    {
        var start = DateTimeOffset.Parse("2026-10-24T20:44:57Z");
        static JsonObject Line(string number, int position, string code) => new()
        {
            ["position"] = new JsonObject { ["Value"] = position.ToString() },
            ["driver"] = new JsonObject { ["RacingNumber"] = number, ["FullName"] = code, ["TLA"] = code },
            ["laps"] = new JsonObject { ["Value"] = "3" }
        };
        static JsonObject Initial(string session, JsonObject lines) => new()
        {
            ["R"] = new JsonObject
            {
                ["data"] = new JsonArray("2026-10-24T20:44:58", new JsonObject { ["Series"] = "F1 Academy", ["Session"] = "Race" }, lines),
                ["racedetailsfeed"] = new JsonArray("2026-10-24T20:44:58", new JsonObject { ["Race"] = "Austin", ["Session"] = session }),
                ["sessionfeed"] = new JsonArray("2026-10-24T20:44:58", new JsonObject { ["Value"] = "Started" })
            },
            ["I"] = "1"
        };
        // Datafeed lines carry Number, the display slot, beside the car: slot 2 here is not car 2.
        static JsonObject Slot(string slot, string number, int position) => new()
        {
            ["Number"] = slot,
            ["driver"] = new JsonObject { ["RacingNumber"] = number },
            ["position"] = new JsonObject { ["Value"] = position.ToString() }
        };
        var update = new JsonObject
        {
            ["M"] = new JsonArray(new JsonObject
            {
                ["H"] = "Streaming",
                ["M"] = "datafeed",
                ["A"] = new JsonArray("2026-10-24T20:45:10", 0, new JsonObject
                {
                    ["lines"] = new JsonArray(Slot("1", "9", 1), Slot("2", "1", 2), Slot("3", "2", 3))
                })
            })
        };
        var path = TemporaryPath();
        try
        {
            Write(path,
            [
                // Handshakes and keep-alives come first and must not decide the format.
                Raw(start, new JsonObject { ["C"] = "s-0", ["S"] = 1, ["M"] = new JsonArray() }),
                Raw(start, new JsonObject()),
                Raw(start, new JsonObject { ["I"] = "0" }),
                Raw(start.AddSeconds(1), Initial("Opening Race", new JsonObject
                {
                    ["1"] = Line("1", 1, "ONE"), ["2"] = Line("2", 2, "TWO"), ["9"] = Line("9", 3, "NIN")
                })),
                Raw(start.AddSeconds(13), update),
                // A reconnect into the next session resends the whole field, which replaces the lines before it.
                Raw(start.AddHours(2), Initial("Reverse Grid Race", new JsonObject
                {
                    ["9"] = Line("9", 1, "NIN"), ["5"] = Line("5", 2, "FIV")
                }))
            ]);
            var source = FeedDumpReplayLoader.CreateAsync(path, new TestClock()).GetAwaiter().GetResult();
            List<TimingSnapshot> snapshots = [];
            source.SnapshotReceived += snapshots.Add;
            try
            {
                source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                Check(source is TimingArchiveReplaySource && source.ArchiveName == "F1 Academy · United States Grand Prix · Reverse Grid Race",
                    "F1 Academy dumps must replay through the FIA hub parser");
                Check(source.FrameCount == 3, "Only frames that change the field become replay frames");
                source.Step();
                source.Step();
                Check(snapshots.All(value => value.Championship == Championship.F1Academy &&
                    value.ConnectionState == ConnectionState.Connected), "The recorded series must survive replay");
                Check(snapshots.All(value => value.Competitors.Select(c => c.Position).Distinct().Count() == value.Competitors.Count),
                    "A display slot must never become a second car");
                Check(string.Join(",", snapshots[1].Competitors.Select(c => c.Code)) == "NIN,ONE,TWO",
                    "Slot updates must move the cars they name");
                Check(string.Join(",", snapshots[^1].Competitors.Select(c => c.Code)) == "NIN,FIV" &&
                    snapshots[^1].Session == "Reverse Grid Race", "A resent field must replace the previous session's lines");
            }
            finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }

            // Without a series in the recording or the file name there is no way to choose the roster.
            var unnamed = Initial("Race", new JsonObject { ["1"] = Line("1", 1, "ONE") });
            unnamed["R"]!["data"]![1]!["Series"] = null;
            Write(path, [Raw(start, unnamed)]);
            Reject(path);
        }
        finally { File.Delete(path); }
    }

    public static void WecTransport()
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Sectors", "wec-replay.json")))!.AsObject();
        var discovery = fixture["discovery"]!;
        var frames = fixture["frames"]!.AsArray();
        var path = TemporaryPath();
        try
        {
            var entries = new List<FeedDumpEntry>
            {
                Raw(DateTimeOffset.Parse(frames[0]!["timestamp"]!.GetValue<string>()), new JsonArray(discovery.DeepClone())),
                Raw(DateTimeOffset.Parse(frames[0]!["timestamp"]!.GetValue<string>()), frames[0]!["bootstrap"]!),
                Raw(DateTimeOffset.Parse(frames[1]!["timestamp"]!.GetValue<string>()), frames[1]!["bootstrap"]!)
            };
            Write(path, entries);
            var source = FeedDumpReplayLoader.CreateAsync(path, new TestClock()).GetAwaiter().GetResult();
            List<TimingSnapshot> snapshots = [];
            source.SnapshotReceived += snapshots.Add;
            try
            {
                source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                Check(source.IsPaused && source.FrameCount == 2 && source.FrameIndex == 1, "WEC must start paused on its first frame");
                var first = snapshots.Single();
                Check(first.Championship == Championship.WorldEnduranceChampionship && first.Meeting.Contains("Barcelona"),
                    "WEC discovery must survive replay");
                Check(first.Lifecycle == SessionLifecycle.Live, "Recorded WEC flags must determine lifecycle");
                Check(first.Competitors.Select(value => value.VirtualEnergyPercent).SequenceEqual([83d, 83d]),
                    "WEC virtual energy must survive replay");
                Check(first.Competitors.Any(value => value.CurrentTyres is not null), "WEC tyre ages must survive replay");
                Check(first.Competitors.Any(value => value.CurrentSectors is not null), "WEC sectors must survive replay");
                source.Step();
                Check(source.IsCompleted && snapshots[^1].Lifecycle == SessionLifecycle.Ended,
                    "Step must reach the recorded chequered state");
                Check(snapshots[^1].Competitors.Select(value => value.VirtualEnergyPercent).SequenceEqual([15d, 83d]),
                    "Energy changes must survive replay");
                source.Restart();
                Check(source.IsPaused && !source.IsCompleted && snapshots[^1].Lifecycle == SessionLifecycle.Live,
                    "Restart must reset playback paused");
            }
            finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            Write(path, [entries[2]]);
            Reject(path);
            var renamed = frames[0]!["bootstrap"]!.DeepClone().AsObject();
            renamed["sessionInfo"]!["sessionName"] = "Not recorded";
            Write(path, [entries[0], Raw(entries[1].Timestamp, renamed)]);
            Reject(path);
            Write(path, [Raw(DateTimeOffset.UtcNow, JsonNode.Parse("[]")!)]);
            Reject(path);
        }
        finally { File.Delete(path); }
    }

    public static void FormatValidation()
    {
        var path = TemporaryPath();
        try
        {
            // The F1 transport retains its own parser and paused-start behavior, including separators.
            var initial = """{"type":3,"result":{"SessionInfo":{"Name":"Race","Meeting":{"Name":"Test GP"}},"DriverList":{"1":{"Tla":"AAA"}},"TimingData":{"Lines":{"1":{"Position":1,"NumberOfLaps":1}}},"SessionStatus":{"Status":"Started"}}}""";
            File.WriteAllText(path, JsonSerializer.Serialize(new FeedDumpEntry(DateTimeOffset.UtcNow, initial + '\u001e')) + "\n");
            var source = FeedDumpReplayLoader.CreateAsync(path, new TestClock()).GetAwaiter().GetResult();
            try
            {
                Check(source is F1FeedDumpReplaySource, "F1 must still use the existing F1 loader");
                source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                Check(source.IsPaused, "F1 must still start paused");
            }
            finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            File.WriteAllText(path, "malformed\n");
            Reject(path);
            File.WriteAllText(path, JsonSerializer.Serialize(new FeedDumpEntry(DateTimeOffset.UtcNow, "{")));
            Reject(path);
            File.WriteAllText(path, new string('x', 8 * 1024 * 1024 + 1));
            Reject(path);
            Write(path, [Entry(DateTimeOffset.UtcNow, new JsonObject { ["UnitId"] = "unknown", ["Results"] = new JsonObject() })]);
            Reject(path);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            try
            {
                FeedDumpReplayLoader.CreateAsync(path, new TestClock(), canceled.Token).GetAwaiter().GetResult();
                throw new Exception("Canceled loads must not succeed");
            }
            catch (OperationCanceledException) { }
        }
        finally { File.Delete(path); }
    }

    public static void ActualWecCapture(string path)
    {
        var source = FeedDumpReplayLoader.CreateAsync(path, new TestClock()).GetAwaiter().GetResult();
        TimingSnapshot? latest = null;
        var seenEnergy = false;
        var seenTyres = false;
        var seenSectors = false;
        var seenFullCourseYellow = false;
        source.SnapshotReceived += snapshot =>
        {
            latest = snapshot;
            seenEnergy |= snapshot.Competitors.Any(value => value.VirtualEnergyPercent is >= 0);
            seenTyres |= snapshot.Competitors.Any(value => value.CurrentTyres is not null);
            seenSectors |= snapshot.Competitors.Any(value => value.CurrentSectors is not null);
            seenFullCourseYellow |= snapshot.TrackCondition == TrackCondition.FullCourseYellow;
        };
        try
        {
            source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(source.IsPaused && source.FrameCount > 1, "Capture must open paused with multiple frames");
            while (!source.IsCompleted) source.Step();
            Check(latest?.Championship == Championship.WorldEnduranceChampionship, "Capture must remain WEC");
            Check(latest?.Lifecycle == SessionLifecycle.Ended, "Capture must contain its recorded finish");
            Check(seenEnergy && seenTyres && seenSectors, "Capture must retain energy, tyre and sector data");
            Check(seenFullCourseYellow, "Capture must replay its recorded whole-track yellow as FCY");
            Check(latest!.Competitors.Count > 0, "Capture must retain competitors");
            Console.WriteLine($"PASS actual WEC capture: {source.FrameCount} frames, {latest.Competitors.Count} final competitors, ended");
        }
        finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    public static void ActualCapture(string path)
    {
        var source = FeedDumpReplayLoader.CreateAsync(path, new TestClock()).GetAwaiter().GetResult();
        TimingSnapshot? latest = null;
        var sawFullCourseYellow = false;
        source.SnapshotReceived += snapshot =>
        {
            latest = snapshot;
            sawFullCourseYellow |= snapshot.TrackCondition == TrackCondition.FullCourseYellow;
        };
        try
        {
            source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(source.IsPaused && source.FrameCount > 1, "Capture must open paused with multiple frames");
            while (!source.IsCompleted) source.Step();
            Check(latest?.Championship == Championship.GtWorldChallengeEurope, "Capture must remain GTWC");
            Check(latest?.Lifecycle == SessionLifecycle.Ended, "Capture must contain recorded session end");
            Check(sawFullCourseYellow, "Capture must replay recorded GTWC full-course yellow as FCY");
            Check(latest!.Competitors.Any(value => value.CurrentSectors is not null), "Capture must retain sector data");
            Console.WriteLine($"PASS actual GTWC capture: {source.FrameCount} frames, {latest.Competitors.Count} final competitors, ended");
        }
        finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    private static void Reject(string path)
    {
        try
        {
            var source = FeedDumpReplayLoader.CreateAsync(path, new TestClock()).GetAwaiter().GetResult();
            source.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw new Exception("An invalid capture was accepted");
        }
        catch (InvalidDataException) { }
    }

    private static string TemporaryPath() => Path.Combine(Path.GetTempPath(), $"RaceDock-Gtwc-{Guid.NewGuid():N}.ndjson");
    private static FeedDumpEntry Raw(DateTimeOffset timestamp, JsonNode payload) => new(timestamp, payload.ToJsonString());
    private static FeedDumpEntry Entry(DateTimeOffset timestamp, JsonObject full) => new(timestamp,
        new JsonObject { ["content"] = new JsonObject { ["full"] = full.DeepClone() } }.ToJsonString());
    private static void Write(string path, IEnumerable<FeedDumpEntry> entries) =>
        File.WriteAllLines(path, entries.Select(entry => JsonSerializer.Serialize(entry)));
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-10-18T12:00:00Z");
    }
}
