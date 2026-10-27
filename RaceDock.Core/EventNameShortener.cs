// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>
/// Converts provider and title-sponsor event names into stable names suitable for compact UI. Raw values
/// remain untouched in timing snapshots and logs. Rules are explicit because deleting unknown words from a
/// proper name is worse than showing a long one.
/// </summary>
public static class EventNameShortener
{
    private static readonly IReadOnlyDictionary<string, string> ExactAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RED Bull Grand Prix of SAN Marino and the Rimini Riviera"] = "San Marino Grand Prix",
            ["Grand Prix of San Marino and the Rimini Riviera"] = "San Marino Grand Prix",
            ["Ueno Rally Del Paraguay"] = "Rally Paraguay",
            ["Ontario Honda Dealers Indy at Markham"] = "Markham Indy",
            ["Ontario Honda Dealers Indy Toronto"] = "Toronto Indy",
            ["Sonsio Grand Prix"] = "Road America Grand Prix",
            ["Bommarito Automotive Group 500"] = "Gateway 500",
            ["Rolex 24 At Daytona"] = "24 Hours of Daytona",
            ["Mobil 1 Twelve Hours of Sebring"] = "12 Hours of Sebring",
            ["Sahlen's Six Hours of The Glen"] = "6 Hours of The Glen",
            ["Motul Petit Le Mans"] = "Petit Le Mans",
            ["Motul SportsCar Endurance Grand Prix"] = "SportsCar Endurance Grand Prix",
            ["FIA WEC - Bapco Energies 8 Hours of Bahrain"] = "8 Hours of Bahrain",
            ["Circuit of the Americas"] = "COTA"
        };

    // Longer and localized markers come first where one title could otherwise match another.
    private static readonly (string Marker, string Name)[] GrandPrixAliases =
    [
        ("GRAN PREMIO DEL MADE IN ITALY E DELL'EMILIA-ROMAGNA", "Emilia-Romagna Grand Prix"),
        ("GRAN PREMIO DELL'EMILIA-ROMAGNA", "Emilia-Romagna Grand Prix"),
        ("GRAN PREMIO DE LA CIUDAD DE MÉXICO", "Mexico City Grand Prix"),
        ("GRAN PREMIO DE BARCELONA-CATALUNYA", "Barcelona-Catalunya Grand Prix"),
        ("GRANDE PRÊMIO DE SÃO PAULO", "São Paulo Grand Prix"),
        ("GRAND PRIX OF SAN MARINO AND THE RIMINI RIVIERA", "San Marino Grand Prix"),
        ("GRAND PRIX OF THE UNITED STATES", "United States Grand Prix"),
        ("GRAND PRIX OF THE AMERICAS", "Americas Grand Prix"),
        ("GRAND PRIX OF GREAT BRITAIN", "British Grand Prix"),
        ("GRAND PRIX DE MONACO", "Monaco Grand Prix"),
        ("GRAN PREMIO DE ESPAÑA", "Spanish Grand Prix"),
        ("GRAND PRIX DU CANADA", "Canadian Grand Prix"),
        ("GRAN PREMIO D’ITALIA", "Italian Grand Prix"),
        ("GRAN PREMIO D'ITALIA", "Italian Grand Prix"),
        ("AUSTRALIAN GRAND PRIX", "Australian Grand Prix"),
        ("CHINESE GRAND PRIX", "Chinese Grand Prix"),
        ("JAPANESE GRAND PRIX", "Japanese Grand Prix"),
        ("BAHRAIN GRAND PRIX", "Bahrain Grand Prix"),
        ("SAUDI ARABIAN GRAND PRIX", "Saudi Arabian Grand Prix"),
        ("MIAMI GRAND PRIX", "Miami Grand Prix"),
        ("EMILIA-ROMAGNA GRAND PRIX", "Emilia-Romagna Grand Prix"),
        ("MONACO GRAND PRIX", "Monaco Grand Prix"),
        ("SPANISH GRAND PRIX", "Spanish Grand Prix"),
        ("CANADIAN GRAND PRIX", "Canadian Grand Prix"),
        ("AUSTRIAN GRAND PRIX", "Austrian Grand Prix"),
        ("BRITISH GRAND PRIX", "British Grand Prix"),
        ("BELGIAN GRAND PRIX", "Belgian Grand Prix"),
        ("HUNGARIAN GRAND PRIX", "Hungarian Grand Prix"),
        ("DUTCH GRAND PRIX", "Dutch Grand Prix"),
        ("ITALIAN GRAND PRIX", "Italian Grand Prix"),
        ("AZERBAIJAN GRAND PRIX", "Azerbaijan Grand Prix"),
        ("SINGAPORE GRAND PRIX", "Singapore Grand Prix"),
        ("UNITED STATES GRAND PRIX", "United States Grand Prix"),
        ("MEXICO CITY GRAND PRIX", "Mexico City Grand Prix"),
        ("SÃO PAULO GRAND PRIX", "São Paulo Grand Prix"),
        ("LAS VEGAS GRAND PRIX", "Las Vegas Grand Prix"),
        ("QATAR GRAND PRIX", "Qatar Grand Prix"),
        ("ABU DHABI GRAND PRIX", "Abu Dhabi Grand Prix"),
        ("MADRID GRAND PRIX", "Madrid Grand Prix"),
        ("GRAND PRIX OF THAILAND", "Thai Grand Prix"),
        ("GRAND PRIX OF BRAZIL", "Brazilian Grand Prix"),
        ("GRAND PRIX OF SPAIN", "Spanish Grand Prix"),
        ("GRAND PRIX OF FRANCE", "French Grand Prix"),
        ("GRAND PRIX OF CATALUNYA", "Catalan Grand Prix"),
        ("GRAND PRIX OF ITALY", "Italian Grand Prix"),
        ("GRAND PRIX OF HUNGARY", "Hungarian Grand Prix"),
        ("GRAND PRIX OF THE CZECH REPUBLIC", "Czech Grand Prix"),
        ("GRAND PRIX OF THE NETHERLANDS", "Dutch Grand Prix"),
        ("GRAND PRIX OF GERMANY", "German Grand Prix"),
        ("GRAND PRIX OF AUSTRIA", "Austrian Grand Prix"),
        ("GRAND PRIX OF ARAGON", "Aragón Grand Prix"),
        ("GRAND PRIX OF JAPAN", "Japanese Grand Prix"),
        ("GRAND PRIX OF INDONESIA", "Indonesian Grand Prix"),
        ("GRAND PRIX OF AUSTRALIA", "Australian Grand Prix"),
        ("GRAND PRIX OF MALAYSIA", "Malaysian Grand Prix"),
        ("GRAND PRIX OF PORTUGAL", "Portuguese Grand Prix"),
        ("GRAND PRIX OF QATAR", "Qatar Grand Prix"),
        ("GRAND PRIX OF VALENCIA", "Valencia Grand Prix"),
        ("GRAND PRIX OF THE VALENCIAN COMMUNITY", "Valencia Grand Prix")
    ];

    private static readonly (string Marker, string Name)[] FormulaEAliases =
    [
        ("MEXICO CITY E-PRIX", "Mexico City E-Prix"),
        ("SÃO PAULO E-PRIX", "São Paulo E-Prix"),
        ("ST. PETERSBURG E-PRIX", "St. Petersburg E-Prix"),
        ("ST PETERSBURG E-PRIX", "St. Petersburg E-Prix"),
        ("MADRID E-PRIX", "Madrid E-Prix"),
        ("JEDDAH E-PRIX", "Jeddah E-Prix"),
        ("MIAMI E-PRIX", "Miami E-Prix"),
        ("MONACO E-PRIX", "Monaco E-Prix"),
        ("TOKYO E-PRIX", "Tokyo E-Prix"),
        ("SHANGHAI E-PRIX", "Shanghai E-Prix"),
        ("JAKARTA E-PRIX", "Jakarta E-Prix"),
        ("BERLIN E-PRIX", "Berlin E-Prix"),
        ("LONDON E-PRIX", "London E-Prix"),
        ("DIRIYAH E-PRIX", "Diriyah E-Prix"),
        ("HYDERABAD E-PRIX", "Hyderabad E-Prix"),
        ("CAPE TOWN E-PRIX", "Cape Town E-Prix"),
        ("PORTLAND E-PRIX", "Portland E-Prix"),
        ("ROME E-PRIX", "Rome E-Prix"),
        ("SEOUL E-PRIX", "Seoul E-Prix")
    ];

    private static readonly (string Marker, string Name)[] RallyAliases =
    [
        ("RALLYE MONTE-CARLO", "Rallye Monte-Carlo"),
        ("SAFARI RALLY KENYA", "Safari Rally Kenya"),
        ("RALLY ISLAS CANARIAS", "Rally Islas Canarias"),
        ("RALLY ITALIA SARDEGNA", "Rally Sardinia"),
        ("ACROPOLIS RALLY", "Acropolis Rally"),
        ("CENTRAL EUROPEAN RALLY", "Central European Rally"),
        ("RALLY SAUDI ARABIA", "Rally Saudi Arabia"),
        ("RALLY DE PORTUGAL", "Rally Portugal"),
        ("RALLY ESTONIA", "Rally Estonia"),
        ("RALLY FINLAND", "Rally Finland"),
        ("RALLY DEL PARAGUAY", "Rally Paraguay"),
        ("RALLY PARAGUAY", "Rally Paraguay"),
        ("RALLY CHILE", "Rally Chile"),
        ("RALLY JAPAN", "Rally Japan"),
        ("RALLY SWEDEN", "Rally Sweden"),
        ("RALLY CROATIA", "Rally Croatia")
    ];

    /// <summary>
    /// Cup race names are long sponsor titles ("YellaWood 500", "EchoPark Automotive Grand Prix"). Only the
    /// rounds whose sponsor name hides the venue are mapped; the rest keep their published name.
    /// </summary>
    private static readonly (string Marker, string Name)[] NascarAliases =
    [
        ("ECHOPARK AUTOMOTIVE GRAND PRIX", "COTA 400"),
        ("FOOD CITY 500", "Bristol 500"),
        ("BANK OF AMERICA ROVAL 400", "Charlotte Roval 400"),
        ("YELLAWOOD 500", "Talladega 500"),
    ];

    private static readonly (string Marker, string Name)[] IndyCarAliases =
    [
        ("GRAND PRIX OF ST. PETERSBURG", "St. Petersburg Grand Prix"),
        ("GRAND PRIX OF ST PETERSBURG", "St. Petersburg Grand Prix"),
        ("GRAND PRIX OF WASHINGTON", "Washington Grand Prix"),
        ("INDY AT MARKHAM", "Markham Indy"),
        ("INDY TORONTO", "Toronto Indy"),
        ("DETROIT GRAND PRIX", "Detroit Grand Prix"),
        ("GRAND PRIX AT ROAD AMERICA", "Road America Grand Prix"),
        ("GRAND PRIX OF MONTEREY", "Monterey Grand Prix"),
        ("GRAND PRIX OF PORTLAND", "Portland Grand Prix"),
        ("INDY GRAND PRIX", "Alabama Grand Prix"),
        ("INDYCAR GRAND PRIX", "Thermal Club Grand Prix"),
        ("INDIANAPOLIS 500", "Indianapolis 500"),
        ("INDY 200 AT MID-OHIO", "Mid-Ohio 200"),
        ("MILWAUKEE MILE 250", "Milwaukee Mile 250")
    ];

    private static readonly (string Marker, string Name)[] ImsaAliases =
    [
        ("ROLEX 24 AT DAYTONA", "24 Hours of Daytona"),
        ("TWELVE HOURS OF SEBRING", "12 Hours of Sebring"),
        ("GRAND PRIX OF LONG BEACH", "Long Beach Grand Prix"),
        ("COURSE DE MONTEREY", "Monterey SportsCar Grand Prix"),
        ("DETROIT SPORTS CAR CLASSIC", "Detroit Sports Car Classic"),
        ("SIX HOURS OF THE GLEN", "6 Hours of The Glen"),
        ("BATTLE ON THE BRICKS", "Battle on the Bricks"),
        ("PETIT LE MANS", "Petit Le Mans"),
        ("SPORTSCAR ENDURANCE GRAND PRIX", "SportsCar Endurance Grand Prix")
    ];

    public static string Shorten(Championship championship, string eventName)
    {
        var value = StripYear(CollapseWhitespace(eventName));
        if (ExactAliases.TryGetValue(value, out var exact)) return exact;
        return championship switch
        {
            Championship.Formula1 or Championship.Formula2 or Championship.Formula3 =>
                Match(value, GrandPrixAliases) ?? value,
            Championship.MotoGp or Championship.Moto2 or Championship.Moto3 =>
                Match(value, GrandPrixAliases) ?? value,
            Championship.FormulaE => Match(value, FormulaEAliases) ?? value,
            Championship.WorldRallyChampionship => Match(value, RallyAliases) ?? value,
            Championship.IndyCar => Match(value, IndyCarAliases) ?? value,
            Championship.NascarCup or Championship.NascarXfinity or Championship.NascarTrucks => Match(value, NascarAliases) ?? value,
            Championship.Imsa => Match(value, ImsaAliases) ?? value,
            Championship.GtWorldChallengeEurope => ShortenGtWorldChallenge(value),
            Championship.WorldEnduranceChampionship => StripPrefix(value, "FIA WEC - "),
            Championship.EuropeanLeMansSeries => StripPrefix(value, "ELMS - "),
            Championship.MichelinLeMansCup => StripPrefix(value, "MLMC - "),
            Championship.Dtm => StripDtmRound(value),
            _ => value
        };
    }

    private static string? Match(string value, IEnumerable<(string Marker, string Name)> aliases) =>
        aliases.FirstOrDefault(alias => value.Contains(alias.Marker, StringComparison.OrdinalIgnoreCase)).Name;

    private static string ShortenGtWorldChallenge(string value)
    {
        foreach (var prefix in new[] { "GT WorldChEU pwrd by AWS ", "GT World Challenge Europe powered by AWS " })
        {
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            return value[prefix.Length..].Replace(" Round ", " · Round ", StringComparison.OrdinalIgnoreCase);
        }
        return value.Replace(" powered by AWS", "", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripDtmRound(string value)
    {
        var separator = value.IndexOf(' ');
        return separator > 0 && int.TryParse(value[..separator], out _) ? value[(separator + 1)..] : value;
    }

    private static string StripPrefix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;

    private static string StripYear(string value)
    {
        if (value.Length < 4) return value;
        var suffix = value[^4..];
        return int.TryParse(suffix, out var year) && year is >= 2000 and <= 2099
            ? value[..^4].TrimEnd(' ', '-', '·')
            : value;
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
