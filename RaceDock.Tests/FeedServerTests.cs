// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using RaceDock.Core;
using RaceDock.FeedServer;
using RaceDock.FeedServer.Recordings;

/// <summary>
/// Exercises the recorded-feed server through the real adapters: the server is only useful if the
/// production transports, not a test double, accept what it sends.
/// </summary>
internal static class FeedServerTests
{
    private static readonly DateTimeOffset Recorded = DateTimeOffset.Parse("2026-10-24T20:44:57Z");

    public static void CatalogSelectsLiveRecordings()
    {
        var folder = Directory.CreateTempSubdirectory("RaceDock-FeedCatalog-").FullName;
        try
        {
            var idle = Path.Combine(folder, "feed-dump-idle", "formula1.ndjson");
            Directory.CreateDirectory(Path.GetDirectoryName(idle)!);
            WriteDump(idle, [Raw(Recorded, new JsonObject { ["type"] = 6 })]);

            var live = Path.Combine(folder, "feed-dump-live", "formula1.ndjson");
            Directory.CreateDirectory(Path.GetDirectoryName(live)!);
            WriteDump(live, [Raw(Recorded, F1Initial())]);

            var catalog = DumpCatalog.Resolve(new FeedServerOptions { Root = folder });
            True(catalog.Dumps.TryGetValue("f1", out var chosen), "The live F1 recording must be selected.");
            Equal(Path.GetFullPath(live), chosen);

            var explicitDump = Path.Combine(folder, "explicit.ndjson");
            WriteDump(explicitDump, [Raw(Recorded, F1Initial())]);
            var requested = DumpCatalog.Resolve(new FeedServerOptions
            {
                Root = folder,
                Dumps = new Dictionary<string, string> { ["f1"] = explicitDump }
            });
            Equal(Path.GetFullPath(explicitDump), requested.Dumps["f1"]);

            True(!DumpCatalog.HasLiveFrames("nascar", explicitDump),
                "A recording without a NASCAR field must not pass the live check.");
        }
        finally { Directory.Delete(folder, true); }
    }

