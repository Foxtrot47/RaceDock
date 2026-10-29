// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Layouts
import QtQuick.Shapes
import org.kde.kirigami as Kirigami

/// The "next on track" state, mirroring the Windows schedule view: one featured card for the
/// answer to "what is next?", then a quieter "LATER" scan list. Only the featured session gets a
/// surface and an accent edge; the rest are plain rows, so the flyout does not turn into a stack
/// of cards.
ColumnLayout {
    id: scheduleView

    property var schedule: []

    readonly property int laterCount: Math.max(0, schedule.length - 1)
    readonly property real featuredHeight: 80
    readonly property real laterHeadingHeight: 25
    readonly property real laterRowHeight: 34
    readonly property color secondaryColor: Qt.rgba(Kirigami.Theme.textColor.r,
        Kirigami.Theme.textColor.g, Kirigami.Theme.textColor.b, 0.72)
    /// The featured card is a tint of the text colour, not a white wash: the Windows card uses
    /// the "control fill" brush, which is a subtle lift in both light and dark themes.
    readonly property color cardColor: Qt.rgba(Kirigami.Theme.textColor.r,
        Kirigami.Theme.textColor.g, Kirigami.Theme.textColor.b, 0.05)

    implicitHeight: schedule.length === 0
        ? 40
        : featuredHeight
            + (laterCount > 0 ? laterHeadingHeight + (laterCount * laterRowHeight) : 0)
    spacing: 0

    // The first session is the answer, so it gets the card treatment.
    Repeater {
        model: scheduleView.schedule.length > 0 ? [scheduleView.schedule[0]] : []

        delegate: Rectangle {
            id: featuredCard
            required property var modelData
            readonly property var session: modelData
            readonly property string clock: (session && session.time) || ""

            Layout.fillWidth: true
            Layout.preferredHeight: scheduleView.featuredHeight
            radius: 7
            color: scheduleView.cardColor

            // Follow the same 7 px corner arc as the card itself. This is a true left border,
            // not an inset pill with a gap at either rounded corner.
            Shape {
                id: accentBorder
                anchors.fill: parent
                readonly property real cornerRadius: featuredCard.radius
                readonly property real stripeWidth: 3
                // Cubic control distance for a quarter-circle.
                readonly property real bezierOffset: cornerRadius * 0.55228475
                readonly property real cornerControl: cornerRadius - bezierOffset
                readonly property real innerControl: cornerRadius + stripeWidth - bezierOffset

                ShapePath {
                    fillColor: featuredCard.session ? featuredCard.session.accent : "#7E8798"
                    strokeColor: "transparent"
                    startX: accentBorder.cornerRadius
                    startY: 0

                    PathLine { x: accentBorder.cornerRadius + accentBorder.stripeWidth; y: 0 }
                    PathCubic {
                        control1X: accentBorder.innerControl; control1Y: 0
                        control2X: accentBorder.stripeWidth; control2Y: accentBorder.cornerControl
                        x: accentBorder.stripeWidth; y: accentBorder.cornerRadius
                    }
                    PathLine { x: accentBorder.stripeWidth; y: accentBorder.height - accentBorder.cornerRadius }
                    PathCubic {
                        control1X: accentBorder.stripeWidth; control1Y: accentBorder.height - accentBorder.cornerControl
                        control2X: accentBorder.innerControl; control2Y: accentBorder.height
                        x: accentBorder.cornerRadius + accentBorder.stripeWidth; y: accentBorder.height
                    }
                    PathLine { x: accentBorder.cornerRadius; y: accentBorder.height }
                    PathCubic {
                        control1X: accentBorder.cornerControl; control1Y: accentBorder.height
                        control2X: 0; control2Y: accentBorder.height - accentBorder.cornerControl
                        x: 0; y: accentBorder.height - accentBorder.cornerRadius
                    }
                    PathLine { x: 0; y: accentBorder.cornerRadius }
                    PathCubic {
                        control1X: 0; control1Y: accentBorder.cornerControl
                        control2X: accentBorder.cornerControl; control2Y: 0
                        x: accentBorder.cornerRadius; y: 0
                    }
                }
            }

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 12
                anchors.rightMargin: 12
                anchors.topMargin: 11
                anchors.bottomMargin: 11
                spacing: 12

                ChampionshipMark {
                    Layout.preferredWidth: 72
                    Layout.preferredHeight: 34
                    championship: featuredCard.session ? featuredCard.session.championship : ""
                    shortName: featuredCard.session ? featuredCard.session.logoText : ""
                    accent: featuredCard.session ? featuredCard.session.accent : "#7E8798"
                }

                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: 0

                    Text {
                        Layout.fillWidth: true
                        text: featuredCard.session ? featuredCard.session.event : ""
                        color: Kirigami.Theme.textColor
                        font.pixelSize: 16
                        font.bold: true
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }

                    Text {
                        Layout.fillWidth: true
                        Layout.topMargin: 2
                        visible: featuredCard.session && !!featuredCard.session.session
                        text: featuredCard.session ? featuredCard.session.session || "" : ""
                        color: scheduleView.secondaryColor
                        font.pixelSize: 12
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }

                    Text {
                        Layout.fillWidth: true
                        Layout.topMargin: 3
                        visible: featuredCard.session && !!featuredCard.session.venue
                        text: featuredCard.session ? featuredCard.session.venue || "" : ""
                        color: Kirigami.Theme.disabledTextColor
                        font.pixelSize: 11
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }
                }

                ColumnLayout {
                    Layout.alignment: Qt.AlignRight | Qt.AlignVCenter
                    Layout.minimumWidth: 72
                    spacing: 1

                    Text {
                        Layout.fillWidth: true
                        text: featuredCard.clock.length > 0 ? featuredCard.clock : "TIME TBA"
                        color: Kirigami.Theme.textColor
                        font.pixelSize: featuredCard.clock.length > 0 ? 20 : 11
                        font.bold: true
                        horizontalAlignment: Text.AlignRight
                        maximumLineCount: 1
                    }

                    Text {
                        Layout.fillWidth: true
                        text: featuredCard.session ? featuredCard.session.date : ""
                        color: scheduleView.secondaryColor
                        font.pixelSize: 10
                        horizontalAlignment: Text.AlignRight
                        maximumLineCount: 1
                    }

                    Text {
                        Layout.fillWidth: true
                        Layout.topMargin: 5
                        text: featuredCard.session ? (featuredCard.session.lead || "").toUpperCase() : ""
                        color: Kirigami.Theme.disabledTextColor
                        font.pixelSize: 9
                        font.bold: true
                        horizontalAlignment: Text.AlignRight
                        maximumLineCount: 1
                    }
                }
            }
        }
    }

    RowLayout {
        Layout.fillWidth: true
        Layout.leftMargin: 2
        Layout.rightMargin: 2
        Layout.topMargin: 8
        Layout.bottomMargin: 4
        Layout.preferredHeight: 12
        visible: scheduleView.laterCount > 0
        spacing: 8

        Text {
            Layout.fillWidth: true
            text: "LATER"
            color: Kirigami.Theme.disabledTextColor
            font.pixelSize: 9
            font.bold: true
        }

        Text {
            text: scheduleView.laterCount + " MORE"
            color: Kirigami.Theme.disabledTextColor
            font.pixelSize: 8
            font.bold: true
        }
    }

    Repeater {
        model: scheduleView.schedule.slice(1)

        delegate: RowLayout {
            id: laterRow
            required property var modelData
            required property int index
            readonly property var session: modelData
            readonly property string clock: (session && session.time) || ""

            Layout.fillWidth: true
            Layout.leftMargin: 2
            Layout.rightMargin: 2
            Layout.bottomMargin: index === scheduleView.laterCount - 1 ? 1 : 4
            Layout.preferredHeight: scheduleView.laterRowHeight
            spacing: 11

            ChampionshipMark {
                Layout.preferredWidth: 54
                Layout.preferredHeight: 27
                Layout.alignment: Qt.AlignTop
                Layout.topMargin: 2
                championship: laterRow.session ? laterRow.session.championship : ""
                shortName: laterRow.session ? laterRow.session.logoText : ""
                accent: laterRow.session ? laterRow.session.accent : "#7E8798"
            }

            ColumnLayout {
                Layout.fillWidth: true
                Layout.alignment: Qt.AlignVCenter
                spacing: 0

                Text {
                    Layout.fillWidth: true
                    text: laterRow.session ? laterRow.session.event : ""
                    color: Kirigami.Theme.textColor
                    font.pixelSize: 13
                    font.bold: true
                    elide: Text.ElideRight
                    maximumLineCount: 1
                }

                Text {
                    Layout.fillWidth: true
                    Layout.topMargin: 1
                    visible: laterRow.session && !!laterRow.session.subtitle
                    text: laterRow.session ? laterRow.session.subtitle || "" : ""
                    color: scheduleView.secondaryColor
                    font.pixelSize: 10
                    elide: Text.ElideRight
                    maximumLineCount: 1
                }
            }

            ColumnLayout {
                Layout.alignment: Qt.AlignRight | Qt.AlignVCenter
                spacing: 0

                Text {
                    Layout.fillWidth: true
                    text: laterRow.session ? laterRow.session.date : ""
                    color: scheduleView.secondaryColor
                    font.pixelSize: 10
                    horizontalAlignment: Text.AlignRight
                    maximumLineCount: 1
                }

                Text {
                    Layout.fillWidth: true
                    Layout.topMargin: 1
                    text: laterRow.clock.length > 0 ? laterRow.clock : "TBA"
                    color: Kirigami.Theme.textColor
                    font.pixelSize: laterRow.clock.length > 0 ? 14 : 10
                    font.bold: true
                    horizontalAlignment: Text.AlignRight
                    maximumLineCount: 1
                }
            }
        }
    }

    Text {
        Layout.fillWidth: true
        Layout.leftMargin: 2
        Layout.rightMargin: 2
        Layout.topMargin: 8
        Layout.bottomMargin: 4
        visible: scheduleView.schedule.length === 0
        text: "No published sessions for the championships you follow."
        color: scheduleView.secondaryColor
        font.pixelSize: 12
        wrapMode: Text.Wrap
    }
}
