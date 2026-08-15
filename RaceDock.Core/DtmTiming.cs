// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RaceDock.Core;

internal static partial class DtmTiming
{
    public static TimingSnapshot BuildSnapshot(
        JsonObject session,
        JsonObject bootstrap,
        DateTimeOffset now,
        ConnectionState connection)
    {
        var info = bootstrap["sessionInfo"] as JsonObject ?? [];
        var participants = ByPid(bootstrap["participants"] as JsonArray);
        var ranks = ByPid(bootstrap["ranks"] as JsonArray);
        var gaps = ByPid(bootstrap["gaps"] as JsonArray);
        var statuses = ByPid(bootstrap["runningStatuses"] as JsonArray);
        var locations = ByPid(bootstrap["carLocations"] as JsonArray);
        var bestLaps = BestLapsByPid(bootstrap["bestLaps"] as JsonArray);
        var laps = LatestLapsByPid(bootstrap["laps"] as JsonArray);
        var fastestPid = bestLaps
            .Where(pair => JsonSupport.Int(pair.Value["lapTimeMillis"]) is > 0)
            .OrderBy(pair => JsonSupport.Int(pair.Value["lapTimeMillis"]))
            .Select(pair => (long?)pair.Key)
            .FirstOrDefault();

        List<CompetitorStanding> standings = [];
        foreach (var (pid, rank) in ranks)
        {
            var position = JsonSupport.Int(rank["overallPosition"]) ?? JsonSupport.Int(rank["position"]) ?? 0;
            if (position <= 0 || JsonSupport.Bool(rank["isDeleted"]) || !participants.TryGetValue(pid, out var participant)) continue;
            gaps.TryGetValue(pid, out var gap);
            statuses.TryGetValue(pid, out var runningStatus);
            locations.TryGetValue(pid, out var location);
            bestLaps.TryGetValue(pid, out var bestLap);
            laps.TryGetValue(pid, out var latestLap);
            var status = JsonSupport.String(runningStatus?["status"]) ?? "";
            var retired = status.Contains("Retired", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("DidNotFinish", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("DNF", StringComparison.OrdinalIgnoreCase);
            var stopped = status.Contains("Stopped", StringComparison.OrdinalIgnoreCase);
            var number = JsonSupport.String(participant["carNumber"]) ?? JsonSupport.String(rank["carNumber"]) ?? pid.ToString(CultureInfo.InvariantCulture);
            var code = JsonSupport.String(participant["threeLettersName"]);
            if (string.IsNullOrWhiteSpace(code)) code = DriverCode(JsonSupport.String(participant["lastname"]), number);
            standings.Add(new(
                number,
                position,
                code,
                JsonSupport.String(participant["displayName"]) ?? code,
                JsonSupport.String(participant["teamName"]) ?? "",
                position == 1 ? "LEAD" : FormatGap(gap, "gapToFirstLaps", "gapToFirstMillis"),
                position == 1 ? null : FormatGap(gap, "gapToAheadLaps", "gapToAheadMillis"),
                null,
                JsonSupport.Int(latestLap?["lapNumber"]) ?? 0,
                string.Equals(JsonSupport.String(location?["carLocation"]), "Pit", StringComparison.OrdinalIgnoreCase),
                retired,
                stopped,
                fastestPid == pid,
                FormatLapTime(JsonSupport.Int(bestLap?["lapTimeMillis"])),
                StatusLabel(status),
                null,
                NullIfEmpty(JsonSupport.String(participant["classId"])),
                AlkamelTiming.ManufacturerFromVehicle(JsonSupport.String(participant["manufacturer"]))));
        }

        standings = [.. standings.OrderBy(value => value.Position).ThenBy(value => value.DriverId, StringComparer.Ordinal)];
        var currentLap = standings.Select(value => value.Lap).DefaultIfEmpty(0).Max();
        var chequered = JsonSupport.Bool(info["hasSeenChequered"]) || JsonSupport.Bool(session["hasSeenChequered"]) ||
            bootstrap["chequeredFlags"] is JsonArray { Count: > 0 };
        var started = JsonSupport.Bool(info["isStarted"]);
        var running = JsonSupport.Bool(session["isRunning"]);
        var lifecycle = chequered || JsonSupport.Bool(session["hasResult"]) ? SessionLifecycle.Ended
            : running && started && standings.Count > 0 ? SessionLifecycle.Live
            : SessionLifecycle.PreSession;
        var length = bootstrap["sessionLengthLimit"] as JsonObject;
        var lapsLimit = JsonSupport.Int(length?["lapsLimit"]);
        var totalLaps = lapsLimit is > 0 ? lapsLimit : null;
        return new(
            CleanVenue(JsonSupport.String(info["eventName"]) ?? EventName(session) ?? "DTM"),
            JsonSupport.String(info["sessionName"]) ?? JsonSupport.String(session["name"]) ?? "Live Session",
            CleanVenue(JsonSupport.String((bootstrap["trackDetails"] as JsonObject)?["trackName"]) ?? JsonSupport.String(info["trackName"]) ?? ""),
            currentLap,
            totalLaps,
            TrackState(bootstrap["raceFlags"] as JsonArray),
            standings,
            now,
            lifecycle,
            connection,
            TimeRemaining(bootstrap),
            Championship.Dtm);
    }

    private static Dictionary<long, JsonObject> ByPid(JsonArray? values)
    {
        Dictionary<long, JsonObject> result = [];
        if (values is null) return result;
        foreach (var value in values.OfType<JsonObject>())
            if (long.TryParse(JsonSupport.String(value["pid"]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                result[pid] = value;
        return result;
    }

    private static Dictionary<long, JsonObject> BestLapsByPid(JsonArray? values)
    {
        Dictionary<long, JsonObject> result = [];
        if (values is null) return result;
        foreach (var value in values.OfType<JsonObject>())
        {
            if (!long.TryParse(JsonSupport.String(value["pid"]), out var pid) || JsonSupport.Int(value["lapTimeMillis"]) is not > 0) continue;
            if (!result.TryGetValue(pid, out var current) || JsonSupport.Int(value["lapTimeMillis"]) < JsonSupport.Int(current["lapTimeMillis"]))
                result[pid] = value;
        }
        return result;
    }

    private static Dictionary<long, JsonObject> LatestLapsByPid(JsonArray? values)
    {
        Dictionary<long, JsonObject> result = [];
        if (values is null) return result;
        foreach (var value in values.OfType<JsonObject>())
        {
            if (!long.TryParse(JsonSupport.String(value["pid"]), out var pid)) continue;
            if (!result.TryGetValue(pid, out var current) || (JsonSupport.Int(value["lapNumber"]) ?? 0) > (JsonSupport.Int(current["lapNumber"]) ?? 0))
                result[pid] = value;
        }
        return result;
    }

    private static string? FormatGap(JsonObject? gap, string lapsName, string millisecondsName)
    {
        var laps = JsonSupport.Int(gap?[lapsName]) ?? 0;
        if (laps > 0) return $"{laps} LAP{(laps == 1 ? "" : "S")}";
        var milliseconds = JsonSupport.Int(gap?[millisecondsName]);
        return milliseconds is >= 0 ? $"+{(milliseconds.Value / 1000d).ToString("0.000", CultureInfo.InvariantCulture)}" : null;
    }

    private static string? FormatLapTime(int? milliseconds)
    {
        if (milliseconds is not > 0) return null;
        var time = TimeSpan.FromMilliseconds(milliseconds.Value);
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss\.fff", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static string? TimeRemaining(JsonObject bootstrap)
    {
        var length = bootstrap["sessionLengthLimit"] as JsonObject;
        var clock = bootstrap["sessionClock"] as JsonObject;
        var seconds = JsonSupport.Int(length?["timeLimitSeconds"]);
        if (seconds is not > 0 || !DateTimeOffset.TryParse(JsonSupport.String(clock?["startTime"]), out var start) ||
            !DateTimeOffset.TryParse(JsonSupport.String(clock?["tsNow"]), out var current)) return null;
        var remaining = TimeSpan.FromSeconds(Math.Max(0, seconds.Value - (current - start).TotalSeconds));
        return $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private static TrackCondition TrackState(JsonArray? flags)
    {
        var value = flags?.OfType<JsonObject>().LastOrDefault();
        return (JsonSupport.String(value?["flag"]) ?? "").ToUpperInvariant() switch
        {
            "GREEN" => TrackCondition.AllClear,
            "YELLOW" or "FULLCOURSEYELLOW" or "FULL COURSE YELLOW" or "CODE60" => TrackCondition.Yellow,
            "SAFETYCAR" or "SAFETY CAR" => TrackCondition.SafetyCar,
            "RED" or "REDFLAG" or "RED FLAG" => TrackCondition.RedFlag,
            _ => TrackCondition.Unknown
        };
    }

    private static string? StatusLabel(string status) => status.ToUpperInvariant() switch
    {
        "RETIRED" or "DNF" or "DIDNOTFINISH" => "RET",
        "DNS" or "DIDNOTSTART" => "N/S",
        "DISQUALIFIED" or "DSQ" => "DSQ",
        "STOPPED" => "STOP",
        _ => null
    };

    private static string DriverCode(string? lastName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(lastName)) return fallback;
        var code = new string(lastName.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(code) ? fallback : code;
    }

    private static string? EventName(JsonObject session) => JsonSupport.String(session["event"]?["name"]);
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string CleanVenue(string value) => EventPrefix().Replace(value.Replace("N�RBURGRING", "Nürburgring", StringComparison.OrdinalIgnoreCase), "");

    [GeneratedRegex(@"^\d+\s+")]
    private static partial Regex EventPrefix();
}