    public static void StreamAdvertisement()
    {
        var folder = Directory.CreateTempSubdirectory("RaceDock-FeedAdvert-").FullName;
        try
        {
            var dump = Path.Combine(folder, "formula1.ndjson");
            WriteDump(dump, [Raw(Recorded, F1Initial())]);
            var host = StartHost(("f1", dump));
            try
            {
                // This is the exact call the application makes before enabling a feed-server session.
                var ids = FeedServerCatalog.GetStreamIdsAsync(host.BaseAddress).GetAwaiter().GetResult();
                True(ids.Contains("f1"), "The application probe must read the advertised stream ids.");
                var championships = FeedServerCatalog.Championships(ids);
                True(championships.SetEquals([Championship.Formula1]),
                    "The advertised stream must enable exactly its own championship.");
            }
            finally { host.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        }
        finally { Directory.Delete(folder, true); }
    }

    public static void FeedServerFlag()
    {
        Equal("http://127.0.0.1:8099", LaunchArguments.FeedServerUrl(["--feed-server", "http://127.0.0.1:8099"]));
        Equal("http://localhost:9000/", LaunchArguments.FeedServerUrl(["--feed-server=http://localhost:9000/"]));
        True(LaunchArguments.FeedServerUrl(["--startup"]) is null);
        Equal("C:\\dumps\\f1.ndjson", LaunchArguments.ReplayPath(["--replay", "C:\\dumps\\f1.ndjson"]));
        Equal("http://localhost:1/", LaunchArguments.FeedServerUrl(
            ["--startup", "--feed-server", "\"http://localhost:1/\""]));
    }

    public static void F1Transport()
    {
        var folder = Directory.CreateTempSubdirectory("RaceDock-FeedF1-").FullName;
        try
        {
            var dump = Path.Combine(folder, "formula1.ndjson");
            WriteDump(dump,
            [
                // Deltas recorded before the initial state must not reach a client that never had that state.
                Raw(Recorded, F1Delta(2)),
                Raw(Recorded.AddSeconds(1), F1Initial()),
                Raw(Recorded.AddSeconds(2), F1Delta(3))
            ]);
            var host = StartHost(("f1", dump));
            try
            {
                var clock = new SystemClock();
                var processor = new TimingStateProcessor(clock, new RecordingAlerts());
                var endpoints = FeedEndpoints.ForServer(host.BaseAddress);
                var source = new F1LiveTimingSource(processor, clock, endpoints.Formula1WebSocket);
                ConcurrentQueue<TimingSnapshot> snapshots = new();
                source.SnapshotReceived += snapshots.Enqueue;
                try
                {
                    source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                    var snapshot = Await(snapshots,
                        value => value.Championship == Championship.Formula1 && value.Competitors.Count == 2,
                        "The F1 adapter must receive the recorded classification.");
                    True(snapshot.CurrentLap > 0, "The recorded lap count must reach the adapter.");
                }
                finally { source.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            }
            finally { host.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        }
        finally { Directory.Delete(folder, true); }
    }

    public static void HttpPollTransports()
    {
        var folder = Directory.CreateTempSubdirectory("RaceDock-FeedHttp-").FullName;
        try
        {
            var gtwc = Path.Combine(folder, "gtwc.ndjson");
            WriteGtwcDump(gtwc);
            var wec = Path.Combine(folder, "wec.ndjson");
            WriteWecDump(wec);
            var nascar = Path.Combine(folder, "nascar.ndjson");
            WriteNascarDump(nascar);

            var host = StartHost(("gtwc", gtwc), ("wec", wec), ("nascar", nascar));
            try
            {
                var clock = new SystemClock();
                var endpoints = FeedEndpoints.ForServer(host.BaseAddress);
                var gtwcSource = new GtwcLiveTimingSource(clock, new RecordingAlerts(), endpoints.GtwcBase);
                var wecSource = new WecLiveTimingSource(clock, new RecordingAlerts(), endpoints.GriiipBase);
                var nascarFeed = new NascarFeed(clock, apiBase: endpoints.NascarBase);
                var nascarSource = new NascarLiveTimingSource(nascarFeed, NascarSeries.Cup, clock, new RecordingAlerts());
                ConcurrentQueue<TimingSnapshot> gtwcSnapshots = new(), wecSnapshots = new(), nascarSnapshots = new();
                gtwcSource.SnapshotReceived += gtwcSnapshots.Enqueue;
                wecSource.SnapshotReceived += wecSnapshots.Enqueue;
                nascarSource.SnapshotReceived += nascarSnapshots.Enqueue;
                try
                {
                    gtwcSource.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                    wecSource.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                    nascarSource.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

                    Await(gtwcSnapshots,
                        value => value.Championship == Championship.GtWorldChallengeEurope && value.Competitors.Count > 0,
                        "The GTWC adapter must receive timing and detail from the same session.");
                    Await(wecSnapshots,
                        value => value.Championship == Championship.WorldEnduranceChampionship && value.Competitors.Count > 0,
                        "The WEC adapter must receive the recorded bootstrap.");
                    var nascarSnapshot = Await(nascarSnapshots,
                        value => value.Championship == Championship.NascarCup && value.Competitors.Count > 0,
                        "The NASCAR adapter must poll while the feed server's calendar says a race is running.");
                    True(nascarSnapshot.TotalLaps > 0, "The recorded NASCAR race distance must reach the adapter.");
                }
                finally
                {
                    gtwcSource.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    wecSource.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    nascarSource.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    nascarFeed.Dispose();
                }
            }
            finally { host.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        }
        finally { Directory.Delete(folder, true); }
    }

    public static void ConcurrentStreams()
    {
        var folder = Directory.CreateTempSubdirectory("RaceDock-FeedAll-").FullName;
        try
        {
            var f1 = Path.Combine(folder, "formula1.ndjson");
            WriteDump(f1, [Raw(Recorded, F1Initial()), Raw(Recorded.AddSeconds(1), F1Delta(3))]);
            var fia = Path.Combine(folder, "f2.ndjson");
            WriteDump(fia, FiaFrames());
            var gtwc = Path.Combine(folder, "gtwc.ndjson");
            WriteGtwcDump(gtwc);
            var wec = Path.Combine(folder, "wec.ndjson");
            WriteWecDump(wec);
            var nascar = Path.Combine(folder, "nascar.ndjson");
            WriteNascarDump(nascar);

            var host = StartHost(("f1", f1), ("f2", fia), ("gtwc", gtwc), ("wec", wec), ("nascar", nascar));
            try
            {
                Equal(5, host.Streams.Count);
                var ids = host.Streams.Select(stream => stream.Id).ToHashSet();
                True(ids.SetEquals(["f1", "f2", "gtwc", "wec", "nascar"]),
                    "Every configured stream must be advertised to the application.");

                var clock = new SystemClock();
                var endpoints = FeedEndpoints.ForServer(host.BaseAddress);
                var f1Source = new F1LiveTimingSource(new TimingStateProcessor(clock, new RecordingAlerts()), clock,
                    endpoints.Formula1WebSocket);
                var fiaSource = new F2LiveTimingSource(clock, new RecordingAlerts(), "F2", endpoints.Formula2Base);
                var gtwcSource = new GtwcLiveTimingSource(clock, new RecordingAlerts(), endpoints.GtwcBase);
                var wecSource = new WecLiveTimingSource(clock, new RecordingAlerts(), endpoints.GriiipBase);
                var nascarFeed = new NascarFeed(clock, apiBase: endpoints.NascarBase);
                var nascarSource = new NascarLiveTimingSource(nascarFeed, NascarSeries.Cup, clock, new RecordingAlerts());
                ConcurrentQueue<TimingSnapshot> snapshots = new();
                f1Source.SnapshotReceived += snapshots.Enqueue;
                fiaSource.SnapshotReceived += snapshots.Enqueue;
                gtwcSource.SnapshotReceived += snapshots.Enqueue;
                wecSource.SnapshotReceived += snapshots.Enqueue;
                nascarSource.SnapshotReceived += snapshots.Enqueue;
                try
                {
                    foreach (var source in new ILiveTimingSource[] { f1Source, fiaSource, gtwcSource, wecSource, nascarSource })
                        source.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

                    // Each stream runs its own cursor, so every championship must publish without waiting for
                    // another one to finish; this is the multi-stream requirement in one assertion.
                    Await(snapshots, value => value.Championship == Championship.Formula1 && value.Competitors.Count > 0,
                        "F1 must publish while the other streams run.");
                    Await(snapshots, value => value.Championship == Championship.Formula2 && value.Competitors.Count > 0,
                        "Formula 2 must publish while the other streams run.");
                    Await(snapshots, value => value.Championship == Championship.GtWorldChallengeEurope && value.Competitors.Count > 0,
                        "GTWC must publish while the other streams run.");
                    Await(snapshots, value => value.Championship == Championship.WorldEnduranceChampionship && value.Competitors.Count > 0,
                        "WEC must publish while the other streams run.");
                    Await(snapshots, value => value.Championship == Championship.NascarCup && value.Competitors.Count > 0,
                        "NASCAR must publish while the other streams run.");
                }
                finally
                {
                    foreach (var source in new ILiveTimingSource[] { f1Source, fiaSource, gtwcSource, wecSource, nascarSource })
                        source.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    nascarFeed.Dispose();
                }
            }
            finally { host.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        }
        finally { Directory.Delete(folder, true); }
    }

    private static FeedServerHost StartHost(params (string Feed, string Path)[] dumps)
    {
        var host = FeedServerHost.Create(new FeedServerOptions
        {
            Port = 0,
            Speed = 200,
            Loop = true,
            Dumps = dumps.ToDictionary(pair => pair.Feed, pair => pair.Path, StringComparer.OrdinalIgnoreCase)
        });
        host.StartAsync().GetAwaiter().GetResult();
        return host;
    }

    private static TimingSnapshot Await(ConcurrentQueue<TimingSnapshot> snapshots,
        Func<TimingSnapshot, bool> predicate, string message, int seconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            var match = snapshots.FirstOrDefault(predicate);
            if (match is not null) return match;
            Thread.Sleep(50);
        }
        var observed = string.Join(" | ", snapshots.Select(snapshot =>
            $"{snapshot.Championship}:{snapshot.Lifecycle}:{snapshot.Competitors.Count}"));
        throw new Exception($"{message} Observed: {observed}");
    }

    private static void WriteDump(string path, IEnumerable<FeedDumpEntry> entries) =>
        File.WriteAllLines(path, entries.Select(entry => JsonSerializer.Serialize(entry)));

    private static FeedDumpEntry Raw(DateTimeOffset timestamp, JsonNode payload) =>
        new(timestamp, payload.ToJsonString());

    /// <summary>The GTWC cache answers whole bodies; the server classifies them by their full snapshot shape.</summary>
    private static FeedDumpEntry GtwcEntry(DateTimeOffset timestamp, JsonObject full) => new(timestamp,
        new JsonObject { ["content"] = new JsonObject { ["full"] = full.DeepClone() } }.ToJsonString());

    private static JsonObject F1Initial() => new()
    {
        ["type"] = 3,
        ["invocationId"] = "1",
        ["result"] = new JsonObject
        {
            ["SessionInfo"] = new JsonObject
            {
                ["Name"] = "Race",
                ["Meeting"] = new JsonObject { ["Name"] = "Test Grand Prix", ["Circuit"] = new JsonObject { ["ShortName"] = "Ring" } }
            },
            ["TrackStatus"] = new JsonObject { ["Status"] = "1" },
            ["SessionData"] = new JsonObject { ["StatusSeries"] = new JsonObject { ["1"] = new JsonObject { ["SessionStatus"] = "Started" } } },
            ["LapCount"] = new JsonObject { ["CurrentLap"] = 1, ["TotalLaps"] = 20 },
            ["DriverList"] = new JsonObject
            {
                ["1"] = new JsonObject { ["Tla"] = "AAA" },
                ["2"] = new JsonObject { ["Tla"] = "BBB" }
            },
            ["TimingData"] = new JsonObject
            {
                ["Lines"] = new JsonObject
                {
                    ["1"] = new JsonObject { ["Position"] = "1", ["NumberOfLaps"] = 1, ["LastLapTime"] = new JsonObject { ["Value"] = "1:30.000" } },
                    ["2"] = new JsonObject { ["Position"] = "2", ["NumberOfLaps"] = 1, ["GapToLeader"] = "+1.000", ["LastLapTime"] = new JsonObject { ["Value"] = "1:31.000" } }
                }
            }
        }
    };

    private static JsonObject F1Delta(int lap) => new()
    {
        ["type"] = 1,
        ["target"] = "feed",
        ["arguments"] = new JsonArray("TimingData", new JsonObject
        {
            ["Lines"] = new JsonObject
            {
                ["1"] = new JsonObject { ["NumberOfLaps"] = lap },
                ["2"] = new JsonObject { ["NumberOfLaps"] = lap }
            }
        })
    };

    private static IEnumerable<FeedDumpEntry> FiaFrames()
    {
        static JsonObject Line(string number, int position, string code) => new()
        {
            ["position"] = new JsonObject { ["Value"] = position.ToString() },
            ["driver"] = new JsonObject { ["RacingNumber"] = number, ["FullName"] = code, ["TLA"] = code },
            ["laps"] = new JsonObject { ["Value"] = "3" }
        };
        yield return Raw(Recorded, new JsonObject { ["C"] = "s-0", ["S"] = 1, ["M"] = new JsonArray() });
        yield return Raw(Recorded.AddMilliseconds(10), new JsonObject());
        yield return Raw(Recorded.AddMilliseconds(20), new JsonObject { ["I"] = "0" });
        yield return Raw(Recorded.AddMilliseconds(30), new JsonObject
        {
            ["R"] = new JsonObject
            {
                ["data"] = new JsonArray(Recorded.ToString("O"), new JsonObject { ["Series"] = "F2", ["Session"] = "Race" },
                    new JsonObject { ["1"] = Line("1", 1, "ONE"), ["2"] = Line("2", 2, "TWO") }),
                ["racedetailsfeed"] = new JsonArray(Recorded.ToString("O"), new JsonObject { ["Race"] = "Test", ["Session"] = "Race" }),
                ["sessionfeed"] = new JsonArray(Recorded.ToString("O"), new JsonObject { ["Value"] = "Started" })
            }
        });
        yield return Raw(Recorded.AddSeconds(1), new JsonObject
        {
            ["M"] = new JsonArray(new JsonObject
            {
                ["H"] = "Streaming",
                ["M"] = "datafeed",
                ["A"] = new JsonArray(Recorded.AddSeconds(1).ToString("O"), 0, new JsonObject
                {
                    ["lines"] = new JsonArray(new JsonObject
                    {
                        ["Number"] = "1",
                        ["driver"] = new JsonObject { ["RacingNumber"] = "1" },
                        ["position"] = new JsonObject { ["Value"] = "1" }
                    })
                })
            })
        });
    }

    private static void WriteGtwcDump(string path)
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Sectors", "gtwc-portimao-fp1.json")))!.AsObject();
        var timing = fixture["timing"]!.AsObject();
        var detail = fixture["detail"]!.AsObject();
        var unitId = timing["UnitId"]!.GetValue<string>();
        var seasons = new JsonObject { ["CurrentSeason"] = "2026" };
        var season = new JsonObject
        {
            ["PresentationMeetingId"] = "meeting",
            ["Meetings"] = new JsonObject
            {
                ["meeting"] = new JsonObject { ["TrackName"] = "Portimao", ["State"] = 1 }
            }
        };
        var schedule = new JsonObject
        {
            ["MeetingId"] = "meeting",
            ["PresentationRoundId"] = unitId,
            ["Series"] = new JsonObject { ["gt"] = new JsonObject { ["Name"] = "GTWorldChEU pwrd by AWS Endurance Cup" } },
            ["Competitions"] = new JsonObject { ["competition"] = new JsonObject { ["SerieId"] = "gt" } },
            ["Units"] = new JsonObject
            {
                [unitId] = new JsonObject { ["Name"] = "Free Practice 1", ["CompetitionId"] = "competition", ["State"] = 1 }
            }
        };
        WriteDump(path,
        [
            GtwcEntry(Recorded, seasons),
            GtwcEntry(Recorded.AddMilliseconds(10), season),
            GtwcEntry(Recorded.AddMilliseconds(20), schedule),
            GtwcEntry(Recorded.AddMilliseconds(30), timing),
            GtwcEntry(Recorded.AddMilliseconds(40), detail),
            GtwcEntry(Recorded.AddSeconds(1), timing),
            GtwcEntry(Recorded.AddSeconds(1.05), detail)
        ]);
    }

    private static void WriteWecDump(string path)
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Sectors", "wec-replay.json")))!.AsObject();
        var discovery = fixture["discovery"]!;
        var bootstrap = fixture["frames"]![0]!["bootstrap"]!;
        WriteDump(path,
        [
            Raw(Recorded, new JsonArray(discovery.DeepClone())),
            Raw(Recorded.AddMilliseconds(50), bootstrap.DeepClone()),
            Raw(Recorded.AddSeconds(1), bootstrap.DeepClone())
        ]);
    }

    private static void WriteNascarDump(string path)
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Nascar", "recorded-transitions.json")))!.AsObject();
        var payload = fixture["race"]![0]!["payload"]!.DeepClone().AsObject();
        // The shared fixture carries an Xfinity field; the Cup adapter needs the Cup series id, and the feed
        // server advertises the recorded series to its synthesized calendar.
        payload["series_id"] = 1;
        WriteDump(path,
        [
            Raw(Recorded, payload),
            Raw(Recorded.AddSeconds(1), payload.DeepClone())
        ]);
    }

    private static void True(bool value, string message = "Expected true.")
    {
        if (!value) throw new Exception(message);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected '{expected}', got '{actual}'.");
    }
}
