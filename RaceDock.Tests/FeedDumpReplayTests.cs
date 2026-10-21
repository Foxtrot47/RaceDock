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
        source.SnapshotReceived += snapshot =>
        {
            latest = snapshot;
            seenEnergy |= snapshot.Competitors.Any(value => value.VirtualEnergyPercent is >= 0);
            seenTyres |= snapshot.Competitors.Any(value => value.CurrentTyres is not null);
            seenSectors |= snapshot.Competitors.Any(value => value.CurrentSectors is not null);
        };
        try
        {
            source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(source.IsPaused && source.FrameCount > 1, "Capture must open paused with multiple frames");
            while (!source.IsCompleted) source.Step();
            Check(latest?.Championship == Championship.WorldEnduranceChampionship, "Capture must remain WEC");
            Check(latest?.Lifecycle == SessionLifecycle.Ended, "Capture must contain its recorded finish");
            Check(seenEnergy && seenTyres && seenSectors, "Capture must retain energy, tyre and sector data");
            Check(latest!.Competitors.Count > 0, "Capture must retain competitors");
            Console.WriteLine($"PASS actual WEC capture: {source.FrameCount} frames, {latest.Competitors.Count} final competitors, ended");
        }
        finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    public static void ActualCapture(string path)
    {
        var source = FeedDumpReplayLoader.CreateAsync(path, new TestClock()).GetAwaiter().GetResult();
        TimingSnapshot? latest = null;
        source.SnapshotReceived += snapshot => latest = snapshot;
        try
        {
            source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(source.IsPaused && source.FrameCount > 1, "Capture must open paused with multiple frames");
            while (!source.IsCompleted) source.Step();
            Check(latest?.Championship == Championship.GtWorldChallengeEurope, "Capture must remain GTWC");
            Check(latest?.Lifecycle == SessionLifecycle.Ended, "Capture must contain recorded session end");
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
