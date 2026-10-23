// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Nodes;
using RaceDock.Core;

/// <summary>
/// Exercises the headless session the Linux sidecar serves: source switching, command dispatch,
/// settings application, schedule refresh, and shutdown. Only deterministic sources are used, so the
/// suite stays offline and repeatable.
/// </summary>
internal static class SessionTests
{
    public static void ScenarioSessionPublishesLiveStateAndCommands()
    {
        using var context = new SessionContext();
        var session = context.CreateSession();
        try
        {
            StartScenario(session);

            True(session.TestMode);
            True(session.Scenario is not null);
            Equal("live", session.Widget.Mode);
            Equal("live", session.Flyout.Mode);
            True(session.Flyout.Rows.Count >= 5);
            Equal(5, session.Widget.Competitors.Count);
            Equal("NOR", session.Widget.Competitors[0].Code);

            True(Ok(Command(session, "pause")));
            True(session.Playback!.IsPaused);
            True(Ok(Command(session, "resume")));
            False(session.Playback!.IsPaused);
            True(Ok(Command(session, "speed", "2.5")));
            Equal(2.5, session.Playback!.Speed);
            True(Ok(Command(session, "step")));

            True(Ok(Command(session, "trigger", "Yellow")));
            // The alert is live immediately; the snapshot carrying the track state lands on the next render tick.
            Equal("yellow", session.Widget.Alert?.Kind);
            WaitFor(() => session.Widget.TrackCondition == "Yellow", TimeSpan.FromSeconds(5));
            WaitFor(() => session.Widget.Flag?.Kind == "yellow", TimeSpan.FromSeconds(5));
            Equal("YELLOW", session.Widget.Flag?.Label);

            // Unknown commands and arguments are rejected as data, never as exceptions on the wire.
            var unknownCommand = Command(session, "nonsense");
            False(Ok(unknownCommand));
            True(Error(unknownCommand).Contains("nonsense", StringComparison.Ordinal));
            False(Ok(Command(session, "trigger", "NotACommand")));
            False(Ok(Command(session, "speed", "fast")));
        }
        finally
        {
            Dispose(session);
        }
    }

    public static void ScheduleRefreshFeedsTheIdleState()
    {
        using var context = new SessionContext();
        var session = context.CreateSession();
        try
        {
            // No source is started: the widget has nothing on track and falls back to the calendar.
            True(Ok(Command(session, "refreshSchedule")));
            Equal("idle", session.Widget.Mode);
            True(session.Widget.Idle!.Headline.Contains("Stub", StringComparison.Ordinal));
            Equal("idle", session.Widget.Mode);
            Equal("schedule", session.Flyout.Mode);
            Equal(1, session.Flyout.Schedule.Count);
            True(context.Calendar.Calls > 0);

            // The schedule is only queried for championships the user enabled.
            True(context.Calendar.Championships.SequenceEqual([Championship.Formula1]));
        }
        finally
        {
            Dispose(session);
        }
    }

    public static void ApplyingSettingsKeepsTheReplaySource()
    {
        using var context = new SessionContext();
        var session = context.CreateSession();
        try
        {
            StartScenario(session);

            // Presentation-only changes never touch the running source.
            session.ApplySettingsAsync(session.Settings with { UseSingleRowTaskbar = true }).GetAwaiter().GetResult();
            True(session.TestMode);
            Equal("live", session.Widget.Mode);
            True(session.Settings.UseSingleRowTaskbar);

            // Feed-changing settings still leave a replay alone; only live timing is rebuilt.
            session.ApplySettingsAsync(session.Settings with { RotationSeconds = 7 }).GetAwaiter().GetResult();
            True(session.TestMode);
            Equal("live", session.Widget.Mode);
            Equal(7, new UserSettingsStore(context.SettingsPath).Load().RotationSeconds);

            // Hiding the championship that is on track clears it instead of leaving stale timing.
            session.ApplySettingsAsync(session.Settings with { HiddenChampionships = [Championship.Formula1] })
                .GetAwaiter().GetResult();
            True(session.Widget.Mode != "live");
            True(session.Flyout.Mode != "live");
        }
        finally
        {
            Dispose(session);
        }
    }

