// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Layouts
import org.kde.kirigami as Kirigami

/// The "next on track" state, mirroring the Windows schedule view: one featured card for the
/// answer to "what is next?", then a quieter "LATER" scan list. Only the featured session gets a
/// surface and an accent edge; the rest are plain rows, so the flyout does not turn into a stack
/// of cards.
ColumnLayout {
    id: scheduleView

    property var schedule: []

    readonly property int laterCount: Math.max(0, schedule.length - 1)
    readonly property real headingHeight: 24
    readonly property real featuredHeight: 80
    readonly property real laterHeadingHeight: 34
    readonly property real laterRowHeight: 48
    readonly property color secondaryColor: Qt.rgba(Kirigami.Theme.textColor.r,
        Kirigami.Theme.textColor.g, Kirigami.Theme.textColor.b, 0.72)
    /// The featured card is a tint of the text colour, not a white wash: the Windows card uses
    /// the "control fill" brush, which is a subtle lift in both light and dark themes.
    readonly property color cardColor: Qt.rgba(Kirigami.Theme.textColor.r,
        Kirigami.Theme.textColor.g, Kirigami.Theme.textColor.b, 0.05)

    implicitHeight: schedule.length === 0
        ? headingHeight + 40
        : headingHeight + featuredHeight
            + (laterCount > 0 ? laterHeadingHeight + (laterCount * laterRowHeight) : 0)
    spacing: 0

    RowLayout {
        Layout.fillWidth: true
        Layout.leftMargin: 2
        Layout.rightMargin: 2
        Layout.topMargin: 1
        Layout.bottomMargin: 7
        Layout.preferredHeight: 16
        spacing: 8

        Text {
            Layout.fillWidth: true
            text: "NEXT ON TRACK"
            color: Kirigami.Theme.disabledTextColor
            font.pixelSize: 9
            font.bold: true
        }

        Text {
            text: "LOCAL TIME"
            color: Kirigami.Theme.disabledTextColor
            font.pixelSize: 9
            font.bold: true
        }
    }

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

            // The Windows card carries the championship colour as a left edge, not a full outline.
            Rectangle {
                anchors.left: parent.left
                anchors.top: parent.top
                anchors.bottom: parent.bottom
                width: 3
                radius: 1.5
                color: featuredCard.session ? featuredCard.session.accent : "#7E8798"
            }

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 12
                anchors.rightMargin: 12
                anchors.topMargin: 11
                anchors.bottomMargin: 11
                spacing: 12

                Rectangle {
                    Layout.preferredWidth: 72
                    Layout.preferredHeight: 34
                    radius: 6
                    color: featuredCard.session ? featuredCard.session.accent : "#7E8798"

                    Text {
                        anchors.centerIn: parent
                        text: featuredCard.session ? featuredCard.session.logoText : ""
                        color: "#FFFFFF"
                        font.pixelSize: 10
                        font.bold: true
                        maximumLineCount: 1
                    }
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
        Layout.topMargin: 15
        Layout.bottomMargin: 7
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
            Layout.bottomMargin: index === scheduleView.laterCount - 1 ? 2 : 10
            Layout.preferredHeight: scheduleView.laterRowHeight
            spacing: 11

            Rectangle {
                Layout.preferredWidth: 54
                Layout.preferredHeight: 27
                Layout.alignment: Qt.AlignTop
                Layout.topMargin: 2
                radius: 5
                color: laterRow.session ? laterRow.session.accent : "#7E8798"

                Text {
                    anchors.centerIn: parent
                    text: laterRow.session ? laterRow.session.logoText : ""
                    color: "#FFFFFF"
                    font.pixelSize: 8
                    font.bold: true
                    maximumLineCount: 1
                }
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
