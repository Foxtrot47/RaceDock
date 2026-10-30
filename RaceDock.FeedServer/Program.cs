// SPDX-License-Identifier: GPL-3.0-or-later
using RaceDock.FeedServer;

var options = FeedServerOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("RaceDock.FeedServer [--root <folder>] [--dump <feed>=<path>]... [--port <n>] [--speed <x>] [--once] [--verbatim]");
    Console.WriteLine();
    Console.WriteLine("Serves recorded RaceDock feed dumps as the provider endpoints the app connects to.");
    Console.WriteLine("Feeds: f1, f2, f3, f1academy, gtwc, wec, nascar");
    Console.WriteLine();
    Console.WriteLine("  --root      folder searched for feed-dump-* recordings (newest live dump per feed)");
    Console.WriteLine("  --dump      explicit recording for one feed; repeatable and wins over --root");
    Console.WriteLine("  --port      listener port (default 8099; 0 picks a free port)");
    Console.WriteLine("  --speed     playback speed relative to the recording (default 4)");
    Console.WriteLine("  --once      play each stream once instead of looping");
    Console.WriteLine("  --verbatim  serve NASCAR payloads unchanged instead of restamping their assembly time");
    return;
}

try
{
    await using var host = FeedServerHost.Create(options, Console.WriteLine);
    await host.StartAsync();
    Console.WriteLine($"RaceDock feed server listening on {host.BaseAddress}");
    foreach (var stream in host.Streams)
        Console.WriteLine($"  {stream.Id,-10} {stream.Championship,-27} {stream.Frames,8:N0} frames  {stream.Dump}");
    Console.WriteLine("Press Ctrl+C to stop.");

    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
    try { await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token); }
    catch (OperationCanceledException) { }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}
