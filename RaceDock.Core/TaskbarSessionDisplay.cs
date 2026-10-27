// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.RegularExpressions;

namespace RaceDock.Core;

/// <summary>Compact session labels and progress shared by the taskbar layouts.</summary>
public static partial class TaskbarSessionDisplay
{
    private readonly record struct SessionLabel(string Code, bool UsesClock);

    private static readonly IReadOnlyDictionary<string, SessionLabel> Labels =
        new Dictionary<string, SessionLabel>(StringComparer.OrdinalIgnoreCase)
        {
            ["Free Practice"] = new("FP", true),
            ["Practice"] = new("FP", true),
            ["FP"] = new("FP", true),
            ["PR"] = new("FP", true),
            ["P"] = new("FP", true),
            ["Qualifying"] = new("Q", true),
            ["Q"] = new("Q", true),
            ["Sprint Qualifying"] = new("SQ", true),
            ["Sprint Shootout"] = new("SQ", true),
            ["SQ"] = new("SQ", true),
            ["Combined Qualifying"] = new("CQ", true),
            ["CQ"] = new("CQ", true),
            ["Qualifying Duel Quarter-Final"] = new("QF", true),
            ["Qualifying Duel Semi-Final"] = new("SF", true),
            ["Qualifying Duel Final"] = new("FIN", true),
            ["Hyperpole"] = new("HP", true),
            ["Warmup"] = new("WUP", true),
            ["Warm Up"] = new("WUP", true),
            ["Warm-Up"] = new("WUP", true),
            ["WUP"] = new("WUP", true),
            ["Race"] = new("RAC", false),
            ["RAC"] = new("RAC", false),
            ["R"] = new("RAC", false),
            ["Sprint"] = new("SPR", false),
            ["Sprint Race"] = new("SPR", false),
            ["SPR"] = new("SPR", false),
            ["Feature Race"] = new("FEA", false)
        };

    public static string Progress(TimingSnapshot snapshot)
    {
        if (IsRally(snapshot)) return snapshot.Session;
        var label = Label(snapshot);
        // The Cup feed publishes no session countdown, so a timed session reports its code alone rather
        // than a placeholder clock.
        if (label.UsesClock && IsNascar(snapshot.Championship)) return label.Code;
        return UsesClock(snapshot, label)
            ? $"{label.Code} {RemainingTime(snapshot.TimeRemaining)}"
            : LapProgress(snapshot, label.Code);
    }

    private static bool IsRally(TimingSnapshot snapshot) =>
        snapshot.Championship == Championship.WorldRallyChampionship ||
        snapshot.Meeting.Contains("Rally", StringComparison.OrdinalIgnoreCase) ||
        snapshot.Session.StartsWith("SS", StringComparison.OrdinalIgnoreCase) ||
        snapshot.Session.StartsWith("SHAKEDOWN", StringComparison.OrdinalIgnoreCase);

    private static bool UsesClock(TimingSnapshot snapshot, SessionLabel label)
    {
        if (label.UsesClock || IsEndurance(snapshot.Championship)) return true;
        if (snapshot.TotalLaps is > 0 || IsLapBased(snapshot.Championship)) return false;
        // DTM and historical Formula E sessions can be time-limited without a lap target.
        return snapshot.Championship is Championship.Dtm or Championship.FormulaE ||
            !string.IsNullOrWhiteSpace(snapshot.TimeRemaining);
    }

    private static bool IsEndurance(Championship championship) => championship is
        Championship.WorldEnduranceChampionship or Championship.EuropeanLeMansSeries or
        Championship.MichelinLeMansCup or Championship.Imsa or Championship.GtWorldChallengeEurope;

    private static bool IsLapBased(Championship championship) => championship is
        Championship.Formula1 or Championship.Formula2 or Championship.Formula3 or
        Championship.MotoGp or Championship.Moto2 or Championship.Moto3 or Championship.IndyCar or
        Championship.NascarCup or Championship.NascarXfinity or Championship.NascarTrucks;

