// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RaceDock.Core;

internal static partial class GriiipTiming
{
    public static TimingSnapshot BuildSnapshot(
        JsonObject session,
        JsonObject bootstrap,
        DateTimeOffset now,
        ConnectionState connection,
        GriiipSeries series)
    {
        var info = bootstrap["sessionInfo"] as JsonObject ?? [];
        var participants = ByPid(bootstrap["participants"] as JsonArray);
        var lapValues = bootstrap["laps"] as JsonArray;
        var bestLaps = BestLapsByPid(bootstrap["bestLaps"] as JsonArray, lapValues);
        var (ranks, gaps) = RankingData(bootstrap, participants, bestLaps);
        var statuses = ByPid(bootstrap["runningStatuses"] as JsonArray);
        var locations = ByPid(bootstrap["carLocations"] as JsonArray);
        var laps = LatestLapsByPid(lapValues);
        var fastestPid = FastestPid(bestLaps);
        var standings = BuildStandings(series, participants, ranks, gaps, statuses, locations, bestLaps, laps, fastestPid);
        var currentLap = standings.Select(value => value.Lap).DefaultIfEmpty(0).Max();
        return new(
            MeetingName(series, info, session),
            JsonSupport.String(info["sessionName"]) ?? JsonSupport.String(session["name"]) ?? "Live Session",
            CleanVenue(JsonSupport.String((bootstrap["trackDetails"] as JsonObject)?["trackName"]) ?? JsonSupport.String(info["trackName"]) ?? ""),
            currentLap,
            TotalLaps(bootstrap),
            TrackState(bootstrap["raceFlags"] as JsonArray),
            standings,
            now,
            Lifecycle(session, info, bootstrap, standings.Count),
            connection,
            TimeRemaining(bootstrap),
            series.Championship);
    }

    private static List<CompetitorStanding> BuildStandings(
        GriiipSeries series,
        Dictionary<long, JsonObject> participants,
        Dictionary<long, JsonObject> ranks,
        Dictionary<long, JsonObject> gaps,
        Dictionary<long, JsonObject> statuses,
        Dictionary<long, JsonObject> locations,
        Dictionary<long, JsonObject> bestLaps,
        Dictionary<long, JsonObject> laps,
        long? fastestPid)
    {
        List<CompetitorStanding> standings = [];
        foreach (var (pid, rank) in ranks)
        {
            if (CreateStanding(series, pid, rank, participants, gaps, statuses, locations, bestLaps, laps, fastestPid) is { } standing)
                standings.Add(standing);
        }
        return [.. standings.OrderBy(value => value.Position).ThenBy(value => value.DriverId, StringComparer.Ordinal)];
    }

    private static CompetitorStanding? CreateStanding(
        GriiipSeries series,
        long pid,
        JsonObject rank,
        Dictionary<long, JsonObject> participants,
        Dictionary<long, JsonObject> gaps,
        Dictionary<long, JsonObject> statuses,
        Dictionary<long, JsonObject> locations,
        Dictionary<long, JsonObject> bestLaps,
        Dictionary<long, JsonObject> laps,
        long? fastestPid)
    {
        var position = Position(rank);
        if (!CanClassify(position, rank, pid, participants, out var participant)) return null;
        gaps.TryGetValue(pid, out var gap);
        statuses.TryGetValue(pid, out var runningStatus);
        locations.TryGetValue(pid, out var location);
        bestLaps.TryGetValue(pid, out var bestLap);
        laps.TryGetValue(pid, out var latestLap);
        var status = JsonSupport.String(runningStatus?["status"]) ?? "";
        var number = CarNumber(participant, rank, pid);
        var identity = ParticipantIdentity(series, participant, number);
        var timing = StandingTiming(position, gap, bestLap, latestLap);
        return new(
            number, position, identity.Code, identity.Name,
            JsonSupport.String(participant["teamName"]) ?? "",
            timing.Gap, timing.Interval, null, timing.Lap,
            string.Equals(JsonSupport.String(location?["carLocation"]), "Pit", StringComparison.OrdinalIgnoreCase),
            IsRetired(status), status.Contains("Stopped", StringComparison.OrdinalIgnoreCase), fastestPid == pid,
            timing.BestLap, StatusLabel(status), null,
            NullIfEmpty(JsonSupport.String(participant["classId"])),
            AlkamelTiming.ManufacturerFromVehicle(JsonSupport.String(participant["manufacturer"])), timing.LastLap);
    }

