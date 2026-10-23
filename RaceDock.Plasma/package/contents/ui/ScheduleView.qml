// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Layouts
import org.kde.kirigami as Kirigami

/// The "next on track" state. The first session is the answer to the question; the rest are a
/// quieter scan list rather than equally weighted rows.
ColumnLayout {
    id: scheduleView

    property var schedule: []
    readonly property real rowHeight: 62
    implicitHeight: heading.height + 8 + Math.max(1, schedule.length) * rowHeight + 4
    spacing: 0

    RowLayout {
        id: heading
        Layout.fillWidth: true
        Layout.preferredHeight: 24
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

    Repeater {
        model: scheduleView.schedule

        delegate: Rectangle {
            id: sessionCard
            required property var modelData
            readonly property var session: modelData

            Layout.fillWidth: true
            Layout.preferredHeight: scheduleView.rowHeight
            height: scheduleView.rowHeight
            radius: 6
            color: session && session.featured ? Qt.rgba(1, 1, 1, 0.055) : Qt.rgba(1, 1, 1, 0.018)

            Rectangle {
                anchors.left: parent.left
                anchors.top: parent.top
                anchors.bottom: parent.bottom
                width: 3
                radius: 2
                color: sessionCard.session ? sessionCard.session.accent : "#7E8798"
            }

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 12
                anchors.rightMargin: 8
                anchors.topMargin: 6
                anchors.bottomMargin: 6
                spacing: 8

                Rectangle {
                    Layout.preferredWidth: 34
                    Layout.preferredHeight: 18
                    radius: 3
                    color: sessionCard.session ? sessionCard.session.accent : "#7E8798"

                    Text {
                        anchors.centerIn: parent
                        text: sessionCard.session ? sessionCard.session.logoText : ""
                        color: "#FFFFFF"
                        font.pixelSize: 7
                        font.bold: true
                        maximumLineCount: 1
                    }
                }

                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: 0

                    Text {
                        Layout.fillWidth: true
                        text: sessionCard.session ? sessionCard.session.event || sessionCard.session.championshipName : ""
                        color: Kirigami.Theme.textColor
                        font.pixelSize: 11
                        font.bold: true
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }

                    Text {
                        Layout.fillWidth: true
                        text: {
                            if (!sessionCard.session) return "";
                            const parts = [];
                            if (sessionCard.session.session) parts.push(sessionCard.session.session);
                            if (sessionCard.session.venue) parts.push(sessionCard.session.venue);
                            return parts.join(" · ");
                        }
                        color: Kirigami.Theme.disabledTextColor
                        font.pixelSize: 9
                        elide: Text.ElideRight
                        maximumLineCount: 1
                    }
                }

                Column {
                    Layout.preferredWidth: 78
                    spacing: 0

                    Text {
                        width: parent.width
                        text: sessionCard.session ? sessionCard.session.time : ""
                        color: Kirigami.Theme.textColor
                        font.pixelSize: 10
                        font.bold: true
                        horizontalAlignment: Text.AlignRight
                        maximumLineCount: 1
                    }

                    Text {
                        width: parent.width
                        text: sessionCard.session ? sessionCard.session.date : ""
                        color: Kirigami.Theme.disabledTextColor
                        font.pixelSize: 8
                        horizontalAlignment: Text.AlignRight
                        maximumLineCount: 1
                    }

                    Text {
                        width: parent.width
                        text: sessionCard.session ? sessionCard.session.lead : ""
                        color: sessionCard.session ? sessionCard.session.accent : Kirigami.Theme.disabledTextColor
                        font.pixelSize: 8
                        horizontalAlignment: Text.AlignRight
                        maximumLineCount: 1
                    }
                }
            }
        }
    }

    Text {
        Layout.fillWidth: true
        Layout.preferredHeight: 32
        visible: scheduleView.schedule.length === 0
        text: "No published sessions for the championships you follow."
        color: Kirigami.Theme.disabledTextColor
        font.pixelSize: 10
        wrapMode: Text.Wrap
    }
}
