// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed record GtwcMeeting(string Id, string Name, string Circuit, int State);
internal sealed record GtwcUnit(string Id, string Name, string CompetitionId, int State);
internal sealed record GtwcDiscovery(string Year, GtwcMeeting? Meeting, GtwcUnit? Unit);
internal sealed record GtwcTrackState(TrackCondition Condition, string? Detail);

internal sealed record GtwcPollingOptions(
    TimeSpan PollInterval,
    TimeSpan ScheduleRefreshInterval,
    TimeSpan IdlePollInterval,
    IReadOnlyList<TimeSpan> DiscoveryBackoff,
    IReadOnlyList<TimeSpan> PollBackoff)
{
    public static GtwcPollingOptions Default { get; } = new(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(15),
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)],
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30)]);
}

internal static class GtwcTiming
{
    public static string? CurrentSeason(JsonObject root, int currentYear)
    {
        var current = JsonSupport.String(root["CurrentSeason"]);
        if (!string.IsNullOrWhiteSpace(current)) return current;
        return (root["AllSeasons"] as JsonArray)?.Select(JsonSupport.String)
            .Where(value => int.TryParse(value, out _))
            .OrderBy(value => int.Parse(value!, CultureInfo.InvariantCulture) == currentYear ? 0 : 1)
            .ThenByDescending(value => int.Parse(value!, CultureInfo.InvariantCulture))
            .FirstOrDefault();
    }

    public static GtwcMeeting? PresentationMeeting(JsonObject season)
    {
        var id = JsonSupport.String(season["PresentationMeetingId"]);
        var meetings = season["Meetings"] as JsonObject;
        var node = ValueById(meetings, id) as JsonObject;
        if (node is null || string.IsNullOrWhiteSpace(id)) return null;
        var circuit = MeetingName(node) ?? "GT World Challenge Europe";
        return new(id, circuit, circuit, JsonSupport.Int(node["State"]) ?? 0);
    }

    /// <summary>The venue-based event name shared by live timing and the schedule.</summary>
    internal static string? MeetingName(JsonObject meeting) =>
        JsonSupport.String(meeting["TrackName"])
        ?? JsonSupport.String(meeting["Location"])
        ?? JsonSupport.String(meeting["Name"]);