    private static int Position(JsonObject rank) =>
        JsonSupport.Int(rank["overallPosition"]) ?? JsonSupport.Int(rank["position"]) ?? 0;

    private static bool CanClassify(
        int position,
        JsonObject rank,
        long pid,
        Dictionary<long, JsonObject> participants,
        out JsonObject participant)
    {
        if (position > 0 && !JsonSupport.Bool(rank["isDeleted"]) && participants.TryGetValue(pid, out var found))
        {
            participant = found;
            return true;
        }
        participant = [];
        return false;
    }

    private static string CarNumber(JsonObject participant, JsonObject rank, long pid) =>
        JsonSupport.String(participant["carNumber"]) ?? JsonSupport.String(rank["carNumber"]) ??
        pid.ToString(CultureInfo.InvariantCulture);

    private static (string? Gap, string? Interval, int Lap, string? BestLap, string? LastLap) StandingTiming(
        int position, JsonObject? gap, JsonObject? bestLap, JsonObject? latestLap) => (
            position == 1 ? "LEAD" : FormatGap(gap, "gapToFirstLaps", "gapToFirstMillis"),
            position == 1 ? null : FormatGap(gap, "gapToAheadLaps", "gapToAheadMillis"),
            JsonSupport.Int(latestLap?["lapNumber"]) ?? 0,
            FormatLapTime(JsonSupport.Int(bestLap?["lapTimeMillis"])),
            FormatLapTime(JsonSupport.Int(latestLap?["lapTimeMillis"])));

    private static (Dictionary<long, JsonObject> Ranks, Dictionary<long, JsonObject> Gaps) RankingData(
        JsonObject bootstrap,
        Dictionary<long, JsonObject> participants,
        Dictionary<long, JsonObject> bestLaps)
    {
        var ranks = ByPid(bootstrap["ranks"] as JsonArray);
        if (ranks.Count == 0 && bestLaps.Count > 0) ranks = QualifyingRanks(participants, bestLaps);
        var gaps = ByPid(bootstrap["gaps"] as JsonArray);
        if (gaps.Count == 0 && bestLaps.Count > 0) gaps = QualifyingGaps(bestLaps);
        return (ranks, gaps);
    }

    private static int? TotalLaps(JsonObject bootstrap)
    {
        var laps = JsonSupport.Int(bootstrap["sessionLengthLimit"]?["lapsLimit"]);
        return laps is > 0 ? laps : null;
    }

    private static (string Code, string Name) ParticipantIdentity(GriiipSeries series, JsonObject participant, string fallback)
    {
        if (series.Championship is Championship.WorldEnduranceChampionship or Championship.EuropeanLeMansSeries
            or Championship.MichelinLeMansCup)
            return EnduranceDriverIdentity(participant, fallback);
        var participantCode = JsonSupport.String(participant["threeLettersName"]);
        if (string.IsNullOrWhiteSpace(participantCode))
            participantCode = DriverCode(JsonSupport.String(participant["lastname"]), fallback);
        return (participantCode, JsonSupport.String(participant["displayName"]) ?? participantCode);
    }

    private static (string Code, string Name) EnduranceDriverIdentity(JsonObject participant, string fallback)
    {
        var driver = CurrentDriver(participant);
        var lastName = JsonSupport.String(participant["lastname"]);
        var firstName = JsonSupport.String(participant["firstname"]);
        var name = JsonSupport.String(driver?["displayName"]);
        if (string.IsNullOrWhiteSpace(name)) name = string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var code = JsonSupport.String(driver?["threeLettersName"]);
        return (string.IsNullOrWhiteSpace(code) ? DriverCode(lastName ?? name, fallback) : code,
            string.IsNullOrWhiteSpace(name) ? fallback : name);
    }

