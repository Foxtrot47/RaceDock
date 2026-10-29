// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed record NascarSeries(Championship Championship, int SeriesId, string Label)
{
    public static NascarSeries Cup { get; } = new(Championship.NascarCup, 1, "NASCAR Cup");
    public static NascarSeries Xfinity { get; } = new(Championship.NascarXfinity, 2, "NASCAR Xfinity");
    public static NascarSeries Trucks { get; } = new(Championship.NascarTrucks, 3, "NASCAR Trucks");
}

/// <summary>
/// Maps the unofficial NASCAR Cup live feed (<c>/live/feeds/live-feed.json</c>). The feed publishes one
/// whole-field classification and carries no sectors, tyre compounds, or interval-to-ahead, so those
/// columns stay empty rather than being derived from a guess.
/// </summary>
internal static class NascarTiming
{
    private const int CupSeriesId = 1;
    private const int RaceRunType = 3;
    private const int CheckeredFlagState = 5;
    private const int PreSessionFlagState = 8;
    private const int StoppedFlagState = 9;
    private const int UnboundedLapCount = 999;
    private const int OutStatusCode = 0;
    private const int RunningStatusCode = 1;

    public static TimingSnapshot BuildSnapshot(JsonObject payload, DateTimeOffset now, ConnectionState connection) =>
        BuildSnapshot(payload, now, connection, NascarSeries.Cup);

    public static TimingSnapshot BuildSnapshot(JsonObject payload, DateTimeOffset now, ConnectionState connection, NascarSeries series)
    {
        var standings = payload["vehicles"] is JsonArray vehicles ? BuildStandings(vehicles) : [];
        var circuit = JsonSupport.String(payload["track_name"]) ?? "";
        var runType = JsonSupport.Int(payload["run_type"]);
        var currentLap = JsonSupport.Int(payload["lap_number"]) ??
            standings.Select(value => value.Lap).DefaultIfEmpty(0).Max();
        var totalLaps = LapCount(payload["laps_in_race"]);
        var stage = runType == RaceRunType ? CurrentStage(payload) : default;
        var lifecycle = Lifecycle(payload, standings.Count > 0, runType, currentLap, series);
        var trackState = TrackState(JsonSupport.Int(payload["flag_state"]));
        // Off track means pit road only while cars are racing: the grid forms off track before green, the field
        // leaves the track once it has stopped, and a red flag parks it, often on pit road, without anyone pitting.
        if (lifecycle != SessionLifecycle.Live || trackState == TrackCondition.RedFlag)
            standings = [.. standings.Select(value => value.InPit ? value with { InPit = false } : value)];
        return new(
            BlankTo(JsonSupport.String(payload["run_name"]), circuit),
            SessionName(runType),
            circuit,
            currentLap,
            totalLaps,
            trackState,
            standings,
            now,
            lifecycle,
            connection,
            // NASCAR races are run to a lap count, so the feed publishes no session countdown.
            TimeRemaining: null,
            Championship: series.Championship)
        {
            StageNumber = stage.Number,
            StageFinishLap = stage.FinishLap,
            StageLength = stage.Length
        };
    }

    /// <summary>
    /// The stage in progress. Practice and qualifying publish a placeholder stage (finishing lap 0, 99 laps
    /// long), so a stage is only reported when the feed gives it a real finishing lap.
    /// </summary>
    private static StageInfo CurrentStage(JsonObject payload)
    {
        if (payload["stage"] is not JsonObject stage) return default;
        var number = JsonSupport.Int(stage["stage_num"]);
        var finishAtLap = JsonSupport.Int(stage["finish_at_lap"]);
        if (number is not > 0 || finishAtLap is not > 0) return default;
        return new(number, finishAtLap, LapCount(stage["laps_in_stage"]));
    }

    /// <summary>
    /// When the provider assembled the payload. The feed is a snapshot with no transport-level modified
    /// time, so this is the only evidence available that the classification is still being updated.
    /// </summary>
    internal static DateTimeOffset? FeedTimestamp(JsonObject payload) =>
        DateTimeOffset.TryParse(JsonSupport.String(payload["time_of_day_os"]), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var timestamp) ? timestamp : null;

