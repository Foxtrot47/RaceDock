// Copyright (c) 2026 MotorsportTaskbar contributors
// SPDX-License-Identifier: GPL-3.0-or-later
namespace MotorsportTaskbar.Core;

public static class ChampionshipDisplay
{
    public static string CompactEvent(TimingSnapshot snapshot)
    {
        var championship = ShortName(snapshot.Championship);
        var eventName = snapshot.Championship == Championship.WorldRallyChampionship
            ? RemovePrefix(snapshot.Meeting, "Rally ")
            : CompactCircuit(snapshot.Circuit, snapshot.Meeting);
        return Join(championship, eventName, " ");
    }

    public static string FullEvent(TimingSnapshot snapshot) =>
        Join(FullName(snapshot.Championship), snapshot.Meeting, " · ");

    public static string ShortName(Championship championship) => championship switch
    {
        Championship.Formula1 => "F1",
        Championship.Formula2 => "F2",
        Championship.Formula3 => "F3",
        Championship.WorldRallyChampionship => "WRC",
        _ => ""
    };

    public static string FullName(Championship championship) => championship switch
    {
        Championship.Formula1 => "Formula 1",
        Championship.Formula2 => "Formula 2",
        Championship.Formula3 => "Formula 3",
        Championship.WorldRallyChampionship => "World Rally Championship",
        _ => ""
    };

    private static string CompactCircuit(string circuit, string meeting)
    {
        var value = string.IsNullOrWhiteSpace(circuit) ? meeting : circuit;
        value = RemovePrefix(value, "Circuit de ");
        var hyphen = value.IndexOf('-');
        return hyphen > 0 ? value[..hyphen] : value;
    }

    private static string RemovePrefix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;

    private static string Join(string first, string second, string separator) =>
        string.IsNullOrWhiteSpace(first) ? second : string.IsNullOrWhiteSpace(second) ? first : $"{first}{separator}{second}";
}
