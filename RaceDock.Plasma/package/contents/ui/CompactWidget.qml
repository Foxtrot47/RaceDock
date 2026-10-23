// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Layouts
import org.kde.kirigami as Kirigami

/// The compact panel strip. It deliberately mirrors the Windows taskbar widget: a series context,
/// a flag/alert accent, and one cell per visible competitor.
MouseArea {
    id: widget

    required property var client
    readonly property var state: client ? client.widgetState : ({ mode: "hidden", competitors: [], display: {} })
    readonly property bool live: state.mode === "live"
    readonly property bool idle: state.mode === "idle"
    readonly property var display: state.display || ({})
    /// Mirrors the Windows taskbar's single-row mode: the position card folds into the driver line and
    /// the event and progress share one line, so the whole strip stays one line tall.
    readonly property bool singleRow: display.singleRow === true
    /// The idle strip grows to fit "event detail" side by side in single-row mode, like the Windows view.
    readonly property real idleWidth: singleRow
        ? Math.min(520, Math.max(248, 70 + idleEventInline.implicitWidth + idleDetailInline.implicitWidth))
        : 248

    signal toggleRequested()

    implicitWidth: state.mode === "hidden" ? 0
        : idle ? idleWidth
        : Math.min(singleRow ? 620 : 570,
            (singleRow ? 160 : 110) + Math.max(1, display.competitorCount || 5) * (singleRow ? 78 : 72) + 12)
    implicitHeight: Kirigami.Units.gridUnit * 2

    Layout.minimumWidth: implicitWidth
    Layout.preferredWidth: implicitWidth
    Layout.maximumWidth: implicitWidth
    Layout.minimumHeight: implicitHeight
    Layout.preferredHeight: implicitHeight
    Layout.maximumHeight: implicitHeight

    hoverEnabled: true
    acceptedButtons: Qt.LeftButton
    cursorShape: Qt.PointingHandCursor
    onClicked: widget.toggleRequested()

    Accessible.role: Accessible.Button
    Accessible.name: {
        if (widget.state.mode === "live")
            return (widget.state.fullEvent || widget.state.event || "RaceDock")
                + (widget.state.session ? ", " + widget.state.session : "")
                + (widget.state.progress ? ", " + widget.state.progress : "");
        if (widget.state.mode === "idle" && widget.state.idle)
            return widget.state.idle.headline + ", " + widget.state.idle.detail;
        return "RaceDock";
    }
    Accessible.description: widget.state.alert ? widget.state.alert.title
        : widget.state.flag ? widget.state.flag.label : ""
    Accessible.onPressAction: widget.toggleRequested()

    Rectangle {
        anchors.fill: parent
        radius: 5
        color: widget.containsMouse ? Qt.rgba(1, 1, 1, 0.08) : "transparent"
        border.width: widget.state.alert ? 1 : 0
        border.color: widget.state.alert ? widget.state.alert.color : "transparent"
    }

    RowLayout {
        anchors.fill: parent
        anchors.margins: 4
        spacing: 4
        visible: widget.live

        Item {
            Layout.preferredWidth: widget.singleRow ? 150 : 100
            Layout.fillHeight: true

            Rectangle {
                anchors.left: parent.left
                anchors.top: parent.top
                anchors.bottom: parent.bottom
                width: 3
                radius: 2
                color: widget.state.alert ? widget.state.alert.color
                    : widget.state.flag ? widget.state.flag.color
                    : (widget.state.accent || "#7E8798")
            }

            ColumnLayout {
                anchors.left: parent.left
                anchors.leftMargin: 7
                anchors.right: parent.right
                anchors.verticalCenter: parent.verticalCenter
                spacing: 0
                visible: !widget.singleRow

                Text {
                    Layout.fillWidth: true
                    text: widget.state.event || widget.state.championshipShort || "RaceDock"
                    color: Kirigami.Theme.textColor
                    font.pixelSize: 10
                    font.bold: true
                    elide: Text.ElideRight
                    maximumLineCount: 1
                }

                RowLayout {
                    Layout.fillWidth: true
                    spacing: 3

                    Text {
                        Layout.fillWidth: true
                        text: widget.state.progress || ""
                        color: Kirigami.Theme.disabledTextColor
                        font.pixelSize: 9
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }

                    FlagChip {
                        flag: widget.state.flag
                    }
                }
            }

            RowLayout {
                anchors.left: parent.left
                anchors.leftMargin: 7
                anchors.right: parent.right
                anchors.verticalCenter: parent.verticalCenter
                spacing: 3
                visible: widget.singleRow

                Text {
                    Layout.fillWidth: true
                    text: (widget.state.event || widget.state.championshipShort || "RaceDock")
                        + (widget.state.progress ? " · " + widget.state.progress : "")
                    color: Kirigami.Theme.textColor
                    font.pixelSize: 10
                    font.bold: true
                    elide: Text.ElideRight
                    maximumLineCount: 1
                }

                FlagChip {
                    flag: widget.state.flag
                }
            }
        }

        Repeater {
            model: Math.max(1, widget.display.competitorCount || 5)

            delegate: Item {
                id: cell
                required property int index
                readonly property var competitor: widget.state.competitors && index < widget.state.competitors.length
                    ? widget.state.competitors[index] : null
                /// PIT/RET/STOP/LEAD otherwise the interval, so both layouts show the same short state.
                readonly property string detailText: {
                    if (!competitor)
                        return "";
                    if (competitor.retired)
                        return "RET";
                    if (competitor.stopped)
                        return "STOP";
                    if (competitor.inPit)
                        return "PIT";
                    if (competitor.isLeader)
                        return "LEAD";
                    return competitor.interval || "—";
                }

                Layout.preferredWidth: widget.singleRow ? 74 : 68
                Layout.fillHeight: true

                Rectangle {
                    anchors.fill: parent
                    radius: 4
                    opacity: cell.competitor ? 1 : 0.35
                    border.width: cell.competitor && (cell.competitor.isLeader || cell.competitor.isFastest) ? 1 : 0
                    border.color: cell.competitor && cell.competitor.isFastest ? "#C457FF"
                        : cell.competitor && cell.competitor.isLeader ? "#F2C94C" : "transparent"

                    gradient: Gradient {
                        orientation: Gradient.Horizontal
                        GradientStop {
                            position: 0
                            color: cell.competitor ? cell.competitor.positionColorStart : "#34373F"
                        }
                        GradientStop {
                            position: 1
                            color: cell.competitor ? cell.competitor.positionColorEnd : "#535864"
                        }
                    }
                }

                ColumnLayout {
                    anchors.fill: parent
                    anchors.margins: 4
                    spacing: 0
                    visible: !widget.singleRow && !!cell.competitor

                    RowLayout {
                        Layout.fillWidth: true
                        spacing: 3

                        Text {
                            text: cell.competitor ? cell.competitor.positionLabel : ""
                            color: "#FFFFFF"
                            opacity: 0.78
                            font.pixelSize: 9
                            font.bold: true
                        }

                        Text {
                            Layout.fillWidth: true
                            text: cell.competitor ? cell.competitor.code : ""
                            color: "#FFFFFF"
                            font.pixelSize: 10
                            font.bold: true
                            elide: Text.ElideRight
                            maximumLineCount: 1
                        }
                    }

                    Text {
                        Layout.fillWidth: true
                        text: cell.detailText
                        color: "#FFFFFF"
                        opacity: 0.82
                        font.pixelSize: 8
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }
                }

                Item {
                    anchors.fill: parent
                    anchors.margins: 4
                    visible: widget.singleRow && !!cell.competitor

                    Text {
                        id: inlinePosition
                        anchors.left: parent.left
                        anchors.verticalCenter: parent.verticalCenter
                        text: cell.competitor ? cell.competitor.positionLabel : ""
                        color: "#FFFFFF"
                        opacity: 0.78
                        font.pixelSize: 9
                        font.bold: true
                    }

                    Text {
                        id: inlineDetail
                        anchors.right: parent.right
                        anchors.verticalCenter: parent.verticalCenter
                        // Keep a three-letter code intact; a long interval gives way instead, still
                        // showing its scale. "+12.345" fits, "+1:02.345" truncates.
                        width: Math.min(implicitWidth,
                            Math.max(0, parent.width - inlinePosition.width - 6 - 24))
                        text: cell.detailText
                        color: "#FFFFFF"
                        opacity: 0.82
                        font.pixelSize: 8
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }

                    Text {
                        anchors.left: inlinePosition.right
                        anchors.leftMargin: 3
                        anchors.right: inlineDetail.left
                        anchors.rightMargin: 3
                        anchors.verticalCenter: parent.verticalCenter
                        text: cell.competitor ? cell.competitor.code : ""
                        color: "#FFFFFF"
                        font.pixelSize: 10
                        font.bold: true
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }
                }
            }
        }
    }

    RowLayout {
        anchors.fill: parent
        anchors.margins: 6
        spacing: 8
        visible: widget.idle

        Rectangle {
            implicitWidth: 38
            implicitHeight: 22
            radius: 4
            color: widget.state.idle ? widget.state.idle.accent : "#7E8798"

            Text {
                anchors.centerIn: parent
                text: widget.state.idle ? widget.state.idle.logoText : ""
                color: "#FFFFFF"
                font.pixelSize: 8
                font.bold: true
                elide: Text.ElideRight
            }
        }

        ColumnLayout {
            Layout.fillWidth: true
            spacing: 0
            visible: !widget.singleRow

            Text {
                Layout.fillWidth: true
                text: widget.state.idle ? widget.state.idle.headline : ""
                color: Kirigami.Theme.textColor
                font.pixelSize: 10
                font.bold: true
                elide: Text.ElideRight
                maximumLineCount: 1
            }

            Text {
                Layout.fillWidth: true
                text: widget.state.idle ? widget.state.idle.detail : ""
                color: Kirigami.Theme.disabledTextColor
                font.pixelSize: 9
                elide: Text.ElideRight
                maximumLineCount: 1
            }
        }

        RowLayout {
            Layout.fillWidth: true
            spacing: 8
            visible: widget.singleRow

            Text {
                id: idleEventInline
                Layout.fillWidth: true
                text: widget.state.idle ? widget.state.idle.headline : ""
                color: Kirigami.Theme.textColor
                font.pixelSize: 10
                font.bold: true
                elide: Text.ElideRight
                maximumLineCount: 1
            }

            Text {
                id: idleDetailInline
                Layout.fillWidth: true
                // Windows drops the separator in the single line so the two segments do not read as
                // one run of dots next to the headline's own " · ".
                text: widget.state.idle && widget.state.idle.detail
                    ? widget.state.idle.detail.replace(" · ", " ") : ""
                color: Kirigami.Theme.disabledTextColor
                font.pixelSize: 9
                elide: Text.ElideRight
                maximumLineCount: 1
            }
        }
    }

    /// The track-condition chip, shared by the single-row and two-row context lines.
    component FlagChip: Rectangle {
        // A `flag` of null means the track condition is clear, so the chip takes no space.
        property var flag

        visible: !!flag
        implicitWidth: Math.max(8, chipLabel.implicitWidth + 6)
        implicitHeight: 11
        radius: 3
        color: flag ? flag.color : "transparent"

        Text {
            id: chipLabel
            anchors.centerIn: parent
            text: parent.flag ? parent.flag.label : ""
            color: parent.flag && parent.flag.kind === "chequered" ? "#111111" : "#FFFFFF"
            font.pixelSize: 6
            font.bold: true
            maximumLineCount: 1
        }
    }
}
