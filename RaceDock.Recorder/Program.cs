// SPDX-License-Identifier: GPL-3.0-or-later
using RaceDock.Core;

var options = RecorderOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("RaceDock.Recorder [--output <folder>]");
    Console.WriteLine();
    Console.WriteLine("Starts every RaceDock live adapter and records raw feed payloads until Ctrl+C.");
    return;
}

var outputRoot = Path.GetFullPath(options.OutputRoot);
Directory.CreateDirectory(outputRoot);
var dumpFolder = FeedDump.Start(outputRoot);
AppLog.UseFolder(dumpFolder);

var clock = new SystemClock();
var plan = RecorderPlan.Create(clock);
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stop.Cancel();
};

Console.WriteLine($"Recording RaceDock feeds to {dumpFolder}");
Console.WriteLine($"Adapters: {plan.Sources.Count}");
Console.WriteLine("Press Ctrl+C to stop.");

try
{
    foreach (var entry in plan.Sources) Attach(entry);
    foreach (var entry in plan.Sources) await StartAsync(entry, stop.Token);
    await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token);
}
catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
finally
{
    Console.WriteLine("Stopping adapters...");
    foreach (var entry in plan.Sources) await StopAsync(entry);
    plan.NascarFeed.Dispose();
    FeedDump.Stop();
    AppLog.Close();
    Console.WriteLine($"Recording stopped. Files are in {dumpFolder}");
}

static void Attach(RecorderEntry entry)
{
    entry.Source.ConnectionChanged += state =>
    {
        WriteLine($"[{entry.Name}] connection={state}");
        AppLog.Info(entry.Name, $"Connection changed to {state}.");
    };
    entry.Source.Failed += failure =>
    {
        WriteLine($"[{entry.Name}] ERROR {failure.Message}");
        AppLog.Error(entry.Name, failure.Message, failure.Exception);
    };

    var gate = new object();
    string? lastSession = null;
    entry.Source.SnapshotReceived += snapshot =>
    {
        var session = $"{snapshot.Lifecycle}|{snapshot.Championship}|{snapshot.Meeting}|{snapshot.Session}";
        lock (gate)
        {
            if (session == lastSession) return;
            lastSession = session;
        }
        WriteLine($"[{entry.Name}] {snapshot.Lifecycle}: {snapshot.Meeting} / {snapshot.Session}");
        AppLog.Info(entry.Name, $"Session changed to {snapshot.Lifecycle}: {snapshot.Meeting} / {snapshot.Session}.");
    };
}

static async Task StartAsync(RecorderEntry entry, CancellationToken cancellationToken)
{
    try
    {
        await entry.Source.StartAsync(cancellationToken);
        WriteLine($"[{entry.Name}] started");
    }
    catch (Exception exception)
    {
        WriteLine($"[{entry.Name}] could not start: {exception.Message}");
        AppLog.Error(entry.Name, "Adapter could not start.", exception);
    }
}

static async Task StopAsync(RecorderEntry entry)
{
    try { await entry.Source.DisposeAsync(); }
    catch (Exception exception)
    {
        WriteLine($"[{entry.Name}] could not stop: {exception.Message}");
        AppLog.Error(entry.Name, "Adapter could not stop cleanly.", exception);
    }
}

static void WriteLine(string message)
{
    lock (typeof(Program)) Console.WriteLine($"{DateTimeOffset.UtcNow:O} {message}");
}

sealed record RecorderEntry(string Name, ILiveTimingSource Source);

sealed record RecorderPlan(IReadOnlyList<RecorderEntry> Sources, NascarFeed NascarFeed)
{
    public static RecorderPlan Create(IClock clock)
    {
        var entries = new List<RecorderEntry>
        {
            Entry("formula1", () => new F1LiveTimingSource(new TimingStateProcessor(clock, Alerts(clock)), clock)),
            Entry("f2", () => new F2LiveTimingSource(clock, Alerts(clock), "F2")),
            Entry("f3", () => new F2LiveTimingSource(clock, Alerts(clock), "F3")),
            Entry("motogp", () => new MotoGpLiveTimingSource(clock, Alerts(clock), Championship.MotoGp)),
            Entry("moto2", () => new MotoGpLiveTimingSource(clock, Alerts(clock), Championship.Moto2)),
            Entry("moto3", () => new MotoGpLiveTimingSource(clock, Alerts(clock), Championship.Moto3)),
            Entry("gtwc", () => new GtwcLiveTimingSource(clock, Alerts(clock))),
            Entry("wec", () => new WecLiveTimingSource(clock, Alerts(clock))),
            Entry("elms", () => new ElmsLiveTimingSource(clock, Alerts(clock))),
            Entry("mlmc", () => new MichelinLeMansCupLiveTimingSource(clock, Alerts(clock))),
            Entry("wrc", () => new WrcLiveTimingSource(clock, Alerts(clock))),
            Entry("imsa", () => new ImsaLiveTimingSource(clock, Alerts(clock))),
            Entry("formulae", () => new FormulaELiveTimingSource(clock, Alerts(clock))),
            Entry("dtm", () => new DtmLiveTimingSource(clock, Alerts(clock))),
            Entry("indycar", () => new IndyCarLiveTimingSource(clock, Alerts(clock)))
        };

        var nascarFeed = new NascarFeed(clock);
        entries.Add(Entry("nascar-cup", () => new NascarLiveTimingSource(nascarFeed, NascarSeries.Cup, clock, Alerts(clock))));
        entries.Add(Entry("nascar-xfinity", () => new NascarLiveTimingSource(nascarFeed, NascarSeries.Xfinity, clock, Alerts(clock))));
        entries.Add(Entry("nascar-trucks", () => new NascarLiveTimingSource(nascarFeed, NascarSeries.Trucks, clock, Alerts(clock))));
        return new(entries, nascarFeed);
    }

    private static RecorderEntry Entry(string name, Func<ILiveTimingSource> factory) => new(name, factory());
    private static IAlertArbiter Alerts(IClock clock) => new AlertArbiter(clock);
}

sealed record RecorderOptions(string OutputRoot, bool ShowHelp)
{
    public static RecorderOptions Parse(string[] args)
    {
        var defaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "racedock-recordings");
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--help", StringComparison.OrdinalIgnoreCase) || args[index] == "-h")
                return new(defaultRoot, true);
            if (string.Equals(args[index], "--output", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                return new(args[index + 1], false);
            if (args[index].StartsWith("--output=", StringComparison.OrdinalIgnoreCase))
                return new(args[index]["--output=".Length..], false);
        }
        return new(defaultRoot, false);
    }
}
