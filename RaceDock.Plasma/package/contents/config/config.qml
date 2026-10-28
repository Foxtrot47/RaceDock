// SPDX-License-Identifier: GPL-3.0-or-later
import QtQuick
import org.kde.plasma.configuration

/// Configuration categories, mirroring the Windows settings pages that apply on Plasma. The pages
/// live in contents/ui next to the applet, and every change is pushed to the RaceDock service
/// rather than stored in the applet's own configuration group.
ConfigModel {
    ConfigCategory {
        name: "General"
        icon: "preferences-system"
        source: "ConfigGeneral.qml"
    }
    ConfigCategory {
        name: "Formula 1"
        icon: "flag"
        source: "ConfigChampionshipFormula1.qml"
    }
    ConfigCategory {
        name: "Formula 2"
        icon: "flag"
        source: "ConfigChampionshipFormula2.qml"
    }
    ConfigCategory {
        name: "Formula 3"
        icon: "flag"
        source: "ConfigChampionshipFormula3.qml"
    }
    ConfigCategory {
        name: "World Rally Championship"
        icon: "flag"
        source: "ConfigChampionshipWorldRallyChampionship.qml"
    }
    ConfigCategory {
        name: "GT World Challenge Europe"
        icon: "flag"
        source: "ConfigChampionshipGtWorldChallengeEurope.qml"
    }
    ConfigCategory {
        name: "World Endurance Championship"
        icon: "flag"
        source: "ConfigChampionshipWorldEnduranceChampionship.qml"
    }
    ConfigCategory {
        name: "European Le Mans Series"
        icon: "flag"
        source: "ConfigChampionshipEuropeanLeMansSeries.qml"
    }
    ConfigCategory {
        name: "Michelin Le Mans Cup"
        icon: "flag"
        source: "ConfigChampionshipMichelinLeMansCup.qml"
    }
    ConfigCategory {
        name: "IMSA"
        icon: "flag"
        source: "ConfigChampionshipImsa.qml"
    }
    ConfigCategory {
        name: "MotoGP"
        icon: "flag"
        source: "ConfigChampionshipMotoGp.qml"
    }
    ConfigCategory {
        name: "Moto2"
        icon: "flag"
        source: "ConfigChampionshipMoto2.qml"
    }
    ConfigCategory {
        name: "Moto3"
        icon: "flag"
        source: "ConfigChampionshipMoto3.qml"
    }
    ConfigCategory {
        name: "Formula E"
        icon: "flag"
        source: "ConfigChampionshipFormulaE.qml"
    }
    ConfigCategory {
        name: "DTM"
        icon: "flag"
        source: "ConfigChampionshipDtm.qml"
    }
    ConfigCategory {
        name: "IndyCar"
        icon: "flag"
        source: "ConfigChampionshipIndyCar.qml"
    }
    ConfigCategory {
        name: "Testing"
        icon: "tools-report-bug"
        source: "ConfigTesting.qml"
    }
}
