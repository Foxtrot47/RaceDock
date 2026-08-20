// SPDX-License-Identifier: GPL-3.0-or-later
using RaceDock.Core;

namespace RaceDock.App.Settings;

internal sealed record ChampionshipSettingsPresentation
{
    public required string TaskbarCountTitle { get; init; }
    public required string TaskbarCountDescription { get; init; }
    public required string CompetitorLabelTitle { get; init; }
    public required string CompetitorLabelDescription { get; init; }
    public required string CodeOption { get; init; }
    public required string FullNameOption { get; init; }
    public required string NumberOption { get; init; }
    public required string TaskbarEventDescription { get; init; }
    public required string TaskbarProgressDescription { get; init; }
    public required string TaskbarGapDescription { get; init; }
    public required string PositionMarkerDescription { get; init; }
    public required string ManufacturerTitle { get; init; }
    public required string TaskbarManufacturerDescription { get; init; }
    public required string FlagTitle { get; init; }
    public required string FlagDescription { get; init; }
    public required string FlyoutCountTitle { get; init; }
    public required string FlyoutCountDescription { get; init; }
    public required string FlyoutEventDescription { get; init; }
    public required string SessionDetailsDescription { get; init; }
    public required string TrackStatusTitle { get; init; }
    public required string TrackStatusDescription { get; init; }
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
            "Driver code",
            "Driver name",
            "Car number",
            $"Show {championship} and the event venue.",
            "Show the session, remaining time, and lap count.",
            $"Show the gap to the leader below each {competitor.ToLowerInvariant()}.",
            showManufacturer ? $"Show a position card or {organization} logo." : "Show a position-number card.",
            organization == "constructor" ? "Constructor logos" : "Manufacturer logos",
            $"Replace position cards with {organization} marks when available.",
            "Race-control alerts",
            "Show flag and race-control alerts beside the event name.",
            $"Show {championship} and the event title.",
            "Show the session, circuit, remaining time, and laps.",
            "Track status",
            "Show the current circuit-condition badge.",
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
        "Driver code",
        "Driver name",
        "Car number",
        "Show Formula E and the E-Prix venue.",
        "Show the session, remaining time, and lap count.",
        "Show the gap to the leader below each driver.",
        "Show a position card or Formula E team logo.",
        "Team logos",
        "Replace position cards with the entrant badges used by Formula E live timing.",
        "Race-control alerts",
        "Show flag and race-control alerts beside the E-Prix name.",
        "Show Formula E and the E-Prix title.",
        "Show the session, circuit, remaining time, and laps.",
        "Track status",
        "Show the current circuit-condition badge.",
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
        "Driver code",
        "Driver name",
        "Car number",
        "Show DTM and the event venue.",
        "Show the session, remaining time, and lap count.",
        "Show the gap to the leader below each driver.",
        "Show a position card or manufacturer logo.",
        "Manufacturer logos",
        "Replace position cards with manufacturer marks when available.",
        "Race-control alerts",
        "Show flag and race-control alerts beside the event name.",
        "Show DTM and the event title.",
        "Show the session, circuit, remaining time, and laps.",
        "Track status",
        "Show the current circuit-condition badge.",
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
        "Driver code",
        "Driver name",
        "Car number",
        "Show INDYCAR and the event venue.",
        "Show the session, remaining time, and lap count.",
        "Show the gap to the leader below each driver.",
        "Show a position card or INDYCAR entrant team logo.",
        "Team logos",
        "Replace position cards with official INDYCAR entrant badges when available.",
        "Race-control alerts",
        "Show flag and race-control alerts beside the event name.",
        "Show NTT INDYCAR SERIES and the event title.",
        "Show the session, circuit, remaining time, and laps.",
        "Track status",
        "Show the current circuit-condition badge.",
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
        "Rider code",
        "Rider name",
        "Bike number",
        $"Show {championship} and the Grand Prix venue.",
        "Show the session, remaining time, and lap count.",
        "Show the gap to the leader below each rider.",
        "Show a position card or motorcycle manufacturer logo.",
        "Manufacturer logos",
        "Replace position cards with motorcycle manufacturer marks when available.",
        "Race-control alerts",
        "Show flag and race-control alerts beside the Grand Prix name.",
        $"Show {championship} and the Grand Prix title.",
        "Show the session, circuit, remaining time, and laps.",
        "Track status",
        "Show the current circuit-condition badge.",
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
        "Crew code",
        "Crew name",
        "Car number",
        "Show WRC and the rally name.",
        "Show the current special stage and stage progress.",
        "Show the gap to the leading crew.",
        "Show a position card or vehicle manufacturer logo.",
        "Manufacturer logos",
        "Replace position cards with vehicle manufacturer marks when available.",
        "Stage alerts",
        "Show stage interruptions and rally-control alerts beside the rally name.",
        "Show WRC and the rally title.",
        "Show the rally, current special stage, and timing progress.",
        "Course status",
        "Show the current stage or rally-condition badge.",
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
        "Entry code",
        "Entry name",
        "Car number",
        $"Show {championship} and the event venue.",
        "Show the session, remaining time, and race lap.",
        "Show the gap to the leading entry.",
        "Show a position card or vehicle manufacturer logo.",
        "Manufacturer logos",
        "Replace position cards with vehicle manufacturer marks when available.",
        "Race-control alerts",
        "Show flag and race-control alerts beside the event name.",
        $"Show {championship} and the event title.",
        "Show the session, circuit, remaining time, and race progress.",
        "Track status",
        "Show the current circuit-condition badge.",
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
        string codeOption,
        string fullNameOption,
        string numberOption,
        string taskbarEventDescription,
        string taskbarProgressDescription,
        string taskbarGapDescription,
        string positionMarkerDescription,
        string manufacturerTitle,
        string taskbarManufacturerDescription,
        string flagTitle,
        string flagDescription,
        string flyoutEventDescription,
        string sessionDetailsDescription,
        string trackStatusTitle,
        string trackStatusDescription,
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
            CompetitorLabelTitle = $"{competitor} label",
            CompetitorLabelDescription = $"Choose how each {competitor.ToLowerInvariant()} is identified.",
            CodeOption = codeOption,
            FullNameOption = fullNameOption,
            NumberOption = numberOption,
            TaskbarEventDescription = taskbarEventDescription,
            TaskbarProgressDescription = taskbarProgressDescription,
            TaskbarGapDescription = taskbarGapDescription,
            PositionMarkerDescription = positionMarkerDescription,
            ManufacturerTitle = manufacturerTitle,
            TaskbarManufacturerDescription = taskbarManufacturerDescription,
            FlagTitle = flagTitle,
            FlagDescription = flagDescription,
            FlyoutCountTitle = $"Maximum {competitors}",
            FlyoutCountDescription = $"Limit the classification to the leading {competitors}.",
            FlyoutEventDescription = flyoutEventDescription,
            SessionDetailsDescription = sessionDetailsDescription,
            TrackStatusTitle = trackStatusTitle,
            TrackStatusDescription = trackStatusDescription,
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
