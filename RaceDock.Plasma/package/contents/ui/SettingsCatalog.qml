// SPDX-License-Identifier: GPL-3.0-or-later
import QtQuick

/// Static lists shared by the configuration pages. The keys are the enum names the service uses in
/// its JSON payloads, so a page never has to guess how a championship or column is spelled on the wire.
QtObject {
    readonly property var championships: [
        { key: "formula1", label: "Formula 1" },
        { key: "formula2", label: "Formula 2" },
        { key: "formula3", label: "Formula 3" },
        { key: "worldRallyChampionship", label: "World Rally Championship" },
        { key: "gtWorldChallengeEurope", label: "GT World Challenge Europe" },
        { key: "imsa", label: "IMSA" },
        { key: "motoGp", label: "MotoGP" },
        { key: "moto2", label: "Moto2" },
        { key: "moto3", label: "Moto3" },
        { key: "formulaE", label: "Formula E" },
        { key: "dtm", label: "DTM" },
        { key: "indyCar", label: "IndyCar" },
        { key: "worldEnduranceChampionship", label: "World Endurance Championship" },
        { key: "europeanLeMansSeries", label: "European Le Mans Series" },
        { key: "michelinLeMansCup", label: "Michelin Le Mans Cup" }
    ]

    /// Column order entries and the display flag that shows them. Widths and labels still come from
    /// the service so both front ends stay aligned.
    readonly property var columns: [
        { key: "gap", label: "Gap to leader", flag: "flyoutShowGaps" },
        { key: "interval", label: "Interval", flag: "flyoutShowIntervals" },
        { key: "lapTime", label: "Lap time", flag: "flyoutShowLapTime" },
        { key: "sectors", label: "Sectors", flag: "flyoutShowSectorTimes" },
        { key: "splits", label: "Rally splits", flag: "flyoutShowSplitTimes" },
        { key: "tyre", label: "Tyre", flag: "flyoutShowTyre" },
        { key: "category", label: "Category / lap", flag: "flyoutShowCategoryOrLap" },
        { key: "status", label: "Status", flag: "flyoutShowStatus" },
        { key: "energy", label: "Energy", flag: "flyoutShowEnergy" }
    ]

    function columnFor(key) {
        for (let i = 0; i < columns.length; ++i) {
            if (columns[i].key === key)
                return columns[i];
        }
        return { key: key, label: key, flag: "" };
    }
}