    internal static bool IsCupSeries(JsonObject payload) => JsonSupport.Int(payload["series_id"]) == CupSeriesId;

    internal static bool IsSeries(JsonObject payload, NascarSeries series) =>
        JsonSupport.Int(payload["series_id"]) == series.SeriesId;

    internal static bool IsRace(JsonObject payload) => JsonSupport.Int(payload["run_type"]) == RaceRunType;

    internal static string? SessionKey(JsonObject payload)
    {
        var name = JsonSupport.String(payload["run_name"]);
        if (string.IsNullOrWhiteSpace(name)) return null;
        return $"{JsonSupport.String(payload["track_name"])}|{name}|{JsonSupport.String(payload["run_type"])}";
    }

    /// <summary>
    /// Provisional: the provider publishes an undocumented flag code table, so only the green, yellow,
    /// red, white and chequered codes are mapped. Every other code (0, pre-session 8, stopped 9, and any
    /// future value) reports Unknown rather than claiming a track state the payload does not prove.
    /// The chequered code 5 is an all-clear track too; the finish itself is reported through the lifecycle.
    /// </summary>
    internal static TrackCondition TrackState(int? flagState) => flagState switch
    {
        1 or 4 or CheckeredFlagState => TrackCondition.AllClear,
        2 => TrackCondition.FullCourseYellow,
        3 => TrackCondition.RedFlag,
        _ => TrackCondition.Unknown
    };

    /// <summary>
    /// Cup sessions are identified by the series the subscription answers for, not by the session name,
    /// and the feed never carries practice or qualifying cars it is not running.
    /// </summary>
    private static SessionLifecycle Lifecycle(JsonObject payload, bool hasVehicles, int? runType, int currentLap, NascarSeries series)
    {
        if (!IsSeries(payload, series) || !hasVehicles) return SessionLifecycle.OffSession;
        var flagState = JsonSupport.Int(payload["flag_state"]);
        if (flagState == StoppedFlagState) return SessionLifecycle.Ended;
        // Recorded before a race (an hour before green) and between qualifying rounds: the field is listed
        // but nothing is on track yet.
        if (flagState == PreSessionFlagState) return SessionLifecycle.PreSession;

        var lapLimit = JsonSupport.Int(payload["laps_in_race"]);
        var finished = flagState == CheckeredFlagState ||
            JsonSupport.Int(payload["laps_to_go"]) is 0 && lapLimit is > 0 && currentLap >= lapLimit;
        return runType == RaceRunType && finished ? SessionLifecycle.Ended : SessionLifecycle.Live;
    }