    /// <summary>Competitions in a meeting timetable that belong to the championship rather than a support race.</summary>
    public static HashSet<string> GtwcEuropeCompetitionIds(JsonObject schedule)
    {
        var series = schedule["Series"] as JsonObject ?? [];
        var competitions = schedule["Competitions"] as JsonObject ?? [];
        var targetSeries = series
            .Where(pair => pair.Value is JsonObject value &&
                (IsGtwcEuropeName(JsonSupport.String(value["Name"])) ||
                 IsGtwcEuropeName(JsonSupport.String(value["ShortName"]))))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return competitions
            .Where(pair => pair.Value is JsonObject value &&
                (targetSeries.Contains(JsonSupport.String(value["SerieId"]) ?? "") ||
                 IsGtwcEuropeName(JsonSupport.String(value["Name"]))))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static GtwcUnit? PresentedUnit(JsonObject schedule)
    {
        var units = schedule["Units"] as JsonObject ?? [];
        var targetCompetitions = GtwcEuropeCompetitionIds(schedule);
        var id = JsonSupport.String(schedule["PresentationRoundId"]);
        var node = ValueById(units, id) as JsonObject;
        if (node is null || string.IsNullOrWhiteSpace(id)) return null;
        var competitionId = JsonSupport.String(node["CompetitionId"]) ?? "";
        var name = JsonSupport.String(node["Name"]) ?? JsonSupport.String(node["Code"]) ?? "Live session";
        if (!targetCompetitions.Contains(competitionId) || IsTestSession(name)) return null;
        return new(id, name, competitionId, JsonSupport.Int(node["State"]) ?? 0);
    }

    public static bool IsTestSession(string? value) =>
        value?.Contains("test", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsGtwcEuropeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = new StringBuilder(value.Length);
        foreach (var character in value)
            if (char.IsLetterOrDigit(character)) normalized.Append(char.ToUpperInvariant(character));
        var name = normalized.ToString();
        return name.Contains("GTWORLDCH", StringComparison.Ordinal) &&
               name.Contains("EU", StringComparison.Ordinal) &&
               (name.Contains("SPRINTCUP", StringComparison.Ordinal) || name.Contains("ENDURANCECUP", StringComparison.Ordinal));
    }

    public static TimingSnapshot BuildSnapshot(
        GtwcMeeting meeting,
        GtwcUnit unit,
        JsonObject timing,
        JsonObject detail,
        DateTimeOffset now,
        ConnectionState connection)
    {
        RequireUnit(timing, unit.Id, "timing");
        RequireUnit(detail, unit.Id, "competition detail");
        var results = timing["Results"] as JsonObject
            ?? throw new InvalidOperationException($"GTWC unit {unit.Id} timing results were missing.");
        var competitors = detail["Competitors"] as JsonObject ?? [];
        var classes = detail["Classes"] as JsonObject ?? [];
        var standings = ReadStandings(results, competitors, classes);
        var unitInfo = timing["UntInfo"] as JsonObject ?? [];
        return new(meeting.Name, unit.Name, meeting.Circuit,
            standings.Count == 0 ? 0 : standings.Max(standing => standing.Lap), null,
            TrackState(unitInfo).Condition, standings, now, Lifecycle(unit, unitInfo, standings.Count), connection,
            JsonSupport.String(unitInfo["RemainingTime"]), Championship.GtWorldChallengeEurope);
    }

    private static List<CompetitorStanding> ReadStandings(JsonObject results, JsonObject competitors, JsonObject classes)
    {
        var rows = new List<(CompetitorStanding Standing, int Order, double? BestSeconds)>();
        foreach (var pair in results)
        {
            if (pair.Value is not JsonObject result) continue;
            var competitorId = JsonSupport.String(result["CompetitorId"]) ?? pair.Key;
            if (ValueById(competitors, competitorId) is not JsonObject competitor) continue;
            var main = result["MainResult"] as JsonObject ?? [];
            var standing = CreateStanding(competitorId, result, main, competitor, classes);
            if (standing is null) continue;
            rows.Add((standing, JsonSupport.Int(result["ListIndex"]) ?? int.MaxValue, ParseLapSeconds(standing.ResultTime)));
        }
        return OrderedStandings(rows);
    }

    private static List<CompetitorStanding> OrderedStandings(List<(CompetitorStanding Standing, int Order, double? BestSeconds)> rows)
    {
        var ordered = rows.OrderBy(row => row.Standing.Position).ThenBy(row => row.Order)
            .ThenBy(row => row.Standing.DriverId, StringComparer.Ordinal).ToList();
        var fastestId = ordered.Where(row => row.BestSeconds.HasValue).OrderBy(row => row.BestSeconds)
            .ThenBy(row => row.Standing.Position).Select(row => row.Standing.DriverId).FirstOrDefault();
        return ordered.Select(row => row.Standing with { IsOverallFastest = row.Standing.DriverId == fastestId }).ToList();
    }

    private static CompetitorStanding? CreateStanding(
        string competitorId, JsonObject result, JsonObject main, JsonObject competitor, JsonObject classes)
    {
        var order = JsonSupport.Int(result["ListIndex"]);
        var rank = JsonSupport.Int(main["Rank"]);
        var hasRank = rank is > 0;
        if (!hasRank && !order.HasValue) return null;
        var position = hasRank ? rank.GetValueOrDefault() : order.GetValueOrDefault() + 1;
        var identity = DriverIdentity(competitor, position);
        var timing = StandingTiming(main, position);
        var status = JsonSupport.Int(main["Status"]) ?? 0;
        return new(
            competitorId, position, identity.Code, identity.Name,
            JsonSupport.String(competitor["TeamName"]) ?? "",
            timing.Gap, timing.Interval, null, timing.Laps,
            JsonSupport.Bool(competitor["InPitLane"]), (status & (4 | 16)) != 0, false, false,
            timing.Best, StatusLabel(status), hasRank ? null : "—", Category(competitor, classes),
            JsonSupport.String(competitor["ManufacturerName"]),
            CurrentSectors: ParseSectors(main["LastLap"]),
            BestLapSectors: ParseSectors(main["BestTime"]))
        {
            CarNumber = JsonSupport.String(competitor["Bib"])
        };
    }

    private static (string Code, string Name) DriverIdentity(JsonObject competitor, int position)
    {
        var driver = CurrentDriver(competitor);
        var bib = JsonSupport.String(competitor["Bib"]) ?? position.ToString(CultureInfo.InvariantCulture);
        var code = JsonSupport.String(driver?["ShortName"]) ?? bib;
        var name = string.Join(" ", new[] { JsonSupport.String(driver?["FirstName"]), JsonSupport.String(driver?["LastName"]) }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return (code, string.IsNullOrWhiteSpace(name) ? code : name);
    }

    private static (string? Best, string? Gap, string? Interval, int Laps) StandingTiming(JsonObject main, int position) => (
        JsonSupport.String(main["BestTime"]?["Time"]),
        position == 1 ? "LEAD" : FormatGap(JsonSupport.String(main["Behind"])),
        position == 1 ? null : FormatGap(JsonSupport.String(main["Diff"])),
        JsonSupport.Int(main["TotalLapCount"]) ?? 0);

    private static string? Category(JsonObject competitor, JsonObject classes) =>
        ValueById(classes, JsonSupport.String(competitor["ClassId"])) is JsonObject category
            ? JsonSupport.String(category["ShortName"]) ?? JsonSupport.String(category["Name"])
            : null;

    private static SessionLifecycle Lifecycle(GtwcUnit unit, JsonObject info, int count)
    {
        if (JsonSupport.Bool(info["ChequeredFlag"]) || unit.State == 2) return SessionLifecycle.Ended;
        if (unit.State == 1 && count > 0) return SessionLifecycle.Live;
        return unit.State == 0 ? SessionLifecycle.PreSession : SessionLifecycle.OffSession;
    }

    private static SectorTimingSet? ParseSectors(JsonNode? node)
    {
        if (node is not JsonObject lap || lap["Intermediates"] is not JsonArray values) return null;
        var sectors = new SectorTimingSet(ParseSector(values.ElementAtOrDefault(0)),
            ParseSector(values.ElementAtOrDefault(1)), ParseSector(values.ElementAtOrDefault(2)));
        return sectors.Sector1 is null && sectors.Sector2 is null && sectors.Sector3 is null ? null : sectors;
    }

    private static SectorTiming? ParseSector(JsonNode? node)
    {
        if (node is not JsonObject sector) return null;
        var value = JsonSupport.String(sector["Time"]);
        var seconds = ParseLapSeconds(value);
        if (seconds is not > 0 || !double.IsFinite(seconds.Value)) return null;
        // Swiss Timing states are normal, personal best, and session best.
        var state = JsonSupport.Int(sector["TimeState"]) switch
        {
            0 => SectorTimingState.Slower,
            1 => SectorTimingState.PersonalBest,
            2 => SectorTimingState.OverallBest,
            _ => SectorTimingState.None
        };
        return new(value!, state);
    }

    public static GtwcTrackState TrackState(JsonObject unitInfo)
    {
        var trackFlag = JsonSupport.Int(unitInfo["TrackFlag"]) ?? 0;
        var sectorFlags = (unitInfo["SectorFlags"] as JsonArray)?.Select(value => JsonSupport.Int(value) ?? 0).ToList() ?? [];
        if (sectorFlags.Contains(8)) return new(TrackCondition.RedFlag, "RED FLAG");
        if (sectorFlags.Contains(32)) return new(TrackCondition.SafetyCar, "SAFETY CAR");
        if (sectorFlags.Contains(64)) return new(TrackCondition.VirtualSafetyCar, "CODE 60");
        if (trackFlag == 4) return new(TrackCondition.FullCourseYellow, "FULL COURSE YELLOW");
        if (sectorFlags.Contains(4)) return new(TrackCondition.Yellow, "YELLOW FLAG");
        return trackFlag switch
        {
            1 => new(TrackCondition.AllClear, null),
            8 => new(TrackCondition.RedFlag, "RED FLAG"),
            32 => new(TrackCondition.SafetyCar, "SAFETY CAR"),
            64 => new(TrackCondition.VirtualSafetyCar, "CODE 60"),
            _ => new(TrackCondition.Unknown, null)
        };
    }

    public static string? StatusLabel(int status)
    {
        if ((status & 32) != 0) return "N/S";
        if ((status & (4 | 16)) != 0) return "RET";
        if ((status & 2) != 0) return "RUN";
        if ((status & 1) != 0) return "FIN";
        return null;
    }

    public static string? FormatGap(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value[0] is '+' or '-') return value;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? $"+{value}"
            : value;
    }

    private static JsonNode? ValueById(JsonObject? values, string? id)
    {
        if (values is null || string.IsNullOrWhiteSpace(id)) return null;
        if (values.TryGetPropertyValue(id, out var exact)) return exact;
        return values.FirstOrDefault(pair => pair.Key.Equals(id, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static JsonObject? CurrentDriver(JsonObject competitor)
    {
        var drivers = competitor["Drivers"] as JsonObject;
        if (drivers is null) return null;
        var currentId = JsonSupport.String(competitor["CurrentDriverId"]);
        if (ValueById(drivers, currentId) is JsonObject current) return current;
        return drivers.Select(pair => pair.Value).OfType<JsonObject>()
            .OrderBy(driver => JsonSupport.Int(driver["ListIndex"]) ?? int.MaxValue)
            .FirstOrDefault();
    }

    private static void RequireUnit(JsonObject root, string expected, string source)
    {
        var actual = JsonSupport.String(root["UnitId"]);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"GTWC {source} returned unit {actual ?? "—"} instead of {expected}.");
    }

    private static double? ParseLapSeconds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split(':');
        return parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? minutes * 60 + seconds
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct) ? direct : null;
    }
}