    private static bool IsNascar(Championship championship) => championship is
        Championship.NascarCup or Championship.NascarXfinity or Championship.NascarTrucks;

    private static string LapProgress(TimingSnapshot snapshot, string code)
    {
        var lap = Math.Max(0, snapshot.CurrentLap);
        var progress = snapshot.TotalLaps is > 0 ? $"{code} {lap}/{snapshot.TotalLaps}"
            : lap > 0 ? $"{code} {lap}" : code;
        return StageSuffix(snapshot) is { } stage ? $"{progress} · {stage}" : progress;
    }

    /// <summary>The stage being run as a short suffix, absent whenever the feed publishes no stage.</summary>
    private static string? StageSuffix(TimingSnapshot snapshot) =>
        snapshot.StageNumber is > 0 and var number ? $"S{number}" : null;

    private static SessionLabel Label(TimingSnapshot snapshot)
    {
        if (snapshot.Championship is Championship.MotoGp or Championship.Moto2 or Championship.Moto3)
        {
            var shortLabel = ParseLabel(snapshot.SessionShortName ?? "");
            if (shortLabel.Code != "LIVE") return shortLabel;
        }
        return ParseLabel(StripMotorcycleCategory(snapshot.Session));
    }

    private static string StripMotorcycleCategory(string session)
    {
        var name = session.Trim();
        foreach (var prefix in new[] { "MotoGP ", "Moto GP ", "Moto2 ", "Moto3 " })
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return name[prefix.Length..];
        return name;
    }

    private static SessionLabel ParseLabel(string session)
    {
        var name = Whitespace().Replace(session.Trim(), " ");
        const string allCars = " - All Cars";
        if (name.EndsWith(allCars, StringComparison.OrdinalIgnoreCase)) name = name[..^allCars.Length];
        var parts = SessionParts().Match(name);
        if (!Labels.TryGetValue(parts.Groups["name"].Value, out var label)) return FallbackLabel(name);
        var number = parts.Groups["number"].Value;
        var code = label.Code;
        if (code == "RAC" && number.Length > 0) code = "R";
        return label with { Code = code + SessionSuffix(parts) };
    }

    private static SessionLabel FallbackLabel(string name)
    {
        // A class or group suffix must not turn a timed session into lap progress.
        foreach (var entry in Labels.OrderByDescending(entry => entry.Key.Length))
            if (name.StartsWith(entry.Key + " ", StringComparison.OrdinalIgnoreCase)) return entry.Value;
        return new("LIVE", false);
    }

    private static string SessionSuffix(Match parts)
    {
        var number = parts.Groups["number"].Value;
        var group = parts.Groups["group"].Value.ToUpperInvariant();
        if (group.Length == 0) return number;
        return number.Length > 0 ? number + group : "G" + group;
    }

    private static string RemainingTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "—";
        var parts = value.Trim().Split(':');
        if (parts.Length is not (2 or 3)) return "—";
        if (!parts.All(part => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _))) return "—";
        var hours = parts.Length == 3 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : 0;
        var minutes = int.Parse(parts[^2], CultureInfo.InvariantCulture);
        var seconds = int.Parse(parts[^1], CultureInfo.InvariantCulture);
        if (seconds >= 60 || (parts.Length == 3 && minutes >= 60)) return "—";
        var totalMinutes = (long)hours * 60 + minutes;
        return totalMinutes >= 60
            ? FormattableString.Invariant($"{totalMinutes / 60}:{totalMinutes % 60:00}:{seconds:00}")
            : FormattableString.Invariant($"{totalMinutes:00}:{seconds:00}");
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^(?<name>.+?)(?:\s*(?:Nr\.?\s*)?(?<number>[1-9][0-9]*))?(?:\s+Group\s+(?<group>[A-Z]))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SessionParts();
}
