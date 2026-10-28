// SPDX-License-Identifier: GPL-3.0-or-later
import QtQuick

/// Static catalog shared by the configuration pages. Championship keys are the enum names the
/// service uses in its JSON payloads. The per-championship wording is a port of the Windows
/// settings UI (ChampionshipSettingsPresentation.cs and the column descriptors in SettingsDraft.cs);
/// keep both sides in sync when either changes.
QtObject {
    /// The enum keys double as the profile keys in `championshipDisplay`; `enableKey` is the
    /// matching `UserSettings` power switch. The two do not always share a stem (WEC, ELMS, and WRC
    /// use `EnableWec`, `EnableElms`, and `EnableWrc`), so the mapping is explicit.
    readonly property var championships: [
        { key: "formula1", label: "Formula 1", enableKey: "enableFormula1" },
        { key: "formula2", label: "Formula 2", enableKey: "enableFormula2" },
        { key: "formula3", label: "Formula 3", enableKey: "enableFormula3" },
        { key: "worldRallyChampionship", label: "World Rally Championship", enableKey: "enableWrc" },
        { key: "gtWorldChallengeEurope", label: "GT World Challenge Europe", enableKey: "enableGtWorldChallengeEurope" },
        { key: "imsa", label: "IMSA", enableKey: "enableImsa" },
        { key: "motoGp", label: "MotoGP", enableKey: "enableMotoGp" },
        { key: "moto2", label: "Moto2", enableKey: "enableMoto2" },
        { key: "moto3", label: "Moto3", enableKey: "enableMoto3" },
        { key: "formulaE", label: "Formula E", enableKey: "enableFormulaE" },
        { key: "dtm", label: "DTM", enableKey: "enableDtm" },
        { key: "indyCar", label: "IndyCar", enableKey: "enableIndyCar" },
        { key: "worldEnduranceChampionship", label: "World Endurance Championship", enableKey: "enableWec" },
        { key: "europeanLeMansSeries", label: "European Le Mans Series", enableKey: "enableElms" },
        { key: "michelinLeMansCup", label: "Michelin Le Mans Cup", enableKey: "enableMichelinLeMansCup" }
    ]

    function commonPresentation(competitors, competitor, manufacturerTitle, taskbarManufacturerDescription,
                                teamTitle, teamDescription, flyoutManufacturerDescription,
                                resultTitle, resultDescription, categoryTitle, categoryDescription,
                                statusDescription) {
        const lower = competitor.toLowerCase();
        return {
            taskbarCountTitle: "Visible " + competitors,
            taskbarCountDescription: "Number of leading " + competitors + " shown in the widget.",
            manufacturerTitle: manufacturerTitle,
            taskbarManufacturerDescription: taskbarManufacturerDescription,
            flyoutCountTitle: "Maximum " + competitors,
            flyoutCountDescription: "Limit the classification to the leading " + competitors + ".",
            teamTitle: teamTitle,
            teamDescription: teamDescription,
            flyoutManufacturerDescription: flyoutManufacturerDescription,
            leaderGapDescription: "Show each " + lower + "'s gap to the leader.",
            intervalDescription: "Show the interval to the " + lower + " immediately ahead.",
            resultTitle: resultTitle,
            resultDescription: resultDescription,
            categoryTitle: categoryTitle,
            categoryDescription: categoryDescription,
            statusDescription: statusDescription,
            showManufacturerSettings: true,
            showTeamSetting: true,
            showResultSetting: true,
            showSectorTimingSetting: false,
            showSplitTimingSetting: false,
            showEnergySetting: false,
            lapTimeTitle: "Lap time",
            lapTimeDescription: "Show the best lap in practice and qualifying, and the last lap in sprints and races."
        };
    }

    function circuitPresentation(competitors, competitor, organization, showManufacturer, showTyres) {
        const presentation = commonPresentation(
            competitors, competitor,
            organization === "constructor" ? "Constructor logos" : "Manufacturer logos",
            "Replace position cards with " + organization + " marks when available.",
            "Team names",
            "Show each " + competitor.toLowerCase() + "'s " + organization + " or team.",
            "Show " + organization + " marks beside drivers.",
            "Tyre compound",
            "Show each driver's current tyre compound.",
            "Lap",
            "Show each driver's completed lap count.",
            "Show pit, running, retirement, and classification status.");
        presentation.showManufacturerSettings = showManufacturer;
        presentation.showResultSetting = showTyres;
        return presentation;
    }

    function endurancePresentation() {
        return commonPresentation(
            "entries", "Entry",
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
    }

    function motoPresentation() {
        const presentation = commonPresentation(
            "riders", "Rider",
            "Manufacturer logos",
            "Replace position cards with motorcycle manufacturer marks when available.",
            "Team names",
            "Show each rider's team.",
            "Show motorcycle manufacturer marks beside riders.",
            "Lap time",
            "Show each rider's lap time when supplied by live timing.",
            "Lap",
            "Show each rider's completed lap count.",
            "Show pit, running, retirement, and classification status.");
        presentation.showResultSetting = false;
        return presentation;
    }

    function rallyPresentation() {
        const presentation = commonPresentation(
            "crews", "Crew",
            "Manufacturer logos",
            "Replace position cards with vehicle manufacturer marks when available.",
            "Team names",
            "Show each crew's registered team.",
            "Show vehicle manufacturer marks beside crews.",
            "Tyres",
            "Show each crew's tyre choice.",
            "Rally class",
            "Show each crew's WRC category.",
            "Show running, retired, and classified crew status.");
        presentation.showTeamSetting = false;
        presentation.showResultSetting = false;
        presentation.lapTimeTitle = "Stage time";
        presentation.lapTimeDescription = "Show each crew's elapsed stage time.";
        return presentation;
    }

    function presentationFor(key) {
        switch (key) {
        case "formula1":
            return Object.assign(circuitPresentation("drivers", "Driver", "constructor", true, true),
                { showSectorTimingSetting: true });
        case "formula2":
        case "formula3":
            return circuitPresentation("drivers", "Driver", "team", false, false);
        case "motoGp":
        case "moto2":
        case "moto3":
            return motoPresentation();
        case "worldRallyChampionship":
            return Object.assign(rallyPresentation(), { showSplitTimingSetting: true });
        case "gtWorldChallengeEurope":
            return Object.assign(endurancePresentation(), { showSectorTimingSetting: true });
        case "worldEnduranceChampionship":
            return Object.assign(endurancePresentation(), {
                showSectorTimingSetting: true,
                showEnergySetting: true,
                resultTitle: "Tyre age",
                resultDescription: "Show current tyre ages in laps for each wheel, front axle above rear. Hover for compounds."
            });
        case "europeanLeMansSeries":
        case "michelinLeMansCup":
        case "imsa":
            return endurancePresentation();
        case "formulaE":
            return commonPresentation(
                "drivers", "Driver",
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
        case "dtm":
            return commonPresentation(
                "drivers", "Driver",
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
        case "indyCar":
            return commonPresentation(
                "drivers", "Driver",
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
        default:
            return circuitPresentation("competitors", "Competitor", "team", false, false);
        }
    }

    /// Column catalog in the service's default order. Only columns the championship's feed can fill
    /// are offered, exactly like the Windows column list.
    function columnsFor(key) {
        const p = presentationFor(key);
        return [
            { key: "gap", title: "Leader gap", description: p.leaderGapDescription, flag: "flyoutShowGaps", supported: true },
            { key: "interval", title: "Interval", description: p.intervalDescription, flag: "flyoutShowIntervals", supported: true },
            { key: "lapTime", title: p.lapTimeTitle, description: p.lapTimeDescription, flag: "flyoutShowLapTime", supported: true },
            { key: "sectors", title: "Sector times", description: "Show S1, S2, and S3 with purple, green, and yellow timing states.", flag: "flyoutShowSectorTimes", supported: p.showSectorTimingSetting },
            { key: "splits", title: "Stage splits", description: "Expand the classification with every published stage split.", flag: "flyoutShowSplitTimes", supported: p.showSplitTimingSetting },
            { key: "tyre", title: p.resultTitle, description: p.resultDescription, flag: "flyoutShowTyre", supported: p.showResultSetting },
            { key: "category", title: p.categoryTitle, description: p.categoryDescription, flag: "flyoutShowCategoryOrLap", supported: true },
            { key: "status", title: "Status", description: p.statusDescription, flag: "flyoutShowStatus", supported: true },
            { key: "energy", title: "Energy", description: "Show the reported virtual energy tank percentage and bar, not physical fuel level.", flag: "flyoutShowEnergy", supported: p.showEnergySetting }
        ];
    }

    function columnFor(key, championship) {
        const columns = columnsFor(championship);
        for (let i = 0; i < columns.length; ++i) {
            if (columns[i].key === key)
                return columns[i];
        }
        return { key: key, title: key, description: "", flag: "", supported: false };
    }

    function labelFor(key) {
        for (let i = 0; i < championships.length; ++i) {
            if (championships[i].key === key)
                return championships[i].label;
        }
        return key;
    }

    /// The `UserSettings` power switch for a championship key. Falling back to the key stem keeps a
    /// future championship working until its real flag name is added here.
    function enableKeyFor(key) {
        for (let i = 0; i < championships.length; ++i) {
            if (championships[i].key === key)
                return championships[i].enableKey;
        }
        return "enable" + key.charAt(0).toUpperCase() + key.slice(1);
    }
}