    private static List<CompetitorStanding> BuildStandings(JsonArray vehicles)
    {
        var rows = vehicles.OfType<JsonObject>()
            .Select(vehicle => new VehicleRow(
                vehicle,
                JsonSupport.Int(vehicle["running_position"]) ?? 0,
                Seconds(vehicle["delta"]),
                LapSeconds(vehicle["best_lap_time"])))
            .Where(row => row.Position > 0)
            .OrderBy(row => row.Position)
            .ThenBy(row => JsonSupport.String(row.Vehicle["vehicle_number"]), StringComparer.Ordinal)
            .ToList();
        var fastestLap = rows.Select(row => row.BestLap).Min();

        List<CompetitorStanding> standings = [];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var vehicle = row.Vehicle;
            var driver = vehicle["driver"] as JsonObject;
            var number = JsonSupport.String(vehicle["vehicle_number"]) ?? "";
            var lastName = JsonSupport.String(driver?["last_name"]) ?? "";
            var fullName = JsonSupport.String(driver?["full_name"]) ??
                string.Join(' ', new[] { JsonSupport.String(driver?["first_name"]), lastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            standings.Add(new(
                string.IsNullOrWhiteSpace(number) ? DriverCode(lastName, fullName) : number,
                row.Position,
                DriverCode(lastName, fullName),
                BlankTo(fullName, number),
                JsonSupport.String(vehicle["sponsor_name"]) ?? "",
                row.Position == 1 ? "LEAD" : Gap(row.Delta),
                Interval(index > 0 ? rows[index - 1].Delta : null, row.Delta),
                null,
                JsonSupport.Int(vehicle["laps_completed"]) ?? 0,
                OnPitRoad(vehicle),
                false,
                false,
                row.BestLap is not null && row.BestLap == fastestLap,
                ResultTime: LapTime(row.BestLap),
                StatusLabel: StatusLabel(vehicle),
                Manufacturer: Manufacturer(JsonSupport.String(vehicle["vehicle_manufacturer"])),
                LastLapTime: LapTime(LapSeconds(vehicle["last_lap_time"])))
            {
                CarNumber = string.IsNullOrWhiteSpace(number) ? null : number,
                IsPlayoffEligible = PlayoffEligible(driver)
            });
        }

        return standings;
    }

    /// <summary>
    /// A running car the feed explicitly reports off track is on pit road: in the 24 Oct Xfinity race, 100 of the
    /// 106 times a running car left the track were under caution, when the field pits. <c>pit_stops</c> only lists
    /// completed stops, so it cannot say a car is in the pits now. DVP and out-of-session cars keep their own status,
    /// and an absent <c>is_on_track</c> proves nothing.
    /// </summary>
    private static bool OnPitRoad(JsonObject vehicle) =>
        vehicle["is_on_track"] is JsonValue onTrack && onTrack.TryGetValue<bool>(out var value) && !value &&
        JsonSupport.Int(vehicle["status"]) == RunningStatusCode && !JsonSupport.Bool(vehicle["is_on_dvp"]);

    /// <summary>
    /// The per-vehicle <c>status</c> code table is undocumented: only the observed running code (1) and the
    /// zero that marks a car out of the session are known. Any other code stays unreported rather than claiming
    /// a state the payload does not prove. DVP (the damaged-vehicle policy) takes precedence.
    /// </summary>
    private static string? StatusLabel(JsonObject vehicle)
    {
        if (JsonSupport.Bool(vehicle["is_on_dvp"])) return "DVP";
        return JsonSupport.Int(vehicle["status"]) == OutStatusCode ? "OUT" : null;
    }

    /// <summary>The playoff field is absent on pre-season payloads, which must not read as "not eligible".</summary>
    private static bool? PlayoffEligible(JsonObject? driver) =>
        driver?["is_in_chase"] is null ? null : JsonSupport.Bool(driver["is_in_chase"]);

    private static string SessionName(int? runType) => runType switch
    {
        1 => "Practice",
        2 => "Qualifying",
        RaceRunType => "Race",
        _ => "Live Session"
    };

    private static int? LapCount(JsonNode? node)
    {
        // The feed pads practice and qualifying with a placeholder distance instead of leaving it empty.
        var laps = JsonSupport.Int(node);
        return laps is > 0 and not UnboundedLapCount ? laps : null;
    }

    private static double? Seconds(JsonNode? node)
    {
        var text = JsonSupport.String(node);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value
            : null;
    }

    /// <summary>A car that has not completed a lap reports zero, which is not a lap time.</summary>
    private static double? LapSeconds(JsonNode? node) => Seconds(node) is > 0 and var seconds ? seconds : null;

    private static string? Gap(double? seconds) =>
        seconds is >= 0 ? $"+{seconds.Value.ToString("0.000", CultureInfo.InvariantCulture)}" : null;

    /// <summary>The feed publishes only time to the leader, so the interval is the difference of deltas.</summary>
    private static string? Interval(double? ahead, double? behind) =>
        ahead is >= 0 && behind >= 0 ? Gap(behind - ahead) : null;

    private static string? LapTime(double? seconds)
    {
        if (seconds is null) return null;
        var lap = TimeSpan.FromSeconds(seconds.Value);
        return $"{lap.Minutes}:{lap.Seconds:00}.{lap.Milliseconds:000}";
    }

    private static string? Manufacturer(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "CHV" => "Chevrolet",
        "FRD" => "Ford",
        "TYT" => "Toyota",
        _ => null
    };

    private static string DriverCode(string lastName, string fullName)
    {
        var source = string.IsNullOrWhiteSpace(lastName) ? fullName : lastName;
        var code = new string(source.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(code) ? fullName.ToUpperInvariant() : code;
    }

    private static string BlankTo(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private sealed record VehicleRow(JsonObject Vehicle, int Position, double? Delta, double? BestLap);

    private readonly record struct StageInfo(int? Number, int? FinishLap, int? Length);
}
