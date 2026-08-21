// SPDX-License-Identifier: GPL-3.0-or-later
using RaceDock.Core;

namespace RaceDock.App.Settings;

internal sealed record ChampionshipSettingsPresentation
{
    public required string TaskbarCountTitle { get; init; }
    public required string TaskbarCountDescription { get; init; }
    public required string ManufacturerTitle { get; init; }
    public required string TaskbarManufacturerDescription { get; init; }
    public required string FlyoutCountTitle { get; init; }
    public required string FlyoutCountDescription { get; init; }
    public required string TeamTitle { get; init; }
    public required string TeamDescription { get; init; }
    public required string FlyoutManufacturerDescription { get; init; }
    public required string LeaderGapDescription { get; init; }
    public required string IntervalDescription { get; init; }
    public required string ResultTitle { get; init; }
    public required string ResultDescription { get; init; }
    public required string CategoryTitle { get; init; }
    public required string CategoryDescription { get; init; }
    public required string StatusDescription { get; init; }
    public bool ShowManufacturerSettings { get; init; } = true;
    public bool ShowTeamSetting { get; init; } = true;
    /// <summary>Whether this championship's feed carries a tyre compound worth a column.</summary>
    public bool ShowResultSetting { get; init; } = true;
    public string LapTimeTitle { get; init; } = "Lap time";
    public string LapTimeDescription { get; init; } =
        "Show the best lap in practice and qualifying, and the last lap in sprints and races.";

    public static ChampionshipSettingsPresentation For(Championship championship) => championship switch
    {
        Championship.Formula1 => Circuit("Formula 1", "drivers", "Driver", "constructor", showManufacturer: true, showTyres: true),
        Championship.Formula2 => Circuit("Formula 2", "drivers", "Driver", "team", showManufacturer: false, showTyres: false),
        Championship.Formula3 => Circuit("Formula 3", "drivers", "Driver", "team", showManufacturer: false, showTyres: false),
        Championship.MotoGp => Moto("MotoGP"),
        Championship.Moto2 => Moto("Moto2"),
        Championship.Moto3 => Moto("Moto3"),
        Championship.WorldRallyChampionship => Rally(),
        Championship.GtWorldChallengeEurope => Endurance("GT World Challenge Europe"),
        Championship.Imsa => Endurance("IMSA WeatherTech Championship"),
        Championship.FormulaE => FormulaE(),
        Championship.Dtm => Dtm(),
        Championship.IndyCar => IndyCar(),
        _ => Circuit("the championship", "competitors", "Competitor", "team", showManufacturer: false, showTyres: false)
    };

    private static ChampionshipSettingsPresentation Circuit(
        string championship,
        string competitors,
        string competitor,
        string organization,
        bool showManufacturer,
        bool showTyres) => Common(
            competitors,
            competitor,
            organization == "constructor" ? "Constructor logos" : "Manufacturer logos",
            $"Replace position cards with {organization} marks when available.",
            "Team names",
            $"Show each {competitor.ToLowerInvariant()}'s {organization} or team.",
            $"Show {organization} marks beside drivers.",
            "Tyre compound",
            "Show each driver's current tyre compound.",
            "Lap",
            "Show each driver's completed lap count.",
            "Show pit, running, retirement, and classification status.") with
        {
            ShowManufacturerSettings = showManufacturer,
            ShowResultSetting = showTyres
        };

    private static ChampionshipSettingsPresentation FormulaE() => Common(
        "drivers",
        "Driver",
        "Team logos",
        "Replace position cards with the entrant badges used by Formula E live timing.",
        "Team names",
        "Show each driver's Formula E team.",
        "Show Formula E team badges beside drivers.",
        "Best lap",
        "Show each driver's best lap time.",
        "Lap",
        "Show each driver's completed lap count.",
        "Show pit, running, retirement, and classification status.");

    private static ChampionshipSettingsPresentation Dtm() => Common(
        "drivers",
        "Driver",
        "Manufacturer logos",
        "Replace position cards with manufacturer marks when available.",
        "Team names",
        "Show each driver's DTM team.",
        "Show manufacturer marks beside drivers.",
        "Best lap",
        "Show each driver's best lap time.",
        "Lap",
        "Show each driver's completed lap count.",
        "Show pit, running, retirement, and classification status.");

    private static ChampionshipSettingsPresentation IndyCar() => Common(
        "drivers",
        "Driver",
        "Team logos",
        "Replace position cards with official INDYCAR entrant badges when available.",
        "Team names",
        "Show each driver's entrant team.",
        "Show INDYCAR entrant badges beside drivers.",
        "Best lap",
        "Show each driver's best lap time.",
        "Lap",
        "Show each driver's completed lap count.",
        "Show pit, running, retirement, and classification status.");

    private static ChampionshipSettingsPresentation Moto(string championship) => Common(
        "riders",
        "Rider",
        "Manufacturer logos",
        "Replace position cards with motorcycle manufacturer marks when available.",
        "Team names",
        "Show each rider's team.",
        "Show motorcycle manufacturer marks beside riders.",
        "Lap time",
        "Show each rider's lap time when supplied by live timing.",
        "Lap",
        "Show each rider's completed lap count.",
        "Show pit, running, retirement, and classification status.") with
        {
            ShowResultSetting = false
        };

    private static ChampionshipSettingsPresentation Rally() => Common(
        "crews",
        "Crew",
        "Manufacturer logos",
        "Replace position cards with vehicle manufacturer marks when available.",
        "Team names",
        "Show each crew's registered team.",
        "Show vehicle manufacturer marks beside crews.",
        "Stage time",
        "Show each crew's elapsed stage time.",
        "Rally class",
        "Show each crew's WRC category.",
        "Show running, retired, and classified crew status.") with
        {
            ShowTeamSetting = false
        };

    private static ChampionshipSettingsPresentation Endurance(string championship) => Common(
        "entries",
        "Entry",
        "Manufacturer logos",
        "Replace position cards with vehicle manufacturer marks when available.",
        "Team names",
        "Show the team for each classified entry.",
        "Show vehicle manufacturer marks beside entries.",
        "Best lap",
        "Show the best lap time for each entry.",
        "Class",
        "Show the competition class for each entry.",
        "Show pit, running, retired, and classification status.");

    private static ChampionshipSettingsPresentation Common(
        string competitors,
        string competitor,
        string manufacturerTitle,
        string taskbarManufacturerDescription,
        string teamTitle,
        string teamDescription,
        string flyoutManufacturerDescription,
        string resultTitle,
        string resultDescription,
        string categoryTitle,
        string categoryDescription,
        string statusDescription) => new()
        {
            TaskbarCountTitle = $"Visible {competitors}",
            TaskbarCountDescription = $"Number of leading {competitors} shown in the widget.",
            ManufacturerTitle = manufacturerTitle,
            TaskbarManufacturerDescription = taskbarManufacturerDescription,
            FlyoutCountTitle = $"Maximum {competitors}",
            FlyoutCountDescription = $"Limit the classification to the leading {competitors}.",
            TeamTitle = teamTitle,
            TeamDescription = teamDescription,
            FlyoutManufacturerDescription = flyoutManufacturerDescription,
            LeaderGapDescription = $"Show each {competitor.ToLowerInvariant()}'s gap to the leader.",
            IntervalDescription = $"Show the interval to the {competitor.ToLowerInvariant()} immediately ahead.",
            ResultTitle = resultTitle,
            ResultDescription = resultDescription,
            CategoryTitle = categoryTitle,
            CategoryDescription = categoryDescription,
            StatusDescription = statusDescription
        };
}