    public static void ResetSettingsRestoresDefaults()
    {
        using var context = new SessionContext();
        var session = context.CreateSession();
        try
        {
            session.ApplySettingsAsync(session.Settings with { RotationSeconds = 9 })
                .GetAwaiter().GetResult();
            Equal(9, session.Settings.RotationSeconds);
            Equal(9, new UserSettingsStore(context.SettingsPath).Load().RotationSeconds);

            var result = Command(session, "resetSettings");
            True(Ok(result));
            Equal(5, session.Settings.RotationSeconds);
            Equal(5, JsonNode.Parse(result)!["settings"]!["rotationSeconds"]!.GetValue<int>());
            // The reset is persisted like any other full settings snapshot.
            Equal(5, new UserSettingsStore(context.SettingsPath).Load().RotationSeconds);
        }
        finally
        {
            Dispose(session);
        }
    }

    public static void ArchiveReplayStepsThroughClassificationAndFlags()
    {
        using var context = new SessionContext();
        var path = Path.Combine(context.Directory, "synthetic-f1.ndjson");
        var start = DateTimeOffset.Parse("2026-10-18T12:00:00Z");
        var initial = """
            {"type":3,"result":{"SessionInfo":{"Name":"Race","Type":"Race","Meeting":{"Name":"Archive Grand Prix","Circuit":{"ShortName":"Archive"}}},"SessionStatus":{"Status":"Started"},"LapCount":{"CurrentLap":5,"TotalLaps":10},"TrackStatus":{"Status":"1"},"DriverList":{"1":{"Tla":"AAA","FullName":"Alpha Driver"},"2":{"Tla":"BBB","FullName":"Beta Driver"}},"TimingAppData":{"Lines":{"1":{"Stints":{"0":{"Compound":"SOFT","TotalLaps":3}}}}},"TimingData":{"Lines":{"1":{"Position":1,"NumberOfLaps":5,"LastLapTime":{"Value":"1:20.000"},"BestLapTime":{"Value":"1:20.000","Lap":5}},"2":{"Position":2,"NumberOfLaps":5,"GapToLeader":"+1.500","IntervalToPositionAhead":{"Value":"+1.500"},"LastLapTime":{"Value":"1:20.500"},"BestLapTime":{"Value":"1:20.500","Lap":5}}}}}}
            """;
        File.WriteAllLines(path,
        [
            JsonSerializer.Serialize(new FeedDumpEntry(start, initial)),
            JsonSerializer.Serialize(new FeedDumpEntry(start.AddSeconds(1),
                string.Join('\u001e',
                    FeedDelta("LapCount", """{"CurrentLap":10,"TotalLaps":10}"""),
                    FeedDelta("TimingData", """{"Lines":{"1":{"NumberOfLaps":10,"LastLapTime":{"Value":"1:19.500"},"BestLapTime":{"Value":"1:19.500","Lap":10}}}}""")))),
            JsonSerializer.Serialize(new FeedDumpEntry(start.AddSeconds(2),
                FeedDelta("RaceControlMessages", """{"Messages":{"1":{"Message":"CHEQUERED FLAG","Flag":"CHEQUERED","Lap":10}}}""")))
        ]);

        var session = context.CreateSession();
        try
        {
            var started = Task.Run(() => session.StartAsync(archivePath: path)).Wait(TimeSpan.FromSeconds(20));
            True(started);
            WaitFor(() => session.Widget.Mode == "live", TimeSpan.FromSeconds(10));

            Equal("F1", session.Widget.ChampionshipShort);
            Equal("Archive Grand Prix", session.Flyout.Event);
            Equal(2, session.Widget.Competitors.Count);
            Equal("AAA", session.Widget.Competitors[0].Code);
            Equal("+1.500", session.Widget.Competitors[1].Interval);
            Equal("S", session.Flyout.Rows[0].Tyre?.Letter);
            Equal(3, session.Flyout.Rows[0].Tyre?.Age);

            True(Ok(Command(session, "step")));
            Equal(10, session.Flyout.Rows[0].Lap);

            True(Ok(Command(session, "step")));
            Equal("chequered", session.Widget.Flag?.Kind);
            Equal("CHEQUERED", session.Widget.Flag?.Label);
            // A finished archive keeps its final classification on screen instead of emptying the flyout.
            Equal("live", session.Flyout.Mode);
            Equal(10, session.Flyout.Rows[0].Lap);
        }
        finally
        {
            Dispose(session);
        }
    }

