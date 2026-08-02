// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed record ImsaTimingOptions(
    TimeSpan StaleTimeout,
    IReadOnlyList<TimeSpan> ReconnectBackoff)
{
    public static ImsaTimingOptions Default { get; } = new(
        TimeSpan.FromSeconds(45),
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4),
         TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)]);
}

internal sealed record ImsaTrackState(TrackCondition Condition, string? Detail);

internal static class ImsaTiming
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
        var rawRows = standings["standings"] as JsonObject
            ?? throw new InvalidOperationException("IMSA standings were missing.");
        var parsed = new List<(CompetitorStanding Standing, ParsedRow Row)>();
        foreach (var pair in rawRows)
        {
            if (pair.Value is not JsonObject row) continue;
            var timing = ParseRow(row);
            if (timing is null || timing.Position <= 0 || string.IsNullOrWhiteSpace(timing.RaceNumber)) continue;
            var entry = ValueById(entries, timing.RaceNumber) as JsonObject ?? [];
            var code = (JsonSupport.String(entry["driver"]) ?? DriverCode(entry) ?? timing.RaceNumber).ToUpperInvariant();
            var name = JsonSupport.String(entry["name"]);
            if (string.IsNullOrWhiteSpace(name))
                name = string.Join(" ", new[] { JsonSupport.String(entry["firstname"]), JsonSupport.String(entry["lastname"]) }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(name)) name = code;
            var classification = timing.Classification.ToUpperInvariant();
            var location = timing.Location.ToUpperInvariant();
            var retired = classification == "RETIRED";
            var stopped = location == "STOPPED";
            var inPit = location == "BOX" || string.IsNullOrEmpty(classification) && string.IsNullOrEmpty(location);
            var statusLabel = StatusLabel(classification, location, timing.IsCheckered);
            parsed.Add((new CompetitorStanding(
                timing.RaceNumber,
                timing.Position,
                code,
                name,
                JsonSupport.String(entry["team"]) ?? "",
                null,
                null,
                null,
                timing.Laps,
                inPit,
                retired,
                stopped,
                false,
                FormatLapTime(timing.BestLapMilliseconds),
                statusLabel,
                null,
                JsonSupport.String(row["class"]) ?? JsonSupport.String(entry["class"]),
                ManufacturerFromVehicle(JsonSupport.String(entry["vehicle"]))), timing));
        }

        parsed = parsed.OrderBy(value => value.Standing.Position).ThenBy(value => value.Standing.DriverId, StringComparer.Ordinal).ToList();
        var fastestNumber = JsonSupport.String(bestResults?["bestLap"]?["participantNumber"])
            ?? parsed.Where(value => value.Row.BestLapMilliseconds > 0)
                .OrderBy(value => value.Row.BestLapMilliseconds)
                .ThenBy(value => value.Standing.Position)
                .Select(value => value.Row.RaceNumber)
                .FirstOrDefault();
        var race = string.Equals(JsonSupport.String(info["type"]), "RACE", StringComparison.OrdinalIgnoreCase);
        for (var index = 0; index < parsed.Count; index++)
        {
            var value = parsed[index];
            string? gap;
            string? interval;
            if (index == 0)
            {
                gap = "LEAD";
                interval = null;
            }
            else if (race)
            {
                gap = FormatGap(CalculateRaceGap(parsed[0].Row, value.Row));
                interval = FormatGap(CalculateRaceGap(parsed[index - 1].Row, value.Row));
            }
            else
            {
                gap = FormatGap(CalculateBestLapGap(parsed[0].Row, value.Row));
                interval = FormatGap(CalculateBestLapGap(parsed[index - 1].Row, value.Row));
            }
            parsed[index] = (value.Standing with
            {
                GapToLeader = gap,
                IntervalToPositionAhead = interval,
                IsOverallFastest = value.Row.RaceNumber == fastestNumber
            }, value.Row);
        }

        var competitors = parsed.Select(value => value.Standing).ToList();
        var currentLap = JsonSupport.Int(status["currentLap"])
            ?? parsed.Select(value => Math.Max(value.Row.CurrentLapNumber, value.Row.Laps)).DefaultIfEmpty(0).Max();
        var finished = JsonSupport.Bool(status["isFinished"]) || JsonSupport.Bool(status["isClosed"]) || JsonSupport.Bool(info["closed"]);
        var running = JsonSupport.Bool(status["isSessionRunning"]);
        var started = ParseLongNode(status["startTime"]) > 0 || currentLap > 0 || (JsonSupport.Int(status["elapsedLaps"]) ?? 0) > 0;
        var lifecycle = finished ? SessionLifecycle.Ended
            : (running || started) && competitors.Count > 0 ? SessionLifecycle.Live
            : SessionLifecycle.PreSession;
        var finalType = JsonSupport.String(status["finalType"])?.ToUpperInvariant() ?? "";
        var timed = JsonSupport.Bool(status["isForcedByTime"]) || finalType.StartsWith("BY_TIME", StringComparison.Ordinal);
        var finalLaps = JsonSupport.Int(status["finalLaps"]);
        var totalLaps = timed && !JsonSupport.Bool(status["isTimeFinished"]) ? null : finalLaps is > 0 ? finalLaps : null;
        return new(
            JsonSupport.String(info["eventName"]) ?? JsonSupport.String(info["champName"]) ?? "IMSA",
            JsonSupport.String(session["name"]) ?? JsonSupport.String(info["name"]) ?? "Live Session",
            JsonSupport.String(track["name"]) ?? JsonSupport.String(track["shortName"]) ?? "",
            currentLap,
            totalLaps,
            TrackState(status).Condition,
            competitors,
            now,
            lifecycle,
            connection,
            timed ? TimeRemaining(status, now) : null,
            Championship.Imsa);
    }

    public static ImsaTrackState TrackState(JsonObject status) =>
        (JsonSupport.String(status["currentFlag"]) ?? "").ToUpperInvariant() switch
        {
            "GF" => new(TrackCondition.AllClear, null),
            "YF" => new(TrackCondition.Yellow, "YELLOW FLAG"),
            "FCY" => new(TrackCondition.Yellow, "FULL COURSE YELLOW"),
            "SC" => new(TrackCondition.SafetyCar, "SAFETY CAR"),
            "RF" => new(TrackCondition.RedFlag, "RED FLAG"),
            _ => new(TrackCondition.Unknown, null)
        };

    public static string? TimeRemaining(JsonObject status, DateTimeOffset now)
    {
        var start = ParseLongNode(status["startTime"]);
        var duration = ParseLongNode(status["finalTime"]);
        if (start <= 0 || duration <= 0) return null;
        var stopped = Math.Max(0, ParseLongNode(status["stoppedSeconds"]));
        var stop = ParseLongNode(status["stopTime"]);
        var effectiveNow = !JsonSupport.Bool(status["isSessionRunning"]) && stop > 0 ? stop : now.ToUnixTimeSeconds();
        var seconds = Math.Max(0, start + duration + stopped - effectiveNow);
        var remaining = TimeSpan.FromSeconds(seconds);
        return $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    public static string? StatusLabel(string classification, string location, bool checkered)
    {
        if (checkered && classification == "CLASSIFIED") return "FIN";
        return classification switch
        {
            "RETIRED" => "RET",
            "NOT_CLASSIFIED" => "N/C",
            "NOT_STARTED" => "N/S",
            "DISQUALIFIED" or "EXCLUDED" => "DSQ",
            _ when location == "TRACK" => "RUN",
            _ when location == "OUT_LAP" => "OUT",
            _ => null
        };
    }

    public static string? ManufacturerFromVehicle(string? vehicle)
    {
        if (string.IsNullOrWhiteSpace(vehicle)) return null;
        var value = vehicle.ToUpperInvariant();
        if (value.Contains("ASTON MARTIN", StringComparison.Ordinal)) return "Aston Martin";
        if (value.Contains("MERCEDES", StringComparison.Ordinal)) return "Mercedes-AMG";
        if (value.Contains("CHEVROLET", StringComparison.Ordinal) || value.Contains("CORVETTE", StringComparison.Ordinal)) return "Chevrolet";
        if (value.Contains("LAMBORGHINI", StringComparison.Ordinal)) return "Lamborghini";
        if (value.Contains("PORSCHE", StringComparison.Ordinal)) return "Porsche";
        if (value.Contains("CADILLAC", StringComparison.Ordinal)) return "Cadillac";
        if (value.Contains("FERRARI", StringComparison.Ordinal)) return "Ferrari";
        if (value.Contains("LEXUS", StringComparison.Ordinal)) return "Lexus";
        if (value.Contains("ACURA", StringComparison.Ordinal)) return "Acura";
        if (value.Contains("FORD", StringComparison.Ordinal)) return "Ford";
        if (value.Contains("BMW", StringComparison.Ordinal)) return "BMW";
        return null;
    }

    private static ParsedRow? ParseRow(JsonObject row)
    {
        var data = (JsonSupport.String(row["data"]) ?? "").Split(';');
        if (data.Length < 10) return null;
        return new(
            ParseInt(data[0]),
            data[1],
            data[2],
            ParseInt(data[4]),
            ParseLong(data[7]),
            data[8],
            ParseInt(data[9]),
            JsonSupport.Bool(row["isRunning"]),
            JsonSupport.Bool(row["isCheckered"]),
            JsonSupport.Int(row["bestLapTime"]) ?? 0,
            ParseLoops(JsonSupport.String(row["currentLoopSectors"])),
            ParseLoops(JsonSupport.String(row["previousLoopSectors"])));
    }

    private static GapValue? CalculateBestLapGap(ParsedRow reference, ParsedRow target) =>
        reference.BestLapMilliseconds > 0 && target.BestLapMilliseconds > 0
            ? GapValue.ForSeconds(Math.Max(0, target.BestLapMilliseconds - reference.BestLapMilliseconds) / 1000d)
            : null;

    private static GapValue? CalculateRaceGap(ParsedRow reference, ParsedRow target)
    {
        if (!target.IsRunning || target.CurrentLapStartMilliseconds == 0 && target.CurrentLoops.Count == 0) return null;
        var lapDifference = reference.CurrentLapNumber > 0 && target.CurrentLapNumber > 0
            ? reference.CurrentLapNumber - target.CurrentLapNumber
            : reference.Laps - target.Laps;
        var targetLoop = target.CurrentLoops.Keys.DefaultIfEmpty(0).Max();
        var targetCrossing = target.CurrentLapStartMilliseconds + target.CurrentLoops.GetValueOrDefault(targetLoop);
        var referenceLoop = reference.CurrentLoops.Keys.DefaultIfEmpty(0).Max();
        var referenceCrossing = CrossingAt(reference, targetLoop, referenceLoop);
        if (lapDifference > 1)
        {
            var laps = targetLoop > referenceLoop ? lapDifference - 1
                : referenceLoop < targetLoop || referenceCrossing < targetCrossing ? lapDifference : lapDifference - 1;
            return GapValue.ForLaps(Math.Max(1, laps));
        }
        if (lapDifference == 1)
        {
            if (referenceLoop > targetLoop) return GapValue.ForLaps(1);
            if (referenceLoop < targetLoop) return GapValue.ForSeconds(Math.Abs(targetCrossing - referenceCrossing) / 1000d);
            if (referenceCrossing < targetCrossing) return GapValue.ForLaps(1);
            var latestPreviousLoop = reference.PreviousLoops.Keys.DefaultIfEmpty(-1).Max();
            var previousLapStart = reference.CurrentLapStartMilliseconds -
                (latestPreviousLoop >= 0 ? reference.PreviousLoops.GetValueOrDefault(latestPreviousLoop) : 0);
            if (targetLoop >= 0) previousLapStart += reference.PreviousLoops.GetValueOrDefault(targetLoop);
            return GapValue.ForSeconds(Math.Abs(targetCrossing - previousLapStart) / 1000d);
        }
        return GapValue.ForSeconds(Math.Abs(targetCrossing - referenceCrossing) / 1000d);
    }

    private static long CrossingAt(ParsedRow reference, int targetLoop, int referenceLoop)
    {
        if (targetLoop < referenceLoop)
            return reference.CurrentLapStartMilliseconds + reference.CurrentLoops.GetValueOrDefault(targetLoop);
        if (targetLoop > referenceLoop)
        {
            if (reference.PreviousLoops.Count == 0) return reference.CurrentLapStartMilliseconds;
            var previousLapDuration = reference.PreviousLoops.GetValueOrDefault(reference.PreviousLoops.Keys.Max());
            return reference.CurrentLapStartMilliseconds - previousLapDuration + reference.PreviousLoops.GetValueOrDefault(targetLoop);
        }
        return reference.CurrentLapStartMilliseconds + reference.CurrentLoops.GetValueOrDefault(targetLoop);
    }

    private static string? FormatGap(GapValue? value)
    {
        if (value is null) return null;
        if (value.Laps is { } laps) return $"{laps} LAP{(laps == 1 ? "" : "S")}";
        return value.Seconds is { } seconds ? $"+{seconds.ToString("0.000", CultureInfo.InvariantCulture)}" : null;
    }

    private static string? FormatLapTime(int milliseconds)
    {
        if (milliseconds <= 0) return null;
        var time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss\.fff", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static Dictionary<int, long> ParseLoops(string? value)
    {
        Dictionary<int, long> result = [];
        var parts = (value ?? "").Split(';');
        for (var index = 0; index + 1 < parts.Length; index += 2)
            if (int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var key) &&
                long.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset))
                result[key] = offset;
        return result;
    }

    private static JsonNode? ValueById(JsonObject values, string id)
    {
        if (values.TryGetPropertyValue(id, out var exact)) return exact;
        return values.FirstOrDefault(pair => pair.Key.Equals(id, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static string? DriverCode(JsonObject entry)
    {
        var lastName = JsonSupport.String(entry["lastname"]);
        if (string.IsNullOrWhiteSpace(lastName)) return null;
        return lastName[..Math.Min(3, lastName.Length)];
    }

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static long ParseLong(string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static long ParseLongNode(JsonNode? value) => ParseLong(JsonSupport.String(value) ?? "");

    private sealed record ParsedRow(
        int Position,
        string RaceNumber,
        string Classification,
        int Laps,
        long CurrentLapStartMilliseconds,
        string Location,
        int CurrentLapNumber,
        bool IsRunning,
        bool IsCheckered,
        int BestLapMilliseconds,
        Dictionary<int, long> CurrentLoops,
        Dictionary<int, long> PreviousLoops);

    private sealed record GapValue(double? Seconds, int? Laps)
    {
        public static GapValue ForSeconds(double value) => new(value, null);
        public static GapValue ForLaps(int value) => new(null, value);
    }
}
