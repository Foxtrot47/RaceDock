// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

public static class JsonSupport
{
    // Bounds how far a malformed patch can grow an array.
    private const int MaximumIndexedLength = 4096;

    public static void DeepMerge(JsonObject destination, JsonObject? source)
    {
        if (source is null) return;
        foreach (var pair in source)
            if (destination[pair.Key] is JsonObject d && pair.Value is JsonObject s) DeepMerge(d, s);
            else if (destination[pair.Key] is JsonArray a && pair.Value is JsonObject indexed && IsIndexPatch(indexed))
                MergeIndexed(a, indexed);
            else destination[pair.Key] = pair.Value?.DeepClone();
    }

    /// <summary>The timing feeds patch an array by sending an object keyed by position.</summary>
    private static bool IsIndexPatch(JsonObject source) =>
        source.Count > 0 && source.All(pair => int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0);

    private static void MergeIndexed(JsonArray destination, JsonObject source)
    {
        foreach (var pair in source)
        {
            var index = int.Parse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (index >= MaximumIndexedLength) continue;
            while (destination.Count <= index) destination.Add(new JsonObject());
            if (destination[index] is JsonObject existing && pair.Value is JsonObject patch) DeepMerge(existing, patch);
            else destination[index] = pair.Value?.DeepClone();
        }
    }

    public static string? String(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<string>(out var s)) return s;
        if (value.TryGetValue<int>(out var i)) return i.ToString(CultureInfo.InvariantCulture);
        if (value.TryGetValue<long>(out var l)) return l.ToString(CultureInfo.InvariantCulture);
        if (value.TryGetValue<double>(out var d)) return d.ToString(CultureInfo.InvariantCulture);
        if (value.TryGetValue<bool>(out var b)) return b ? "true" : "false";
        return null;
    }

    public static int? Int(JsonNode? node)
    {
        var text = String(node);
        return int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    public static bool Bool(JsonNode? node)
    {
        var text = String(node);
        return bool.TryParse(text, out var b) ? b : int.TryParse(text, out var i) && i != 0;
    }

    public static string? TimingValue(JsonNode? node)
    {
        var raw = node is JsonObject obj ? String(obj["Value"]) : String(node);
        return string.IsNullOrWhiteSpace(raw) || raw is "-" or "0" or "0.000" ? null : raw;
    }
}
