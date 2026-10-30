// SPDX-License-Identifier: GPL-3.0-or-later
using RaceDock.FeedServer.Protocols;
using RaceDock.FeedServer.Recordings;
using RaceDock.FeedServer.Streams;

namespace RaceDock.FeedServer;

/// <summary>Turns resolved dumps into the provider endpoint that knows how to serve each one.</summary>
internal static class EndpointFactory
{
    public static IReadOnlyList<IFeedEndpoint> Create(
        IReadOnlyDictionary<string, string> dumps, FeedServerOptions options, Action<string>? log = null)
    {
        var endpoints = new List<IFeedEndpoint>();
        foreach (var (feed, path) in dumps.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var entries = RecordedDump.Read(path);
            if (entries.Count == 0)
            {
                log?.Invoke($"{feed}: '{path}' contains no payloads");
                continue;
            }
            endpoints.Add(feed switch
            {
                EmulatedFeed.Formula1 => new F1FeedEndpoint(path, entries, options.Speed, options.Loop),
                EmulatedFeed.Formula2 or EmulatedFeed.Formula3 or EmulatedFeed.F1Academy =>
                    new FiaFeedEndpoint(feed, path, entries, options.Speed, options.Loop),
                EmulatedFeed.Gtwc => new GtwcFeedEndpoint(path, entries, options.Speed, options.Loop),
                EmulatedFeed.Wec => new GriiipFeedEndpoint(path, entries, options.Speed, options.Loop),
                EmulatedFeed.Nascar => new NascarFeedEndpoint(path, entries, options.Speed, options.Loop, options.RetimeNascar),
                _ => throw new InvalidOperationException($"Unknown feed '{feed}'.")
            });
            log?.Invoke($"{feed}: {entries.Count:N0} recorded payloads");
        }
        return endpoints;
    }
}
