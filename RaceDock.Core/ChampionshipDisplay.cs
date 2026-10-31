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

    public static string? ShortVenue(string? venue) =>
        venue?.Trim().Equals("Circuit of the Americas", StringComparison.OrdinalIgnoreCase) == true
            ? "COTA"
            : venue?.Trim();

    public static string CompactEvent(TimingSnapshot snapshot)
    {
        var championship = ShortName(snapshot.Championship);
        // The widget names the event, not the circuit: "F1 United States Grand Prix" says which race this is,
        // while the circuit is one line further down and shared by every session of the weekend.
        var eventName = ShortEventName(snapshot.Championship, snapshot.Meeting);
        if (snapshot.Championship == Championship.WorldRallyChampionship) eventName = RemovePrefix(eventName, "Rally ");
        eventName = RemovePrefix(eventName, $"{championship} ");
        return Join(championship, AbbreviateGrandPrix(eventName), " ");
    }

    /// <summary>
    /// A taskbar header has no room to spell out "Grand Prix", and the flyout below it still says the event in full,
    /// so the compact line shortens the suffix instead of the event name.
    /// </summary>
    private static string AbbreviateGrandPrix(string value) =>
        value.EndsWith(" Grand Prix", StringComparison.OrdinalIgnoreCase) ? value[..^" Grand Prix".Length] + " GP" : value;

    public static string FullEvent(TimingSnapshot snapshot) =>
        Join(FullName(snapshot.Championship), ShortEventName(snapshot.Championship, snapshot.Meeting), " · ");

    /// <summary>
    /// Shared-entry championships identify the car as well as its active driver in the flyout, and NASCAR fans
    /// know an entry by its number first.
    /// </summary>
    public static string FlyoutDriverName(Championship championship, CompetitorStanding standing)
    {
        var name = string.IsNullOrWhiteSpace(standing.Name) ? standing.Code : standing.Name;
        if (championship is not (Championship.WorldEnduranceChampionship or Championship.Imsa or
            Championship.GtWorldChallengeEurope or Championship.EuropeanLeMansSeries or Championship.MichelinLeMansCup or
            Championship.NascarCup or Championship.NascarXfinity or Championship.NascarTrucks))
            return name;
        if (string.IsNullOrWhiteSpace(standing.CarNumber)) return name;
        var number = standing.CarNumber.Trim().TrimStart('#');
        if (number.Length == 0) return name;
        return name == number || name == "#" + number ? $"#{number}" : $"#{number}  {name}";
    }

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
        Championship.F1Academy => "F1A",
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
        Championship.NascarCup => "NASCAR Cup",
        Championship.NascarXfinity => "NASCAR Xfinity",
        Championship.NascarTrucks => "NASCAR Trucks",
        _ => ""
    };

    public static string FullName(Championship championship) => championship switch
    {
        Championship.Formula1 => "Formula 1",
        Championship.Formula2 => "Formula 2",
        Championship.Formula3 => "Formula 3",
        Championship.F1Academy => "F1 Academy",
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
        Championship.NascarCup => "NASCAR Cup Series",
        Championship.NascarXfinity => "NASCAR Xfinity Series",
        Championship.NascarTrucks => "NASCAR CRAFTSMAN Truck Series",
        _ => ""
    };

    private static string RemovePrefix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;

    private static string Join(string first, string second, string separator) =>
        string.IsNullOrWhiteSpace(first) ? second : string.IsNullOrWhiteSpace(second) ? first : $"{first}{separator}{second}";
}
