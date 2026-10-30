// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Nodes;
namespace RaceDock.FeedServer.Recordings;

/// <summary>
/// Picks the dump every stream replays: explicit <c>--dump</c> assignments first, then the newest recording
/// under <c>--root</c> that actually contains a running session rather than an idle poll loop.
/// </summary>
internal sealed class DumpCatalog
{
    private DumpCatalog(IReadOnlyDictionary<string, string> dumps) => Dumps = dumps;

    public IReadOnlyDictionary<string, string> Dumps { get; }

    public static DumpCatalog Resolve(FeedServerOptions options, Action<string>? log = null, CancellationToken cancellationToken = default)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (feed, path) in options.Dumps)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException($"The {feed} feed dump was not found.", fullPath);
            resolved[feed] = fullPath;
            log?.Invoke($"{feed}: using requested dump {fullPath}");
        }

        if (string.IsNullOrWhiteSpace(options.Root))
        {
            return new(resolved);
        }

        var root = Path.GetFullPath(options.Root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"The feed-dump root '{root}' does not exist.");

        var candidates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(root, "*.ndjson", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var feed = EmulatedFeed.FromFileName(file);
            if (feed is null || resolved.ContainsKey(feed)) continue;
            if (!candidates.TryGetValue(feed, out var files)) candidates[feed] = files = [];
            files.Add(file);
        }

        foreach (var (feed, files) in candidates.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in files.OrderByDescending(File.GetLastWriteTimeUtc))
            {
                if (!HasLiveFrames(feed, file, cancellationToken))
                {
                    log?.Invoke($"{feed}: skipping {file} (no live session recorded)");
                    continue;
                }
                resolved[feed] = file;
                log?.Invoke($"{feed}: using {file}");
                break;
            }
        }
        return new(resolved);
    }

    /// <summary>
    /// Whether a recording has frames a provider would only send during a session. Idle discovery loops are
    /// valid dumps but would leave the application showing nothing, so they are not selected automatically.
    /// </summary>
    public static bool HasLiveFrames(string feed, string path, CancellationToken cancellationToken = default)
    {
        var sawTiming = false;
        var sawDetail = false;
        foreach (var payload in RecordedDump.Payloads(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonNode? node;
            try { node = JsonNode.Parse(payload); }
            catch (JsonException)
            {
                // F1 SignalR messages carry a trailing record separator.
                var first = payload.Split('\u001e', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                try { node = first is null ? null : JsonNode.Parse(first); }
                catch (JsonException) { continue; }
            }
            if (node is null) continue;
            switch (feed)
            {
                case EmulatedFeed.Formula1:
                    if (node is JsonObject root && root["type"]?.GetValue<int>() == 3) return true;
                    break;
                case EmulatedFeed.Formula2 or EmulatedFeed.Formula3 or EmulatedFeed.F1Academy:
                    if (node is JsonObject fia && fia["R"]?["data"] is JsonArray) return true;
                    break;
                case EmulatedFeed.Gtwc:
                    if (node is JsonObject gtwc && gtwc["content"]?["full"] is JsonObject full)
                    {
                        if (full.ContainsKey("Results")) sawTiming = true;
                        if (full.ContainsKey("Competitors")) sawDetail = true;
                        if (sawTiming && sawDetail) return true;
                    }
                    break;
                case EmulatedFeed.Wec:
                    if (node is JsonObject wec && wec["sessionInfo"] is not null) return true;
                    break;
                case EmulatedFeed.Nascar:
                    if (node is JsonObject nascar && nascar["vehicles"] is JsonArray { Count: > 0 }) return true;
                    break;
            }
        }
        return false;
    }
}
