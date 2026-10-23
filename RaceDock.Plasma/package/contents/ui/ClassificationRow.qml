// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
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

    readonly property color textColor: Kirigami.Theme.textColor
    readonly property color mutedColor: Kirigami.Theme.disabledTextColor
    readonly property real totalWidth: 42 + driverWidth
        + columns.reduce((sum, column) => sum + (column.visible ? column.width : 0), 0) + 10

    implicitWidth: totalWidth
    implicitHeight: 40
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
                width: 25
                height: 25
                radius: 4
                gradient: Gradient {
                    orientation: Gradient.Horizontal
                    GradientStop { position: 0; color: rowItem.row.positionColorStart || "#34373F" }
                    GradientStop { position: 1; color: rowItem.row.positionColorEnd || "#535864" }
                }

                Text {
                    anchors.centerIn: parent
                    text: rowItem.row.positionLabel || ""
                    color: "#FFFFFF"
                    font.pixelSize: 10
                    font.bold: true
                    maximumLineCount: 1
                }
            }
        }

        Item {
            width: rowItem.driverWidth
            height: parent.height

            Column {
                anchors.verticalCenter: parent.verticalCenter
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.leftMargin: 4
                anchors.rightMargin: 6
                spacing: 0

                Text {
                    width: parent.width
                    text: rowItem.row.name || rowItem.row.code || ""
                    color: rowItem.row.isOverallFastest ? "#C457FF" : rowItem.textColor
                    font.pixelSize: 11
                    font.bold: rowItem.row.isOverallFastest
                    elide: Text.ElideRight
                    maximumLineCount: 1
                }

                Text {
                    width: parent.width
                    visible: text.length > 0
                    text: rowItem.row.team || ""
                    color: rowItem.mutedColor
                    font.pixelSize: 8
                    elide: Text.ElideRight
                    maximumLineCount: 1
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

                Text {
                    anchors.fill: parent
                    anchors.leftMargin: 4
                    anchors.rightMargin: 4
                    visible: cell.column.key === "gap" || cell.column.key === "interval" || cell.column.key === "lapTime"
                    text: {
                        if (cell.column.key === "gap") return rowItem.row.gap || "—";
                        if (cell.column.key === "interval") return rowItem.row.interval || "—";
                        return rowItem.row.lapTime || "—";
                    }
                    color: rowItem.textColor
                    font.pixelSize: 10
                    font.family: "monospace"
                    horizontalAlignment: cell.column.align === "center" ? Text.AlignHCenter
                        : cell.column.align === "right" ? Text.AlignRight : Text.AlignLeft
                    verticalAlignment: Text.AlignVCenter
                    elide: Text.ElideRight
                    maximumLineCount: 1
                }

                Row {
                    anchors.fill: parent
                    anchors.leftMargin: 2
                    anchors.rightMargin: 2
                    visible: cell.column.key === "sectors"

                    Repeater {
                        model: rowItem.row.sectors || []

                        delegate: Item {
                            required property var modelData
                            width: cell.width / 3
                            height: cell.height

                            Column {
                                anchors.centerIn: parent
                                spacing: 1

                                Text {
                                    anchors.horizontalCenter: parent.horizontalCenter
                                    text: modelData.value || "—"
                                    color: modelData.color || rowItem.textColor
                                    font.pixelSize: 9
                                    font.family: "monospace"
                                    maximumLineCount: 1
                                }

                                Row {
                                    anchors.horizontalCenter: parent.horizontalCenter
                                    spacing: 1

                                    Repeater {
                                        model: modelData.miniSectors || []

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
                    anchors.leftMargin: 3
                    anchors.rightMargin: 3
                    visible: cell.column.key === "splits"

                    Repeater {
                        model: rowItem.row.splits || []

                        delegate: Text {
                            required property var modelData
                            width: cell.width / Math.max(1, (rowItem.row.splits || []).length)
                            height: cell.height
                            text: modelData.value || "—"
                            color: rowItem.textColor
                            font.pixelSize: 9
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
                        width: 28
                        height: 20
                        radius: 3
                        color: rowItem.row.tyre ? rowItem.row.tyre.color : "#5C606A"

                        Text {
                            anchors.centerIn: parent
                            text: {
                                if (!rowItem.row.tyre) return "—";
                                const letter = rowItem.row.tyre.letter || "";
                                const age = rowItem.row.tyre.age;
                                return letter + (age !== null && age !== undefined ? " " + age : "");
                            }
                            color: rowItem.row.tyre ? rowItem.row.tyre.textColor : "#FFFFFF"
                            font.pixelSize: 9
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
                                required property var modelData
                                width: 23
                                height: 16
                                radius: 2
                                color: modelData.color || "#5C606A"

                                // The cell is 23px wide, so a wheel shows the compound letter and its age
                                // rather than "MEDIUM 12" clipping into nonsense.
                                Text {
                                    anchors.centerIn: parent
                                    text: {
                                        const compound = (modelData.compound || "").toUpperCase();
                                        const letter = compound.length > 0 ? compound.charAt(0) : "·";
                                        return modelData.age !== null && modelData.age !== undefined
                                            ? letter + modelData.age
                                            : letter;
                                    }
                                    color: "#FFFFFF"
                                    font.pixelSize: 7
                                    font.bold: true
                                    maximumLineCount: 1
                                }

                                PlasmaComponents3.ToolTip {
                                    text: (modelData.name || "Wheel") + ": "
                                        + (modelData.compound || "unknown compound")
                                        + (modelData.age !== null && modelData.age !== undefined
                                            ? ", " + modelData.age + " laps" : "")
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

                Text {
                    anchors.fill: parent
                    visible: cell.column.key === "category"
                    text: rowItem.row.category || "—"
                    color: rowItem.textColor
                    font.pixelSize: 9
                    horizontalAlignment: Text.AlignHCenter
                    verticalAlignment: Text.AlignVCenter
                    maximumLineCount: 1
                }

                Rectangle {
                    anchors.centerIn: parent
                    visible: cell.column.key === "status" && rowItem.row.status && rowItem.row.status.text.length > 0
                    width: Math.min(cell.width - 8, statusLabel.implicitWidth + 10)
                    height: 17
                    radius: 3
                    color: rowItem.row.status ? rowItem.row.status.color : "transparent"

                    Text {
                        id: statusLabel
                        anchors.centerIn: parent
                        text: rowItem.row.status ? rowItem.row.status.text : ""
                        color: Kirigami.Theme.textColor
                        font.pixelSize: 8
                        font.bold: true
                        maximumLineCount: 1
                    }
                }

                Column {
                    anchors.fill: parent
                    anchors.margins: 3
                    visible: cell.column.key === "energy"
                    spacing: 2

                    Text {
                        width: parent.width
                        text: rowItem.row.energy ? rowItem.row.energy.text : "—"
                        color: rowItem.row.energy ? rowItem.row.energy.color : rowItem.mutedColor
                        font.pixelSize: 8
                        horizontalAlignment: Text.AlignHCenter
                        maximumLineCount: 1
                    }

                    Rectangle {
                        width: parent.width
                        height: 3
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
