// SPDX-License-Identifier: GPL-3.0-or-later
using RaceDock.Core;
using RaceDock.Core.Presentation;
using RaceDock.Linux.Ipc;
using Tmds.DBus.Protocol;

namespace RaceDock.Linux;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ServiceOptions options;
        try
        {
            options = ServiceOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(ServiceOptions.HelpText);
            return 2;
        }

        // Feed dumps are ambient and auto-flush, but a clean shutdown should still close the writers
        // so a capture on disk is complete even for the print-state path.
        try { return await RunAsync(options); }
        finally { FeedDump.Stop(); }
    }

    private static async Task<int> RunAsync(ServiceOptions options)
    {
        if (options.ShowHelp)
        {
            Console.WriteLine(ServiceOptions.HelpText);
            return 0;
        }
        if (options.ShowVersion)
        {
            Console.WriteLine("RaceDock Linux service " + typeof(Program).Assembly.GetName().Version);
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(options.LogFolder))
        {
            Directory.CreateDirectory(options.LogFolder);
            AppLog.UseFolder(options.LogFolder);
        }

        if (!string.IsNullOrWhiteSpace(options.DumpFeedFolder))
        {
            Directory.CreateDirectory(options.DumpFeedFolder);
            AppLog.Info("Feed", $"Feed dump folder: {FeedDump.Start(options.DumpFeedFolder)}");
        }

        var timeZone = TimeZoneInfo.Local;
        if (!string.IsNullOrWhiteSpace(options.TimeZoneId))
        {
            try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId); }
            catch (TimeZoneNotFoundException)
            {
                Console.Error.WriteLine($"Unknown time zone '{options.TimeZoneId}'.");
                return 2;
            }
        }

        var settingsStore = new UserSettingsStore(options.SettingsPath);
        await using var session = new RaceDockSession(settingsStore, timeZone: timeZone);

        if (options.PrintState)
        {
            return await PrintStateAsync(session, options);
        }

        if (DBusAddress.Session is not { Length: > 0 } address)
        {
            Console.Error.WriteLine("No D-Bus session bus is available.");
            return 1;
        }

        using var connection = new DBusConnection(address);
        await connection.ConnectAsync();
        connection.AddMethodHandler(new RaceDockMethodHandler(session));

        if (!await connection.TryRequestNameAsync(RaceDockMethodHandler.ServiceName, RequestNameOptions.None))
        {
            Console.Error.WriteLine("Another RaceDock service already owns the session bus name.");
            return 3;
        }

        using var lifetime = new CancellationTokenSource();
        // ProcessExit (and SIGTERM) can arrive after Main has already disposed the source, so a late
        // cancel must never surface as an unhandled exception that looks like a crash to systemd.
        void Cancel() { try { lifetime.Cancel(); } catch (ObjectDisposedException) { } }
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Cancel();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Cancel();

        try
        {
            await session.StartAsync(options.Scenario, options.ArchivePath);
            if (options.ResumePlayback && (options.Scenario || !string.IsNullOrWhiteSpace(options.ArchivePath)))
                await session.CommandAsync("resume", null);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("RaceDock session failed to start: " + exception.Message);
            AppLog.Error("Session", "RaceDock session failed to start.", exception);
            return 4;
        }

        Console.WriteLine(
            $"RaceDock service listening on {RaceDockMethodHandler.ServiceName} " +
            $"({connection.UniqueName}); settings {settingsStore.Path}; log folder {AppLog.Folder}" +
            (FeedDump.Folder is { } dump ? $"; feed dump {dump}." : "."));

        var disconnected = connection.DisconnectedAsync();
        await Task.WhenAny(disconnected, Task.Delay(Timeout.Infinite, lifetime.Token));
        if (disconnected.IsCompletedSuccessfully && disconnected.Result is { } reason)
            Console.Error.WriteLine("D-Bus connection closed: " + reason.Message);

        return 0;
    }

    private static async Task<int> PrintStateAsync(RaceDockSession session, ServiceOptions options)
    {
        try
        {
            await session.StartAsync(options.Scenario, options.ArchivePath);
            if (options.ResumePlayback && (options.Scenario || !string.IsNullOrWhiteSpace(options.ArchivePath)))
                await session.CommandAsync("resume", null);
            // Wait for the source's first snapshot instead of sleeping for a fixed time, so CI cannot
            // race the first frame. A feed that is legitimately idle still prints at the ceiling.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(options.PrintDelaySeconds);
            while (session.Widget.Mode == "hidden" && DateTime.UtcNow < deadline)
                await Task.Delay(50);
            Console.WriteLine("WIDGET");
            Console.WriteLine(RaceDockJson.Serialize(session.Widget));
            Console.WriteLine("FLYOUT");
            Console.WriteLine(RaceDockJson.Serialize(session.Flyout));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("RaceDock session failed to start: " + exception.Message);
            return 4;
        }
    }

    private sealed record ServiceOptions(
        bool Scenario,
        string? ArchivePath,
        string? SettingsPath,
        string? LogFolder,
        string? DumpFeedFolder,
        string? TimeZoneId,
        bool ResumePlayback,
        bool PrintState,
        int PrintDelaySeconds,
        bool ShowHelp,
        bool ShowVersion)
    {
        public const string HelpText = """
            Usage: racedock-service [options]

              --scenario              Run the deterministic test scenario instead of live feeds.
              --archive <path>        Replay a recorded .ndjson feed or a .zip timing archive.
              --settings <path>       Use a specific settings.json instead of the XDG data path.
              --log-folder <path>     Write app.ndjson logs to a specific folder.
              --dump-feed <folder>    Record every feed payload into a timestamped .ndjson dump.
              --timezone <id>         Use an IANA time zone for the schedule (default: system local).
              --paused                Do not automatically start a scenario or archive replay.
              --print-state           Print one widget and flyout snapshot to stdout, then exit.
              --print-delay <seconds> Seconds to wait for the first snapshot before printing (default: 2).
              --help                  Show this help.
              --version               Show the service version.
            """;

        public static ServiceOptions Parse(string[] args)
        {
            var scenario = false;
            string? archive = null;
            string? settings = null;
            string? logs = null;
            string? dumps = null;
            string? timezone = null;
            var resume = true;
            var print = false;
            var delay = 2;
            var help = false;
            var version = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--scenario":
                    case "--test":
                        scenario = true;
                        break;
                    case "--archive":
                        archive = RequireValue(args, ref index, "--archive");
                        break;
                    case "--settings":
                        settings = RequireValue(args, ref index, "--settings");
                        break;
                    case "--log-folder":
                        logs = RequireValue(args, ref index, "--log-folder");
                        break;
                    case "--dump-feed":
                        dumps = RequireValue(args, ref index, "--dump-feed");
                        break;
                    case "--timezone":
                    case "--time-zone":
                        timezone = RequireValue(args, ref index, "--timezone");
                        break;
                    case "--paused":
                        resume = false;
                        break;
                    case "--print-state":
                        print = true;
                        break;
                    case "--print-delay":
                        var value = RequireValue(args, ref index, "--print-delay");
                        if (!int.TryParse(value, out delay) || delay < 0)
                            throw new ArgumentException("--print-delay must be a non-negative integer.");
                        break;
                    case "-h":
                    case "--help":
                        help = true;
                        break;
                    case "-v":
                    case "--version":
                        version = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{args[index]}'.");
                }
            }

            return new ServiceOptions(
                scenario, archive, settings, logs, dumps, timezone, resume, print, delay, help, version);
        }

        private static string RequireValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"{option} requires a value.");
            return args[++index];
        }
    }
}
