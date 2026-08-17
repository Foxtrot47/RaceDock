// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

public static class ChampionshipDisplay
{
    public static string CompactEvent(TimingSnapshot snapshot)
    {
        var championship = ShortName(snapshot.Championship);
        var eventName = snapshot.Championship == Championship.WorldRallyChampionship
            ? RemovePrefix(snapshot.Meeting, "Rally ")
            : CompactCircuit(snapshot.Circuit, snapshot.Meeting);
        eventName = RemovePrefix(eventName, $"{championship} ");
        return Join(championship, eventName, " ");
    }

    public static string FullEvent(TimingSnapshot snapshot) =>
        Join(FullName(snapshot.Championship), snapshot.Meeting, " · ");

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
