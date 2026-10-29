// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed class WrcApiClient : IDisposable
{
    private const string ApiBase = "https://p-p.redbull.com/rb-wrccom-lintegration-yv-prod/api/";
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public WrcApiClient(HttpClient? httpClient = null)
    {
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient(CreateDefaultHandler()) { Timeout = TimeSpan.FromSeconds(30) };
    }

    // Bound pooled connections so a broken socket or stale DNS entry is retired on its own rather than
    // surviving until the feed is manually restarted. The idle timeout stays below the WRC idle poll interval.
    internal static SocketsHttpHandler CreateDefaultHandler() => new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
        ConnectTimeout = TimeSpan.FromSeconds(10)
    };

    public async Task<IReadOnlyList<WrcSeason>> GetSeasonsAsync(CancellationToken ct)
    {
        var root = await GetRequiredAsync<JsonArray>("seasons.json", ct);
        return root.OfType<JsonObject>().Select(item => new WrcSeason(
                RequireInt(item["seasonId"], "WRC season id"),
                RequireString(item["name"], "WRC season name"),
                RequireInt(item["year"], "WRC season year")))
            .ToList();
    }

    public async Task<IReadOnlyList<WrcCalendarEvent>> GetSeasonEventsAsync(int seasonId, CancellationToken ct)
    {
        var root = await GetRequiredAsync<JsonObject>($"season-detail.json?seasonId={seasonId}", ct);
        var rounds = root["seasonRounds"] as JsonArray ?? throw new InvalidOperationException($"WRC season {seasonId} rounds were missing.");
        List<WrcCalendarEvent> result = [];
        foreach (var round in rounds.OfType<JsonObject>())
        {
            if (round["event"] is JsonObject eventNode && ParseCalendarEvent(eventNode) is { } item) result.Add(item);
        }
        return result.OrderBy(item => item.StartUtc).ToList();
    }

    public async Task<WrcEventDetail> GetEventAsync(int eventId, CancellationToken ct)
    {
        var node = await GetRequiredAsync<JsonObject>($"events/{eventId}.json", ct);
        var parsedId = RequireInt(node["eventId"], "WRC event id");
        if (parsedId != eventId) throw new InvalidOperationException($"WRC event endpoint {eventId} returned event {parsedId}.");
        var start = RequireDate(node["startDate"], "WRC event start date");
        var finish = RequireDate(node["finishDate"], "WRC event finish date");
        var zoneId = JsonSupport.String(node["timeZoneId"]);
        var offset = JsonSupport.Int(node["timeZoneOffset"]);
        var window = WrcTiming.EventWindow(start, finish, zoneId, offset);
        var rallies = (node["rallies"] as JsonArray)?.OfType<JsonObject>().Select(rally => new WrcRally(
                RequireInt(rally["rallyId"], "WRC rally id"),
                JsonSupport.Int(rally["itineraryId"]),
                JsonSupport.Bool(rally["isMain"])))
            .ToList() ?? [];
        return new(parsedId, RequireString(node["name"], "WRC event name"), window.Start, window.End,
            Math.Max(0, JsonSupport.Int(node["shakedownCount"]) ?? 0), rallies, JsonSupport.String(node["location"]));
    }

    public async Task<IReadOnlyDictionary<int, WrcEntry>> GetEntriesAsync(int eventId, int rallyId, CancellationToken ct)
    {
        var root = await GetRequiredAsync<JsonArray>($"events/{eventId}/rallies/{rallyId}/entries.json", ct);
        Dictionary<int, WrcEntry> result = [];
        foreach (var node in root.OfType<JsonObject>())
        {
            var id = JsonSupport.Int(node["entryId"]);
            if (!id.HasValue) continue;
            var driver = node["driver"] as JsonObject;
            var eventClass = (node["eventClasses"] as JsonArray)?.OfType<JsonObject>()
                .Select(item => JsonSupport.String(item["name"]))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
            result[id.Value] = new(
                id.Value,
                JsonSupport.Int(node["entryListOrder"]) ?? int.MaxValue,
                JsonSupport.String(driver?["code"]) ?? JsonSupport.String(node["identifier"]) ?? id.Value.ToString(),
                JsonSupport.String(driver?["fullName"]) ?? "",
                JsonSupport.String(node["manufacturer"]?["name"]) ?? "",
                JsonSupport.String(node["group"]?["name"]) ?? "",
                JsonSupport.String(node["eligibility"])?.Trim() ?? "",
                eventClass,
                JsonSupport.String(node["status"]) ?? "");
        }
        return result;
    }

    public async Task<IReadOnlyList<WrcStage>> GetStagesAsync(int eventId, CancellationToken ct)
    {
        var root = await GetRequiredAsync<JsonArray>($"events/{eventId}/stages.json", ct);
        List<WrcStage> result = [];
        foreach (var node in root.OfType<JsonObject>())
        {
            var id = JsonSupport.Int(node["stageId"]);
            if (!id.HasValue) continue;
            var start = (node["controls"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(control => string.Equals(JsonSupport.String(control["type"]), "StageStart", StringComparison.OrdinalIgnoreCase));
            var splitPoints = (node["splitPoints"] as JsonArray)?.OfType<JsonObject>()
                .Select(ParseSplitPoint)
                .Where(point => point is not null)
                .Select(point => point!)
                .OrderBy(point => point.Number)
                .ToList() ?? [];
            result.Add(new(
                id.Value,
                JsonSupport.Int(node["number"]) ?? 0,
                JsonSupport.String(node["code"]) ?? "SS",
                JsonSupport.String(node["name"]) ?? JsonSupport.String(node["location"]) ?? "",
                WrcTiming.ParseStageStatus(JsonSupport.String(node["status"])),
                WrcTiming.ParseUtcTimestamp(JsonSupport.String(start?["firstCarDueDateTime"])),
                (JsonObject)node.DeepClone(),
                splitPoints));
        }
        return result;
    }

    public async Task<IReadOnlyList<WrcStageTime>> GetStageTimesAsync(int eventId, int stageId, int rallyId, CancellationToken ct)
    {
        var root = await GetRequiredAsync<JsonArray>($"events/{eventId}/stages/{stageId}/stagetimes.json?rallyId={rallyId}", ct);
        return root.OfType<JsonObject>()
            .Where(node => JsonSupport.Int(node["entryId"]).HasValue)
            .Select(node => new WrcStageTime(
                JsonSupport.Int(node["entryId"])!.Value,
                JsonSupport.Int(node["position"]),
                JsonSupport.Int(node["elapsedDurationMs"]),
                JsonSupport.Int(node["diffFirstMs"]),
                JsonSupport.Int(node["diffPrevMs"]),
                JsonSupport.String(node["status"]) ?? ""))
            .ToList();
    }

    public async Task<IReadOnlyList<WrcSplitTime>> TryGetStageSplitTimesAsync(int eventId, int stageId, int rallyId, CancellationToken ct)
    {
        var root = await GetOptionalArrayAsync($"events/{eventId}/stages/{stageId}/splittimes.json?rallyId={rallyId}", ct);
        if (root is null) return [];
        return root.OfType<JsonObject>()
            .Where(node => JsonSupport.Int(node["entryId"]).HasValue &&
                JsonSupport.Int(node["splitPointId"]).HasValue && JsonSupport.Int(node["elapsedDurationMs"]).HasValue)
            .Select(node => new WrcSplitTime(
                JsonSupport.Int(node["entryId"])!.Value,
                JsonSupport.Int(node["splitPointId"])!.Value,
                JsonSupport.Int(node["elapsedDurationMs"])!.Value))
            .ToList();
    }

    public async Task<IReadOnlyList<WrcStageResult>> GetStageResultsAsync(int eventId, int stageId, int rallyId, CancellationToken ct)
    {
        var root = await GetRequiredAsync<JsonArray>($"events/{eventId}/stages/{stageId}/results.json?rallyId={rallyId}", ct);
        return root.OfType<JsonObject>()
            .Where(node => JsonSupport.Int(node["entryId"]).HasValue)
            .Select(node => new WrcStageResult(
                JsonSupport.Int(node["entryId"])!.Value,
                JsonSupport.Int(node["position"]),
                JsonSupport.Int(node["totalTimeMs"])))
            .ToList();
    }

    public async Task<IReadOnlyList<WrcShakedownTime>> TryGetShakedownTimesAsync(int eventId, int number, CancellationToken ct)
    {
        var root = await GetOptionalArrayAsync($"events/{eventId}/shakedowntimes.json?shakedownNumber={number}", ct);
        if (root is null) return [];
        return root.OfType<JsonObject>()
            .Where(node => JsonSupport.Int(node["entryId"]).HasValue)
            .Select(node => new WrcShakedownTime(
                JsonSupport.Int(node["entryId"])!.Value,
                JsonSupport.Int(node["runNumber"]) ?? 0,
                JsonSupport.Int(node["runDurationMs"])))
            .ToList();
    }

    private async Task<T> GetRequiredAsync<T>(string path, CancellationToken ct) where T : JsonNode
    {
        using var response = await SendAsync(path, ct);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(ct);
        FeedDump.Record("wrc", text);
        var node = JsonNode.Parse(text) ?? throw new InvalidOperationException($"WRC endpoint {path} returned empty JSON.");
        return node as T ?? throw new InvalidOperationException($"WRC endpoint {path} returned an unexpected JSON shape.");
    }

    private async Task<JsonArray?> GetOptionalArrayAsync(string path, CancellationToken ct)
    {
        using var response = await SendAsync(path, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength == 0) return null;
        var text = await response.Content.ReadAsStringAsync(ct);
        FeedDump.Record("wrc", text);
        return JsonNode.Parse(text) as JsonArray
            ?? throw new InvalidOperationException($"WRC endpoint {path} returned an unexpected JSON shape.");
    }

    private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(ApiBase), path));
        request.Headers.UserAgent.ParseAdd(BrowserUserAgent.Value);
        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static RallySplitPoint? ParseSplitPoint(JsonObject node)
    {
        var id = JsonSupport.Int(node["splitPointId"]);
        var number = JsonSupport.Int(node["number"]);
        if (!id.HasValue || !number.HasValue ||
            !double.TryParse(JsonSupport.String(node["distance"]), NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
            return null;
        return new(id.Value, number.Value, distance);
    }

    private static WrcCalendarEvent? ParseCalendarEvent(JsonObject node)
    {
        var id = JsonSupport.Int(node["eventId"]);
        if (!id.HasValue || !DateOnly.TryParse(JsonSupport.String(node["startDate"]), out var start) ||
            !DateOnly.TryParse(JsonSupport.String(node["finishDate"]), out var finish)) return null;
        try
        {
            var window = WrcTiming.EventWindow(start, finish,
                JsonSupport.String(node["timeZoneId"]), JsonSupport.Int(node["timeZoneOffset"]));
            return new(id.Value, window.Start, window.End, start);
        }
        catch (InvalidOperationException) { return null; }
    }

    private static int RequireInt(JsonNode? node, string label) => JsonSupport.Int(node) ?? throw new InvalidOperationException($"{label} was missing.");
    private static string RequireString(JsonNode? node, string label) => JsonSupport.String(node) is { Length: > 0 } value ? value : throw new InvalidOperationException($"{label} was missing.");
    private static DateOnly RequireDate(JsonNode? node, string label) => DateOnly.TryParse(JsonSupport.String(node), out var value) ? value : throw new InvalidOperationException($"{label} was missing or invalid.");

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
