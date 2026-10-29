// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Layouts
import QtQuick.Window
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
    readonly property string idleHeadline: {
        const idle = widget.state.idle || {};
        const title = idle.headline || "";
        const mark = idle.championshipShort || idle.logoText || "";
        const prefix = mark ? mark + " · " : "";
        return prefix && title.indexOf(prefix) === 0 ? title.slice(prefix.length) : title;
    }
    /// The idle strip grows to fit "event detail" side by side in single-row mode, like the Windows view.
    /// Windows reserves 210 px for the two-row strip and 63 px of chrome (padding, logo, gap) plus
    /// the natural text widths for the single line.
    readonly property real idleWidth: singleRow
        ? Math.min(520, Math.max(210, 71 + idleEventInline.implicitWidth + idleDetailInline.implicitWidth))
        : 210

    signal toggleRequested()

    implicitWidth: state.mode === "hidden" ? 0
        : idle ? idleWidth
        : Math.min(singleRow ? 620 : 570,
            12 + Math.max(1, display.competitorCount || 5) * (singleRow ? 74 : 78)
            + (singleRow ? 150 : 100))
    // The Windows taskbar is 40 px tall: a 17 px event row and a 17 px progress row inside 2 px
    // margins. The strip is deliberately not tied to gridUnit, which varies by theme.
    implicitHeight: 40

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
            return widget.idleHeadline + ", " + widget.state.idle.detail;
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

    // The Windows taskbar layout: a context column on the left carries the event, flag, and
    // progress, and the competitor cells take the rest of the strip. Two-row mode stacks the event
    // and progress; single-row mode puts "event · progress" on one line and folds each position
    // into the driver line instead of drawing a marker.
    RowLayout {
        anchors.fill: parent
        anchors.margins: 2
        spacing: 2
        visible: widget.live

        ColumnLayout {
            id: contextColumn
            Layout.preferredWidth: widget.singleRow ? 150 : 100
            Layout.maximumWidth: widget.singleRow ? 150 : 100
            Layout.fillHeight: true
            spacing: 0

            // Two-row context: event name and flag above, progress underneath. The rows get fixed
            // halves instead of fillHeight: two fill items with different implicit heights are
            // not split evenly, and the event row would collapse.
            RowLayout {
                id: eventRow
                Layout.fillWidth: true
                Layout.preferredHeight: 18
                spacing: 2
                visible: !widget.singleRow

                MarqueeText {
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    text: widget.state.event || widget.state.championshipShort || "RaceDock"
                    textColor: Kirigami.Theme.textColor
                    pixelSize: 10
                    bold: true
                }

                FlagChip {
                    flag: widget.state.flag
                }
            }

            RaceDockText {
                Layout.fillWidth: true
                Layout.preferredHeight: 18
                visible: !widget.singleRow
                text: widget.state.progress || ""
                color: Kirigami.Theme.disabledTextColor
                font.pixelSize: 9
                elide: Text.ElideRight
                verticalAlignment: Text.AlignVCenter
                maximumLineCount: 1
            }

            // Single-row context: one combined line, matching the Windows taskbar.
            RowLayout {
                Layout.fillWidth: true
                Layout.fillHeight: true
                spacing: 4
                visible: widget.singleRow

                MarqueeText {
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    text: (widget.state.event || widget.state.championshipShort || "RaceDock")
                        + (widget.state.progress ? " · " + widget.state.progress : "")
                    textColor: Kirigami.Theme.textColor
                    pixelSize: 10
                    bold: true
                }

                FlagChip {
                    flag: widget.state.flag
                }
            }
        }

        RowLayout {
            Layout.fillWidth: true
            Layout.fillHeight: true
            spacing: 2

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
                    /// The Windows widget replaces the position card with a manufacturer mark in
                    /// two-row mode only; single-row keeps the inline position number instead.
                    readonly property bool logoVisible: !widget.singleRow
                        && widget.display.manufacturerLogos === true
                        && !!cell.competitor && !!cell.competitor.manufacturerLogo

                    Layout.fillWidth: true
                    Layout.fillHeight: true

                    RowLayout {
                        anchors.fill: parent
                        spacing: 4
                        visible: !widget.singleRow

                        Image {
                            Layout.preferredWidth: 26
                            Layout.preferredHeight: 26
                            visible: cell.logoVisible
                            source: cell.logoVisible ? "logos/" + cell.competitor.manufacturerLogo : ""
                            fillMode: Image.PreserveAspectFit
                            asynchronous: true
                            smooth: true
                            mipmap: true
                            sourceSize: Qt.size(Math.ceil(26 * Screen.devicePixelRatio),
                                Math.ceil(26 * Screen.devicePixelRatio))
                        }

                        Rectangle {
                            Layout.preferredWidth: 26
                            Layout.preferredHeight: 26
                            radius: 6
                            visible: !cell.logoVisible
                            opacity: cell.competitor ? 1 : 0.45
                            border.width: cell.competitor && (cell.competitor.isLeader || cell.competitor.isFastest) ? 1 : 0
                            border.color: cell.competitor && cell.competitor.isFastest ? "#C457FF"
                                : cell.competitor && cell.competitor.isLeader ? "#F2C94C" : "transparent"

                            gradient: Gradient {
                                orientation: Gradient.Vertical
                                GradientStop {
                                    position: 0
                                    color: cell.competitor ? cell.competitor.positionColorStart : "#34373F"
                                }
                                GradientStop {
                                    position: 1
                                    color: cell.competitor ? cell.competitor.positionColorEnd : "#535864"
                                }
                            }

                            RaceDockText {
                                anchors.centerIn: parent
                                text: cell.competitor ? cell.competitor.positionLabel : "—"
                                color: "#FFFFFF"
                                font.pixelSize: 11
                                font.bold: true
                            }
                        }

                        ColumnLayout {
                            Layout.fillWidth: true
                            Layout.fillHeight: true
                            spacing: 0

                            RaceDockText {
                                Layout.fillWidth: true
                                Layout.preferredHeight: 18
                                text: cell.competitor ? cell.competitor.code : ""
                                color: Kirigami.Theme.textColor
                                font.pixelSize: 10
                                font.bold: true
                                elide: Text.ElideRight
                                verticalAlignment: Text.AlignVCenter
                                maximumLineCount: 1
                            }

                            RaceDockText {
                                Layout.fillWidth: true
                                Layout.preferredHeight: 18
                                text: cell.detailText
                                color: Kirigami.Theme.disabledTextColor
                                font.pixelSize: 8
                                elide: Text.ElideRight
                                verticalAlignment: Text.AlignVCenter
                                maximumLineCount: 1
                            }
                        }
                    }

                    RowLayout {
                        anchors.fill: parent
                        spacing: 4
                        visible: widget.singleRow

                        RaceDockText {
                            text: cell.competitor ? cell.competitor.positionLabel : "—"
                            color: Kirigami.Theme.disabledTextColor
                            font.pixelSize: 9
                            font.bold: true
                            verticalAlignment: Text.AlignVCenter
                        }

                        RaceDockText {
                            text: cell.competitor ? cell.competitor.code : ""
                            color: Kirigami.Theme.textColor
                            font.pixelSize: 10
                            font.bold: true
                            verticalAlignment: Text.AlignVCenter
                        }

                        RaceDockText {
                            Layout.fillWidth: true
                            text: cell.detailText
                            color: Kirigami.Theme.disabledTextColor
                            font.pixelSize: 8
                            elide: Text.ElideRight
                            verticalAlignment: Text.AlignVCenter
                            maximumLineCount: 1
                        }
                    }
                }
            }
        }
    }

    // The Windows idle strip is a 40 px taskbar surface with 2 px vertical padding: a 42 x 22
    // championship mark, then the event above and the session/detail below, 17 px each.
    RowLayout {
        anchors.fill: parent
        anchors.leftMargin: 7
        anchors.rightMargin: 6
        anchors.topMargin: 2
        anchors.bottomMargin: 2
        spacing: widget.singleRow ? 8 : 10
        visible: widget.idle

        ChampionshipMark {
            implicitWidth: 42
            implicitHeight: 22
            championship: widget.state.idle ? widget.state.idle.championship : ""
            shortName: widget.state.idle ? widget.state.idle.logoText : ""
            accent: widget.state.idle ? widget.state.idle.accent : "#7E8798"
        }

        ColumnLayout {
            Layout.fillWidth: true
            spacing: 0
            visible: !widget.singleRow

            RaceDockText {
                Layout.fillWidth: true
                Layout.preferredHeight: 17
                text: widget.idleHeadline
                color: Kirigami.Theme.textColor
                font.pixelSize: 11
                elide: Text.ElideRight
                verticalAlignment: Text.AlignVCenter
                maximumLineCount: 1
            }

            RaceDockText {
                Layout.fillWidth: true
                Layout.preferredHeight: 17
                text: widget.state.idle ? widget.state.idle.detail : ""
                color: Kirigami.Theme.disabledTextColor
                font.pixelSize: 11
                elide: Text.ElideRight
                verticalAlignment: Text.AlignVCenter
                maximumLineCount: 1
            }
        }

        RowLayout {
            Layout.fillWidth: true
            spacing: 8
            visible: widget.singleRow

            RaceDockText {
                id: idleEventInline
                text: widget.idleHeadline
                color: Kirigami.Theme.textColor
                font.pixelSize: 11
                elide: Text.ElideRight
                verticalAlignment: Text.AlignVCenter
                maximumLineCount: 1
            }

            RaceDockText {
                id: idleDetailInline
                // Windows drops the separator in the single line so the two segments do not read as
                // one run of dots next to the headline's own " · ".
                text: widget.state.idle && widget.state.idle.detail
                    ? widget.state.idle.detail.replace(" · ", " ") : ""
                color: Kirigami.Theme.disabledTextColor
                font.pixelSize: 11
                elide: Text.ElideRight
                verticalAlignment: Text.AlignVCenter
                maximumLineCount: 1
            }

            Item {
                Layout.fillWidth: true
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

        RaceDockText {
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
