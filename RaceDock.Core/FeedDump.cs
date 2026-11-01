// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;

namespace RaceDock.Core;

/// <summary>
/// Captures the payloads live sources receive, verbatim, one NDJSON file per source.
/// </summary>
/// <remarks>
/// A dump is a diagnostic aid rather than part of timing, so it is ambient: sources call
/// <see cref="Record"/> wherever the payload already exists, instead of threading a sink through
/// every constructor. A failed write ends the dump rather than throwing into a live feed loop.
/// </remarks>
public static class FeedDump
{
    private static readonly Dictionary<string, StreamWriter> Writers = [];
    private static readonly Dictionary<string, string> InitialStates = [];
    private static readonly object Gate = new();
    private static string? _folder;

    /// <summary>The folder currently being written to, or <c>null</c> when no dump is running.</summary>
    public static string? Folder => _folder;

    /// <summary>
    /// Begins a dump in a timestamped folder beneath <paramref name="root"/> and returns it.
    /// </summary>
    public static string Start(string root)
    {
        lock (Gate)
        {
            StopCore();
            var folder = Path.Combine(root, $"feed-dump-{DateTime.Now:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(folder);
            _folder = folder;
            // Seeded first: a source connected before the recording began never sends its initial state again.
            var started = DateTimeOffset.UtcNow;
            foreach (var (source, payload) in InitialStates) Write(source, new FeedDumpEntry(started, payload));
            return folder;
        }
    }

    public static void Stop()
    {
        lock (Gate) StopCore();
    }

    /// <summary>Appends one payload as received. Ignored unless a dump is running.</summary>
    /// <param name="source">Names the file, so each feed is readable on its own.</param>
    public static void Record(string source, string payload)
    {
        if (_folder is null) return;
        lock (Gate)
        {
            if (_folder is null) return;
            Write(source, new FeedDumpEntry(DateTimeOffset.UtcNow, payload));
        }
    }

    /// <summary>Keeps the state a source received when it connected, so a later recording can open with it.</summary>
    public static void RememberInitial(string source, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;
        lock (Gate) InitialStates[source] = payload;
    }

    /// <summary>Appends one entry. Callers hold <see cref="Gate"/>.</summary>
    private static void Write(string source, FeedDumpEntry entry)
    {
        if (_folder is null) return;
        try
        {
            // Shared for reading so a dump can be tailed while the session is still running.
            if (!Writers.TryGetValue(source, out var writer))
                Writers[source] = writer = new StreamWriter(
                    new FileStream(Path.Combine(_folder, $"{source}.ndjson"), FileMode.Append, FileAccess.Write, FileShare.Read))
                { AutoFlush = true };
            writer.WriteLine(JsonSerializer.Serialize(entry));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StopCore();
        }
    }

    private static void StopCore()
    {
        foreach (var writer in Writers.Values) writer.Dispose();
        Writers.Clear();
        _folder = null;
    }
}

/// <summary>One captured payload. Serialized as a single NDJSON line.</summary>
public readonly record struct FeedDumpEntry(DateTimeOffset Timestamp, string Payload);
