// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>Reads the reported virtual energy tank level; no fuel conversion or extrapolation is applied.</summary>
internal static class WecEnergyTiming
{
    public static IReadOnlyDictionary<long, double?> Read(JsonNode? node)
    {
        var result = new Dictionary<long, double?>();
        if (node is not JsonObject tank || tank["items"] is not JsonArray items) return result;
        foreach (var item in items.OfType<JsonObject>())
        {
            if (!long.TryParse(JsonSupport.String(item["pid"]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)) continue;
            result[pid] = ParsePercent(JsonSupport.String(item["e"]));
        }
        return result;
    }

    private static double? ParsePercent(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) &&
        double.IsFinite(percent) && percent is >= 0 and <= 100 ? percent : null;
}
