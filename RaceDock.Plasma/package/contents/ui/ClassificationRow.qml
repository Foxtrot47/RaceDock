// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Window
import org.kde.kirigami as Kirigami
import org.kde.plasma.components as PlasmaComponents3

/// One classification line. Column order and visibility come from the service so the widget and a
/// future macOS front end share exactly the same presentation rules.
Item {
    id: rowItem

    required property var row
    required property var columns
    property real driverWidth: 180
    property bool animations: true
    property int rowIndex: 0
    property bool showManufacturerLogos: false

    readonly property color textColor: Kirigami.Theme.textColor
    readonly property color mutedColor: Kirigami.Theme.disabledTextColor
    readonly property real totalWidth: 42 + driverWidth
        + columns.reduce((sum, column) => sum + (column.visible ? column.width : 0), 0) + 10
    readonly property bool logoVisible: showManufacturerLogos && !!row.manufacturerLogo

    /// Compound badges come from the service as saturated colours; light tyres need dark text to
    /// stay readable on any theme.
    function contrastText(color) {
        const hex = String(color).replace("#", "");
        if (hex.length < 6)
            return "#FFFFFF";
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        return (0.299 * r + 0.587 * g + 0.114 * b) > 150 ? "#15181D" : "#FFFFFF";
    }

    implicitWidth: totalWidth
    implicitHeight: 50
    opacity: row.opacity !== undefined ? row.opacity : 1

    Rectangle {
        anchors.fill: parent
        color: rowItem.rowIndex % 2 === 0 ? Qt.rgba(1, 1, 1, 0.025) : "transparent"
    }

    Row {
        anchors.fill: parent
        anchors.leftMargin: 4
        anchors.rightMargin: 4
        spacing: 0

        Item {
            width: 34
            height: parent.height

            Rectangle {
                anchors.centerIn: parent
                width: 28
                height: 28
                radius: 4
                gradient: Gradient {
                    orientation: Gradient.Horizontal
                    GradientStop { position: 0; color: rowItem.row.positionColorStart || "#34373F" }
                    GradientStop { position: 1; color: rowItem.row.positionColorEnd || "#535864" }
                }

                RaceDockText {
                    anchors.centerIn: parent
                    text: rowItem.row.positionLabel || ""
                    color: "#FFFFFF"
                    font.pixelSize: 12
                    font.bold: true
                    maximumLineCount: 1
                }
            }
        }

        Item {
            width: rowItem.driverWidth
            height: parent.height

            Row {
                anchors.verticalCenter: parent.verticalCenter
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.leftMargin: 4
                anchors.rightMargin: 6
                spacing: 4

                Image {
                    anchors.verticalCenter: parent.verticalCenter
                    visible: rowItem.logoVisible
                    source: rowItem.logoVisible ? "logos/" + rowItem.row.manufacturerLogo : ""
                    width: 18
                    height: 18
                    fillMode: Image.PreserveAspectFit
                    asynchronous: true
                    smooth: true
                    mipmap: true
                    sourceSize: Qt.size(Math.ceil(18 * Screen.devicePixelRatio),
                        Math.ceil(18 * Screen.devicePixelRatio))
                }

                Column {
                    anchors.verticalCenter: parent.verticalCenter
                    width: parent.width - (rowItem.logoVisible ? 22 : 0)
                    spacing: 0

                    RaceDockText {
                        width: parent.width
                        text: rowItem.row.name || rowItem.row.code || ""
                        color: rowItem.row.isOverallFastest ? "#C457FF" : rowItem.textColor
                        font.pixelSize: 13
                        font.bold: rowItem.row.isOverallFastest
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }

                    RaceDockText {
                        width: parent.width
                        visible: text.length > 0
                        text: rowItem.row.team || ""
                        color: rowItem.mutedColor
                        font.pixelSize: 10
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }
                }
            }
        }

        Repeater {
            model: rowItem.columns

            delegate: Item {
                id: cell
                required property var modelData
                readonly property var column: modelData
                width: column.visible ? column.width : 0
                height: parent.height
                visible: width > 0

                RaceDockText {
                    anchors.fill: parent
                    anchors.leftMargin: 1.5
                    anchors.rightMargin: 1.5
                    visible: cell.column.key === "gap" || cell.column.key === "interval" || cell.column.key === "lapTime"
                    text: {
                        if (cell.column.key === "gap") return rowItem.row.gap || "—";
                        if (cell.column.key === "interval") return rowItem.row.interval || "—";
                        return rowItem.row.lapTime || "—";
                    }
                    color: rowItem.textColor
                    font.pixelSize: 12
                    font.family: "monospace"
                    horizontalAlignment: cell.column.align === "center" ? Text.AlignHCenter
                        : cell.column.align === "right" ? Text.AlignRight : Text.AlignLeft
                    verticalAlignment: Text.AlignVCenter
                    elide: Text.ElideRight
                    maximumLineCount: 1
                }

                Row {
                    anchors.fill: parent
                    anchors.leftMargin: 0.75
                    anchors.rightMargin: 0.75
                    visible: cell.column.key === "sectors"

                    Repeater {
                        model: rowItem.row.sectors || []

                        delegate: Item {
                            id: sectorCell
                            required property var modelData
                            width: cell.width / 3
                            height: cell.height

                            Column {
                                anchors.centerIn: parent
                                spacing: 1

                                RaceDockText {
                                    anchors.horizontalCenter: parent.horizontalCenter
                                    text: sectorCell.modelData.value || "—"
                                    color: sectorCell.modelData.color || rowItem.textColor
                                    font.pixelSize: 11
                                    font.family: "monospace"
                                    maximumLineCount: 1
                                }

                                Row {
                                    anchors.horizontalCenter: parent.horizontalCenter
                                    spacing: 1

                                    Repeater {
                                        model: sectorCell.modelData.miniSectors || []

                                        delegate: Rectangle {
                                            required property var modelData
                                            width: 2
                                            height: 2
                                            radius: 1
                                            color: modelData.color || "#5E6470"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                Row {
                    anchors.fill: parent
                    anchors.leftMargin: 1.125
                    anchors.rightMargin: 1.125
                    visible: cell.column.key === "splits"

                    Repeater {
                        model: rowItem.row.splits || []

                        delegate: RaceDockText {
                            required property var modelData
                            width: cell.width / Math.max(1, (rowItem.row.splits || []).length)
                            height: cell.height
                            text: modelData.value || "—"
                            color: rowItem.textColor
                            font.pixelSize: 11
                            font.family: "monospace"
                            horizontalAlignment: Text.AlignHCenter
                            verticalAlignment: Text.AlignVCenter
                            maximumLineCount: 1
                        }
                    }
                }

                Item {
                    anchors.fill: parent
                    visible: cell.column.key === "tyre"

                    Rectangle {
                        anchors.centerIn: parent
                        visible: !!rowItem.row.tyre && !rowItem.row.tyre.showAges
                        width: 32
                        height: 23
                        radius: 3
                        color: rowItem.row.tyre ? rowItem.row.tyre.color : "#5C606A"

                        RaceDockText {
                            anchors.centerIn: parent
                            text: {
                                if (!rowItem.row.tyre) return "—";
                                const letter = rowItem.row.tyre.letter || "";
                                const age = rowItem.row.tyre.age;
                                return letter + (age !== null && age !== undefined ? " " + age : "");
                            }
                            color: rowItem.contrastText(rowItem.row.tyre ? rowItem.row.tyre.color : "#5C606A")
                            font.pixelSize: 10
                            font.bold: true
                            maximumLineCount: 1
                        }
                    }

                    Grid {
                        anchors.centerIn: parent
                        visible: !!rowItem.row.tyre && rowItem.row.tyre.showAges
                        columns: 2
                        spacing: 1

                        Repeater {
                            model: rowItem.row.tyre && rowItem.row.tyre.showAges ? rowItem.row.tyre.wheels : []

                            delegate: Rectangle {
                                id: wheelCell
                                required property var modelData
                                width: 26
                                height: 19
                                radius: 2
                                color: wheelCell.modelData.color || "#5C606A"

                                // The cell shows the compound letter and its age
                                // rather than "MEDIUM 12" clipping into nonsense.
                                RaceDockText {
                                    anchors.centerIn: parent
                                    text: {
                                        const compound = (wheelCell.modelData.compound || "").toUpperCase();
                                        const letter = compound.length > 0 ? compound.charAt(0) : "·";
                                        return wheelCell.modelData.age !== null && wheelCell.modelData.age !== undefined
                                            ? letter + wheelCell.modelData.age
                                            : letter;
                                    }
                                    color: rowItem.contrastText(wheelCell.modelData.color || "#5C606A")
                                    font.pixelSize: 9
                                    font.bold: true
                                    maximumLineCount: 1
                                }

                                PlasmaComponents3.ToolTip {
                                    text: (wheelCell.modelData.name || "Wheel") + ": "
                                        + (wheelCell.modelData.compound || "unknown compound")
                                        + (wheelCell.modelData.age !== null && wheelCell.modelData.age !== undefined
                                            ? ", " + wheelCell.modelData.age + " laps" : "")
                                    visible: wheelMouse.containsMouse
                                    delay: 600
                                }

                                MouseArea {
                                    id: wheelMouse
                                    anchors.fill: parent
                                    hoverEnabled: true
                                }
                            }
                        }
                    }
                }

                RaceDockText {
                    anchors.fill: parent
                    visible: cell.column.key === "category"
                    text: rowItem.row.category || "—"
                    color: rowItem.textColor
                    font.pixelSize: 11
                    horizontalAlignment: Text.AlignHCenter
                    verticalAlignment: Text.AlignVCenter
                    maximumLineCount: 1
                }

                Rectangle {
                    anchors.centerIn: parent
                    visible: cell.column.key === "status" && rowItem.row.status && rowItem.row.status.text.length > 0
                    width: Math.min(cell.width - 8, statusLabel.implicitWidth + 10)
                    height: 20
                    radius: 3
                    color: rowItem.row.status ? rowItem.row.status.color : "transparent"

                    RaceDockText {
                        id: statusLabel
                        anchors.centerIn: parent
                        text: rowItem.row.status ? rowItem.row.status.text : ""
                        color: Kirigami.Theme.textColor
                        font.pixelSize: 10
                        font.bold: true
                        maximumLineCount: 1
                    }
                }

                Column {
                    anchors.fill: parent
                    anchors.margins: 3
                    visible: cell.column.key === "energy"
                    spacing: 2

                    RaceDockText {
                        width: parent.width
                        text: rowItem.row.energy ? rowItem.row.energy.text : "—"
                        color: rowItem.row.energy ? rowItem.row.energy.color : rowItem.mutedColor
                        font.pixelSize: 10
                        horizontalAlignment: Text.AlignHCenter
                        maximumLineCount: 1
                    }

                    Rectangle {
                        width: parent.width
                        height: 4
                        radius: 1
                        color: Qt.rgba(1, 1, 1, 0.12)

                        Rectangle {
                            width: parent.width * Math.max(0, Math.min(100, rowItem.row.energy && rowItem.row.energy.percent ? rowItem.row.energy.percent : 0)) / 100
                            height: parent.height
                            radius: 1
                            color: rowItem.row.energy ? rowItem.row.energy.color : "transparent"
                        }
                    }
                }
            }
        }
    }
}