    public static void ArchiveLoadFailureKeepsTheCurrentSource()
    {
        using var context = new SessionContext();
        var session = context.CreateSession();
        try
        {
            StartScenario(session);
            var missing = Path.Combine(context.Directory, "missing-archive.zip");

            var failed = false;
            try { session.LoadArchiveReplayAsync(missing).GetAwaiter().GetResult(); }
            catch (Exception exception) when (exception is IOException or ArgumentException or FileNotFoundException)
            {
                failed = true;
            }
            True(failed);
            True(session.TestMode);
            Equal("live", session.Widget.Mode);

            var command = Command(session, "archive", missing);
            False(Ok(command));
            True(Error(command).Length > 0);
            True(session.TestMode);
        }
        finally
        {
            Dispose(session);
        }
    }

    public static void StateChangedSubscribersRunOutsideTheSourceSwitch()
    {
        using var context = new SessionContext();
        var session = context.CreateSession();
        var restarted = 0;
        // A subscriber that calls back into the session must not deadlock the switch that raised it.
        session.StateChanged += () =>
        {
            if (Interlocked.Exchange(ref restarted, 1) == 0)
                session.RestartAsync().GetAwaiter().GetResult();
        };
        try
        {
            var started = Task.Run(() => session.StartAsync(scenario: true)).Wait(TimeSpan.FromSeconds(20));
            True(started);
            WaitFor(() => Volatile.Read(ref restarted) == 1, TimeSpan.FromSeconds(10));
            WaitFor(() => session.Widget.Mode == "live", TimeSpan.FromSeconds(10));
        }
        finally
        {
            Dispose(session);
        }
    }

    public static void CommandsAfterShutdownReturnErrorsInsteadOfThrowing()
    {
        using var context = new SessionContext();
        var session = context.CreateSession();
        StartScenario(session);
        Dispose(session);

        var command = Command(session, "refreshState");
        False(Ok(command));
        True(Error(command).Contains("shut down", StringComparison.OrdinalIgnoreCase));
    }

    private static string FeedDelta(string topic, string data) =>
        $"{{\"type\":1,\"target\":\"feed\",\"arguments\":[\"{topic}\",{data}]}}";

    private static void StartScenario(RaceDockSession session)
    {
        var started = Task.Run(() => session.StartAsync(scenario: true)).Wait(TimeSpan.FromSeconds(20));
        if (!started) throw new Exception("The scenario session did not start in time.");
        WaitFor(() => session.Widget.Mode == "live", TimeSpan.FromSeconds(10));
    }

    private static string Command(RaceDockSession session, string command, string? argument = null) =>
        session.CommandAsync(command, argument).GetAwaiter().GetResult();

    private static bool Ok(string json) =>
        JsonNode.Parse(json)?["ok"]?.GetValue<bool>() ?? false;

    private static string Error(string json) =>
        JsonNode.Parse(json)?["error"]?.GetValue<string>() ?? "";

    private static void WaitFor(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            Thread.Sleep(25);
        }
        if (!condition()) throw new Exception($"Condition was not met within {timeout}.");
    }

    private static void Dispose(RaceDockSession session) =>
        session.DisposeAsync().AsTask().GetAwaiter().GetResult();

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

    /// <summary>A throwaway settings file, a stub calendar, and one session per test.</summary>
    private sealed class SessionContext : IDisposable
    {
        public SessionContext()
        {
            Directory = Path.Combine(Path.GetTempPath(), "racedock-session-" + Guid.NewGuid().ToString("n"));
            System.IO.Directory.CreateDirectory(Directory);
            SettingsPath = Path.Combine(Directory, "settings.json");
            Calendar = new StubCalendar(Championship.Formula1);
            Schedule = new UpcomingScheduleService(
                [Calendar], new SystemClock(), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
        }

        public string Directory { get; }
        public string SettingsPath { get; }
        public StubCalendar Calendar { get; }
        public UpcomingScheduleService Schedule { get; }

        public RaceDockSession CreateSession() =>
            new(new UserSettingsStore(SettingsPath), new SystemClock(), Schedule, TimeZoneInfo.Utc);

        public void Dispose()
        {
            Schedule.Dispose();
            try { System.IO.Directory.Delete(Directory, recursive: true); } catch (IOException) { }
        }
    }

    private sealed class StubCalendar(params Championship[] championships) : IChampionshipCalendar
    {
        private int _calls;

        public IReadOnlyList<Championship> Championships { get; } = championships;
        public int Calls => Volatile.Read(ref _calls);

        public Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(
            DateTimeOffset now, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            IReadOnlyList<UpcomingSession> sessions =
                [new UpcomingSession(Championships[0], "Stub Grand Prix", "Race", now.AddHours(3), "Stub Ring")];
            return Task.FromResult(sessions);
        }
    }
}
