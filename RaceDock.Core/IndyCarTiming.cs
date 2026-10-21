// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal static class IndyCarTiming
{
    public static TimingSnapshot BuildSnapshot(JsonObject payload, DateTimeOffset now, ConnectionState connection)
    {
        if (payload["timing_results"] is not JsonObject results)
        {
            FeedDrift.Record("INDYCAR", "timing_results", payload);
            results = [];
        }
        var heartbeat = results["heartbeat"] as JsonObject ?? [];
        var items = results["Item"] as JsonArray;
        List<CompetitorStanding> standings = [];
        if (items is not null)
        {
            var fastestId = items.OfType<JsonObject>()
                .Select(item => (Id: JsonSupport.String(item["DriverID"]), Time: ParseLapTime(JsonSupport.String(item["bestLapTime"]))))
                .Where(value => value.Id is not null && value.Time is not null)
                .OrderBy(value => value.Time)
                .Select(value => value.Id)
                .FirstOrDefault();

            foreach (var item in items.OfType<JsonObject>())
            {
                var position = JsonSupport.Int(item["rank"]) ?? 0;
                if (position <= 0) continue;
                var number = JsonSupport.String(item["no"]) ?? position.ToString(CultureInfo.InvariantCulture);
                var firstName = JsonSupport.String(item["firstName"]) ?? "";
                var lastName = JsonSupport.String(item["lastName"]) ?? "";
                var name = string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
                var status = JsonSupport.String(item["status"]) ?? "";
                var marker = JsonSupport.String(item["marker"]) ?? "";
                var driverId = JsonSupport.String(item["DriverID"]) ?? number;
                standings.Add(new(
                    driverId,
                    position,
                    DriverCode(lastName, number),
                    string.IsNullOrWhiteSpace(name) ? number : name,
                    JsonSupport.String(item["team"]) ?? "",
                    position == 1 ? "LEAD" : FormatGap(JsonSupport.String(item["diff"])),
                    position == 1 ? null : FormatGap(JsonSupport.String(item["gap"])),
                    NullIfEmpty(JsonSupport.String(item["Tire"])),
                    JsonSupport.Int(item["laps"]) ?? 0,
                    marker.Contains("InPit", StringComparison.OrdinalIgnoreCase),
                    IsRetired(status),
                    status.Contains("Stopped", StringComparison.OrdinalIgnoreCase),
                    driverId == fastestId,
                    NullIfEmpty(JsonSupport.String(item["bestLapTime"])),
                    StatusLabel(status),
                    null,
                    null,
                    Manufacturer(JsonSupport.String(item["equipment"]))));
            }
        }

        standings = [.. standings.OrderBy(value => value.Position).ThenBy(value => value.DriverId, StringComparer.Ordinal)];
        var statusValue = (JsonSupport.String(heartbeat["SessionStatus"]) ?? "").Trim().ToUpperInvariant();
        var flagValue = (JsonSupport.String(heartbeat["currentFlag"]) ?? "").Trim().ToUpperInvariant();
        var isIndyCar = string.Equals(JsonSupport.String(heartbeat["Series"]), "I", StringComparison.OrdinalIgnoreCase);
        var lifecycle = !isIndyCar ? SessionLifecycle.OffSession
            : IsEndedStatus(statusValue, flagValue) ? SessionLifecycle.Ended
            : IsLiveStatus(statusValue, flagValue) && standings.Count > 0 ? SessionLifecycle.Live
            : SessionLifecycle.OffSession;
        var currentLap = JsonSupport.Int(heartbeat["lapNumber"]) ?? standings.Select(value => value.Lap).DefaultIfEmpty(0).Max();
        var totalLaps = JsonSupport.Int(heartbeat["totalLaps"]);
        return new(
            JsonSupport.String(heartbeat["eventName"]) ?? "NTT INDYCAR SERIES",
            JsonSupport.String(heartbeat["SessionName"]) ?? JsonSupport.String(heartbeat["EventSessionLabel"]) ?? "Live Session",
            JsonSupport.String(heartbeat["trackName"]) ?? "",
            currentLap,
            totalLaps is > 0 ? totalLaps : null,
            TrackState(flagValue),
            standings,
            now,
            lifecycle,
            connection,
            TimeRemaining(JsonSupport.String(heartbeat["overallTimeToGo"])),
            Championship.IndyCar);
    }

    internal static string? SessionKey(JsonObject payload)
    {
        var heartbeat = payload["timing_results"]?["heartbeat"];
        return JsonSupport.String(heartbeat?["EventSessionID"]) ?? JsonSupport.String(heartbeat?["SessionName"]);
    }

    private static bool IsLiveStatus(string status, string flag) =>
        status is "GREEN" or "YELLOW" or "RED" or "WHITE" or "RUNNING" or "OPEN" ||
        flag is "GREEN" or "YELLOW" or "RED" or "WHITE";

    private static bool IsEndedStatus(string status, string flag) =>
        status is "CHECKERED" or "CHEQUERED" or "COMPLETE" or "COMPLETED" or "FINISHED" or "OFFICIAL" or "UNOFFICIAL" ||
        flag is "CHECKERED" or "CHEQUERED";

    /// <summary>
    /// IndyCar publishes a single whole-track flag, so its caution is a full-course yellow rather than a
    /// sector yellow like the F1-style feeds carry.
    /// </summary>
    internal static TrackCondition TrackState(string value) => value switch
    {
        "GREEN" or "WHITE" => TrackCondition.AllClear,
        "YELLOW" or "CAUTION" or "FCY" => TrackCondition.FullCourseYellow,
        "RED" => TrackCondition.RedFlag,
        _ => TrackCondition.Unknown
    };

    private static string? TimeRemaining(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split(':');
        return parts.Length switch
        {
            2 when int.TryParse(parts[0], out var minutes) && int.TryParse(parts[1], out var seconds) =>
                $"{minutes / 60:00}:{minutes % 60:00}:{seconds:00}",
            3 when parts.All(part => int.TryParse(part, out _)) =>
                $"{int.Parse(parts[0], CultureInfo.InvariantCulture):00}:{int.Parse(parts[1], CultureInfo.InvariantCulture):00}:{int.Parse(parts[2], CultureInfo.InvariantCulture):00}",
            _ => value
        };
    }

    private static string? FormatGap(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "-" or "--") return null;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return $"+{seconds.ToString("0.0000", CultureInfo.InvariantCulture)}";
        return value.StartsWith('+') ? value : $"+{value}";
    }

    private static TimeSpan? ParseLapTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var minutes) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)) return null;
        return TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
    }

    private static string DriverCode(string lastName, string fallback)
    {
        var code = new string(lastName.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(code) ? fallback : code;
    }

    private static string? Manufacturer(string? equipment)
    {
        var code = equipment?.Split('/').ElementAtOrDefault(1)?.Trim().ToUpperInvariant();
        return code switch
        {
            "H" => "Honda",
            "C" => "Chevrolet",
            _ => null
        };
    }

    private static bool IsRetired(string status) => status.Contains("Retired", StringComparison.OrdinalIgnoreCase) ||
        status.Contains("Out", StringComparison.OrdinalIgnoreCase) || status.Equals("DNF", StringComparison.OrdinalIgnoreCase);

    private static string? StatusLabel(string status) => status.ToUpperInvariant() switch
    {
        "RETIRED" or "OUT" or "DNF" => "RET",
        "DNS" or "DID NOT START" => "N/S",
        "DISQUALIFIED" or "DSQ" => "DSQ",
        "STOPPED" => "STOP",
        _ => null
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
