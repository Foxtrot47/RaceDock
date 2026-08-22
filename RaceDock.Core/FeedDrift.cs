// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>Records structurally valid provider payloads that cannot be mapped, once per source and field.</summary>
public static class FeedDrift
{
    private const int MaximumExcerptLength = 1_024;
    private static readonly object Gate = new();
    private static readonly HashSet<(string Source, string Field)> Reported = [];

    public static void Record(string source, string field, JsonNode? payload)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(field)) return;
        lock (Gate)
        {
            if (!Reported.Add((source, field))) return;
        }

        try
        {
            var excerpt = payload?.ToJsonString() ?? "null";
            if (excerpt.Length > MaximumExcerptLength) excerpt = excerpt[..MaximumExcerptLength];
            AppLog.Warning("FeedDrift", $"{source} payload did not map field '{field}'.", data: new
            {
                Source = source,
                Field = field,
                PayloadExcerpt = excerpt
            });
        }
        catch (Exception failure) when (failure is InvalidOperationException or NotSupportedException or JsonException)
        {
            // Logging is diagnostic; a provider payload must never be failed by its diagnostic path.
        }
    }

    internal static void ResetForTests()
    {
        lock (Gate) Reported.Clear();
    }
}
