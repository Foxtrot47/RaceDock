// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal static class FormulaETiming
{
    public static TimingSnapshot BuildSnapshot(
        JsonObject session,
        JsonObject info,
        JsonObject entries,
        JsonObject track,
        JsonObject standings,
        JsonObject status,
        JsonObject? bestResults,
        DateTimeOffset now,
        ConnectionState connection)
    {
        var activeStandings = IsDuel(session, info)
            ? FilterDuelStandings(entries, standings)
            : standings;
        var snapshot = AlkamelTiming.BuildSnapshot(session, info, entries, track, activeStandings, status, bestResults,
            now, connection, Championship.FormulaE, "Formula E");
        return IsScheduledForFuture(session, info, now)
            ? snapshot with { Lifecycle = SessionLifecycle.PreSession }
            : snapshot;
    }

    internal static bool IsDuel(JsonObject session, JsonObject info) =>
        (JsonSupport.String(session["name"]) ?? JsonSupport.String(info["name"]) ?? "")
            .Contains("Duel", StringComparison.OrdinalIgnoreCase);

    private static bool IsScheduledForFuture(JsonObject session, JsonObject info, DateTimeOffset now)
    {
        var value = JsonSupport.String(session["date"] ?? info["date"]);
        if (!long.TryParse(value, out var timestamp) || timestamp <= 0) return false;
        var scheduled = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
        return scheduled - now > TimeSpan.FromMinutes(10);
    }

    private static JsonObject FilterDuelStandings(JsonObject entries, JsonObject standings)
    {
        var participantNumbers = entries
            .Where(pair => pair.Value is JsonObject)
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (participantNumbers.Count is 0 or > 2) return standings;

        var filtered = (JsonObject)standings.DeepClone();
        if (filtered["standings"] is not JsonObject rows) return filtered;
        foreach (var pair in rows.ToList())
        {
            var data = JsonSupport.String(pair.Value?["data"]);
            var fields = data?.Split(';');
            if (fields is not { Length: > 1 } || !participantNumbers.Contains(fields[1])) rows.Remove(pair.Key);
        }
        return filtered;
    }
}
