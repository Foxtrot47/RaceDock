// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

public static class ChampionshipDisplay
{
    /// <summary>Canonical event name for user-facing surfaces; raw feed values remain untouched.</summary>
    public static string ShortEventName(Championship championship, string eventName) =>
        EventNameShortener.Shorten(championship, eventName);

    /// <summary>Compatibility overload for callers that do not know which series supplied the name.</summary>
    public static string ShortEventName(string eventName) => EventNameShortener.Shorten(Championship.Unknown, eventName);

    public static string CompactEvent(TimingSnapshot snapshot)
    {
        var championship = ShortName(snapshot.Championship);
        var eventName = snapshot.Championship == Championship.WorldRallyChampionship
            ? RemovePrefix(ShortEventName(snapshot.Championship, snapshot.Meeting), "Rally ")
            : CompactCircuit(snapshot.Circuit, snapshot.Meeting);
        eventName = RemovePrefix(eventName, $"{championship} ");
        return Join(championship, eventName, " ");
    }

    public static string FullEvent(TimingSnapshot snapshot) =>
        Join(FullName(snapshot.Championship), ShortEventName(snapshot.Championship, snapshot.Meeting), " · ");

    public static string CompactCategory(Championship championship, string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "—";
        return championship == Championship.WorldEnduranceChampionship &&
            category.Equals("HYPERCAR", StringComparison.OrdinalIgnoreCase) ? "HY" : category;
    }

    /// <summary>
    /// Practice and qualifying are judged on the best lap set; sprints and races on the most recent
    /// lap. Qualifying wins over sprint so "Sprint Qualifying" reports a best lap. Rally stages keep
    /// their own stage times and are never reclassified.
    /// </summary>
    public static LapTimeMode LapTimeModeFor(TimingSnapshot snapshot)
    {
        if (snapshot.Championship == Championship.WorldRallyChampionship) return LapTimeMode.BestLap;
        var session = snapshot.Session ?? "";
        if (Mentions(session, "qualif", "practice", "fp", "shakedown", "warm")) return LapTimeMode.BestLap;
        if (Mentions(session, "race", "sprint", "grand prix", "e-prix", "eprix", "feature", "heat", "final"))
            return LapTimeMode.LastLap;
        return LapTimeMode.BestLap;
    }

    private static bool Mentions(string session, params string[] tokens) =>
        tokens.Any(token => session.Contains(token, StringComparison.OrdinalIgnoreCase));

    public static string ShortName(Championship championship) => championship switch
    {
        Championship.Formula1 => "F1",
        Championship.Formula2 => "F2",
        Championship.Formula3 => "F3",
        Championship.MotoGp => "MotoGP",
        Championship.Moto2 => "Moto2",
        Championship.Moto3 => "Moto3",
        Championship.WorldRallyChampionship => "WRC",
        Championship.GtWorldChallengeEurope => "GTWC",
        Championship.WorldEnduranceChampionship => "WEC",
        Championship.EuropeanLeMansSeries => "ELMS",
        Championship.MichelinLeMansCup => "Le Mans Cup",
        Championship.Imsa => "IMSA",
        Championship.FormulaE => "FE",
        Championship.Dtm => "DTM",
        Championship.IndyCar => "INDYCAR",
        _ => ""
    };

    public static string FullName(Championship championship) => championship switch
    {
        Championship.Formula1 => "Formula 1",
        Championship.Formula2 => "Formula 2",
        Championship.Formula3 => "Formula 3",
        Championship.MotoGp => "MotoGP",
        Championship.Moto2 => "Moto2",
        Championship.Moto3 => "Moto3",
        Championship.WorldRallyChampionship => "World Rally Championship",
        Championship.GtWorldChallengeEurope => "GT World Challenge Europe",
        Championship.WorldEnduranceChampionship => "World Endurance Championship",
        Championship.EuropeanLeMansSeries => "European Le Mans Series",
        Championship.MichelinLeMansCup => "Michelin Le Mans Cup",
        Championship.Imsa => "IMSA WeatherTech SportsCar Championship",
        Championship.FormulaE => "Formula E",
        Championship.Dtm => "DTM",
        Championship.IndyCar => "NTT INDYCAR SERIES",
        _ => ""
    };

    private static string CompactCircuit(string circuit, string meeting)
    {
        var value = string.IsNullOrWhiteSpace(circuit) ? meeting : circuit;
        value = RemovePrefix(value, "Circuit de ");
        if (value.EndsWith(" Circuit", StringComparison.OrdinalIgnoreCase)) value = value[..^8];
        var hyphen = value.IndexOf('-');
        return hyphen > 0 ? value[..hyphen] : value;
    }

    private static string RemovePrefix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;

    private static string Join(string first, string second, string separator) =>
        string.IsNullOrWhiteSpace(first) ? second : string.IsNullOrWhiteSpace(second) ? first : $"{first}{separator}{second}";
}
