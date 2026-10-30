// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using RaceDock.Core;

namespace RaceDock.FeedServer.Recordings;

/// <summary>One recorded payload and the moment it arrived.</summary>
internal sealed record RecordedEntry(DateTimeOffset Timestamp, string Payload);

/// <summary>Reads RaceDock feed dumps. The payload is unescaped by the deserializer, so callers match plain JSON.</summary>
internal static class RecordedDump
{
    private const long MaximumBytes = 1024L * 1024 * 1024;

    public static IReadOnlyList<RecordedEntry> Read(string path, CancellationToken cancellationToken = default)
    {
        var entries = new List<RecordedEntry>();
        foreach (var line in File.ReadLines(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = ParseLine(line, path);
            if (entry is { } recorded) entries.Add(recorded);
        }
        return entries;
    }

    /// <summary>Yields payloads without keeping them, for the cheap "does this dump contain a session?" check.</summary>
    public static IEnumerable<string> Payloads(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var entry = ParseLine(line, path);
            if (entry is { } recorded) yield return recorded.Payload;
        }
    }

    private static RecordedEntry? ParseLine(string line, string path)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        FeedDumpEntry entry;
        try { entry = JsonSerializer.Deserialize<FeedDumpEntry>(line); }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The feed dump '{Path.GetFileName(path)}' contains malformed NDJSON.", exception);
        }
        return string.IsNullOrWhiteSpace(entry.Payload) ? null : new(entry.Timestamp, entry.Payload);
    }
}
