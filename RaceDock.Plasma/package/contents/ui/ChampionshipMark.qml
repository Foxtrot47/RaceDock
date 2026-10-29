// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import org.kde.kirigami as Kirigami

/// Championship artwork shared by the idle panel strip and schedule flyout.
Item {
    id: mark

    property string championship: ""
    property string shortName: ""
    property color accent: "#7E8798"

    readonly property var assets: ({
        Formula1: "f1-wide",
        Formula2: "f2-wide",
        Formula3: "f3-wide",
        FormulaE: "formulae-flat",
        MotoGp: "motogp-wide",
        Moto2: "moto2",
        Moto3: "moto3",
        WorldRallyChampionship: "wrc-wide",
        GtWorldChallengeEurope: "gtwc-wide",
        WorldEnduranceChampionship: "wec-wide",
        EuropeanLeMansSeries: "elms-wide",
        MichelinLeMansCup: "mlmc-wide",
        Imsa: "imsa-wide",
        Dtm: "dtm-wide-flat",
        IndyCar: "indycar-wide"
    })
    readonly property string asset: assets[championship] || ""
    readonly property bool lightArtwork: Kirigami.Theme.textColor.r * 0.2126
        + Kirigami.Theme.textColor.g * 0.7152
        + Kirigami.Theme.textColor.b * 0.0722 > 0.5
    readonly property bool monochrome: ["MotoGp", "Moto2", "Moto3",
        "EuropeanLeMansSeries", "MichelinLeMansCup"].indexOf(championship) >= 0

    Rectangle {
        anchors.fill: parent
        visible: !mark.asset || artwork.status === Image.Error
        radius: Math.min(width, height) * 0.15
        color: mark.accent

        Text {
            anchors.centerIn: parent
            width: parent.width - 6
            text: mark.shortName
            color: "white"
            font.pixelSize: Math.min(10, parent.height * 0.35)
            font.bold: true
            elide: Text.ElideRight
            horizontalAlignment: Text.AlignHCenter
            maximumLineCount: 1
        }
    }

    Image {
        id: artwork
        anchors.fill: parent
        visible: !!mark.asset
        source: mark.asset
            ? "logos/championships/" + mark.asset
                + (mark.monochrome && mark.lightArtwork ? "-light" : "") + ".png"
            : ""
        fillMode: Image.PreserveAspectFit
        smooth: true
        mipmap: true
    }
}
