// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>Reads provider-reported ages for the currently fitted tyres, never the entry's total laps.</summary>
internal static class WecTyreTiming
{
    public static IReadOnlyDictionary<long, CarTyres> Read(JsonNode? node)
    {
        if (node is not JsonArray rows) return new Dictionary<long, CarTyres>();
        var latest = new Dictionary<long, JsonObject>();
        foreach (var row in rows.OfType<JsonObject>())
        {
            if (!long.TryParse(JsonSupport.String(row["pid"]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)) continue;
            if (!latest.TryGetValue(pid, out var previous) || Elapsed(row) >= Elapsed(previous)) latest[pid] = row;
        }
        return latest.ToDictionary(pair => pair.Key, pair => Tyres(pair.Value["tires"] as JsonArray));
    }

    private static int Elapsed(JsonObject row) => JsonSupport.Int(row["elapsedTimeMillis"]) ?? 0;

    private static CarTyres Tyres(JsonArray? rows)
    {
        var wheels = new Dictionary<string, WheelTyre>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in (rows ?? []).OfType<JsonObject>())
        {
            var id = JsonSupport.String(row["id"]);
            if (id is null) continue;
            var age = JsonSupport.Int(row["ageInLaps"]);
            wheels[id] = new(age is >= 0 ? age : null, JsonSupport.String(row["compound"]));
        }
        return new(wheels.GetValueOrDefault("frontLeft"), wheels.GetValueOrDefault("frontRight"),
            wheels.GetValueOrDefault("rearLeft"), wheels.GetValueOrDefault("rearRight"));
    }
}