    private static JsonObject? CurrentDriver(JsonObject participant)
    {
        var currentId = JsonSupport.String(participant["currentDriverId"]);
        return (participant["drivers"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(driver => string.Equals(
                JsonSupport.String(driver["externalDriverID"]), currentId, StringComparison.Ordinal));
    }

    private static long? FastestPid(Dictionary<long, JsonObject> bestLaps) => bestLaps
        .Where(pair => JsonSupport.Int(pair.Value["lapTimeMillis"]) is > 0)
        .OrderBy(pair => JsonSupport.Int(pair.Value["lapTimeMillis"]))
        .Select(pair => (long?)pair.Key)
        .FirstOrDefault();

    private static SessionLifecycle Lifecycle(JsonObject session, JsonObject info, JsonObject bootstrap, int standingsCount)
    {
        var chequered = JsonSupport.Bool(info["hasSeenChequered"]) || JsonSupport.Bool(session["hasSeenChequered"]) ||
            bootstrap["chequeredFlags"] is JsonArray { Count: > 0 };
        if (chequered || JsonSupport.Bool(session["hasResult"])) return SessionLifecycle.Ended;
        return JsonSupport.Bool(session["isRunning"]) && JsonSupport.Bool(info["isStarted"]) && standingsCount > 0
            ? SessionLifecycle.Live
            : SessionLifecycle.PreSession;
    }

    private static bool IsRetired(string status) =>
        status.Contains("Retired", StringComparison.OrdinalIgnoreCase) ||
        status.Contains("DidNotFinish", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("DNF", StringComparison.OrdinalIgnoreCase);

    private static string MeetingName(GriiipSeries series, JsonObject info, JsonObject session)
    {
        var value = JsonSupport.String(info["eventName"]) ?? EventName(session) ?? series.FallbackEvent;
        return series.Championship == Championship.Dtm ? CleanVenue(value) : value;
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

    private static Dictionary<long, JsonObject> BestLapsByPid(JsonArray? values, JsonArray? laps)
    {
        Dictionary<long, JsonObject> result = [];
        AddBestLaps(result, values, requireValid: false);
        if (result.Count == 0) AddBestLaps(result, laps, requireValid: true);
        return result;
    }

    private static void AddBestLaps(Dictionary<long, JsonObject> result, JsonArray? values, bool requireValid)
    {
        if (values is null) return;
        foreach (var value in values.OfType<JsonObject>())
        {
            if (requireValid && !JsonSupport.Bool(value["isValid"])) continue;
            if (!long.TryParse(JsonSupport.String(value["pid"]), out var pid) || JsonSupport.Int(value["lapTimeMillis"]) is not > 0) continue;
            if (!result.TryGetValue(pid, out var current) || JsonSupport.Int(value["lapTimeMillis"]) < JsonSupport.Int(current["lapTimeMillis"]))
                result[pid] = value;
        }
    }

    private static Dictionary<long, JsonObject> QualifyingRanks(
        Dictionary<long, JsonObject> participants,
        Dictionary<long, JsonObject> bestLaps) => participants.Keys
            .OrderBy(pid => bestLaps.TryGetValue(pid, out var lap) ? JsonSupport.Int(lap["lapTimeMillis"]) : int.MaxValue)
            .ThenBy(pid => pid)
            .Select((pid, index) => (pid, rank: new JsonObject { ["overallPosition"] = index + 1 }))
            .ToDictionary(value => value.pid, value => value.rank);

    private static Dictionary<long, JsonObject> QualifyingGaps(Dictionary<long, JsonObject> bestLaps)
    {
        var ordered = bestLaps
            .Where(pair => JsonSupport.Int(pair.Value["lapTimeMillis"]) is > 0)
            .OrderBy(pair => JsonSupport.Int(pair.Value["lapTimeMillis"]))
            .ToList();
        if (ordered.Count == 0) return [];
        var leader = JsonSupport.Int(ordered[0].Value["lapTimeMillis"])!.Value;
        Dictionary<long, JsonObject> result = [];
        for (var index = 0; index < ordered.Count; index++)
        {
            var time = JsonSupport.Int(ordered[index].Value["lapTimeMillis"])!.Value;
            var previous = index == 0 ? time : JsonSupport.Int(ordered[index - 1].Value["lapTimeMillis"])!.Value;
            result[ordered[index].Key] = new JsonObject
            {
                ["gapToFirstMillis"] = time - leader,
                ["gapToAheadMillis"] = time - previous
            };
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
    private static string CleanVenue(string value) => EventPrefix().Replace(
        value.Replace("N�RBURGRING", "Nürburgring", StringComparison.OrdinalIgnoreCase), "");

    [GeneratedRegex(@"^\d+\s+")]
    private static partial Regex EventPrefix();
}
