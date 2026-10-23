// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Layouts
import QtQuick.Controls
import org.kde.kirigami as Kirigami
import org.kde.plasma.components as PlasmaComponents3

/// The same flyout body is used inside Plasma's anchored applet popup and inside the pinned,
/// free-floating PlasmaCore.Dialog host.
Item {
    id: panel

    required property var client
    readonly property var state: client ? client.flyoutState : ({ mode: "hidden", rows: [], columns: [], schedule: [] })
    property bool pinned: false
    property var hostWindow: null

    signal pinRequested(bool shouldPin)
    signal closeRequested()
    signal configureRequested()

    readonly property int tableHeaderHeight: 24
    readonly property int footerHeight: 26
    readonly property real rowHeight: 40

    implicitWidth: Math.max(430, state.width || 460)
    implicitHeight: Math.max(230, state.height || 320)

    focus: true
    Keys.onEscapePressed: function (event) {
        panel.closeRequested();
        event.accepted = true;
    }

    Rectangle {
        anchors.fill: parent
        radius: 10
        color: Kirigami.Theme.backgroundColor
        border.width: 1
        border.color: Qt.rgba(1, 1, 1, 0.12)
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 1
        spacing: 0

        Item {
            id: header
            Layout.fillWidth: true
            Layout.preferredHeight: 58

            MouseArea {
                anchors.fill: parent
                acceptedButtons: Qt.LeftButton
                onPressed: {
                    if (panel.pinned && panel.hostWindow && panel.hostWindow.startSystemMove)
                        panel.hostWindow.startSystemMove();
                }
            }

            Rectangle {
                anchors.left: parent.left
                anchors.top: parent.top
                anchors.bottom: parent.bottom
                width: 4
                radius: 2
                color: panel.state.accent || "#7E8798"
            }

            Column {
                anchors.left: parent.left
                anchors.leftMargin: 14
                anchors.right: actions.left
                anchors.rightMargin: 8
                anchors.verticalCenter: parent.verticalCenter
                spacing: 1

                Text {
                    width: parent.width
                    text: panel.state.title || "RaceDock"
                    color: Kirigami.Theme.textColor
                    font.pixelSize: 14
                    font.bold: true
                    elide: Text.ElideRight
                    maximumLineCount: 1
                }

                Text {
                    width: parent.width
                    text: {
                        if (panel.state.mode === "schedule")
                            return "Published sessions · local time";
                        const parts = [];
                        if (panel.state.session) parts.push(panel.state.session);
                        if (panel.state.circuit) parts.push(panel.state.circuit);
                        if (panel.state.progress) parts.push(panel.state.progress);
                        return parts.join(" · ");
                    }
                    color: Kirigami.Theme.disabledTextColor
                    font.pixelSize: 9
                    elide: Text.ElideRight
                    maximumLineCount: 1
                }
            }

            Row {
                id: actions
                anchors.right: parent.right
                anchors.rightMargin: 8
                anchors.verticalCenter: parent.verticalCenter
                spacing: 2

                Rectangle {
                    visible: !!panel.state.trackLabel && panel.state.trackLabel.length > 0
                    width: Math.max(8, trackLabel.implicitWidth + 10)
                    height: 19
                    radius: 4
                    color: panel.state.trackColor || "#5C606A"

                    Text {
                        id: trackLabel
                        anchors.centerIn: parent
                        text: panel.state.trackLabel || ""
                        color: "#FFFFFF"
                        font.pixelSize: 7
                        font.bold: true
                        maximumLineCount: 1
                    }
                }

                PlasmaComponents3.ToolButton {
                    width: 28
                    height: 28
                    icon.name: "settings-configure"
                    Accessible.name: "Configure RaceDock"
                    onClicked: panel.configureRequested()
                }

                PlasmaComponents3.ToolButton {
                    width: 28
                    height: 28
                    icon.name: "pin"
                    checkable: true
                    checked: panel.pinned
                    Accessible.name: panel.pinned ? "Unpin the classification window" : "Pin the classification window"
                    onClicked: panel.pinRequested(!panel.pinned)
                }

                PlasmaComponents3.ToolButton {
                    width: 28
                    height: 28
                    icon.name: "window-close"
                    Accessible.name: panel.pinned ? "Close the classification window" : "Close the classification"
                    onClicked: panel.closeRequested()
                }
            }
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 1
            implicitHeight: 1
            color: Qt.rgba(1, 1, 1, 0.08)
        }

        Item {
            Layout.fillWidth: true
            Layout.fillHeight: true

            ColumnLayout {
                anchors.fill: parent
                anchors.leftMargin: 6
                anchors.rightMargin: 6
                anchors.topMargin: 5
                anchors.bottomMargin: 2
                spacing: 0
                visible: panel.state.mode === "live"

                Item {
                    Layout.fillWidth: true
                    Layout.preferredHeight: panel.tableHeaderHeight

                    Row {
                        anchors.fill: parent
                        spacing: 0

                        Item {
                            width: 34
                            height: parent.height
                            Text {
                                anchors.centerIn: parent
                                text: "POS"
                                color: Kirigami.Theme.disabledTextColor
                                font.pixelSize: 8
                                font.bold: true
                            }
                        }

                        Item {
                            width: panel.state.driverColumnWidth || 180
                            height: parent.height
                            Text {
                                anchors.left: parent.left
                                anchors.leftMargin: 4
                                anchors.verticalCenter: parent.verticalCenter
                                text: "DRIVER"
                                color: Kirigami.Theme.disabledTextColor
                                font.pixelSize: 8
                                font.bold: true
                            }
                        }

                        Repeater {
                            model: panel.state.columns || []

                            delegate: Item {
                                id: columnCell
                                required property var modelData
                                readonly property var column: modelData
                                width: column.visible ? column.width : 0
                                height: parent.height
                                visible: width > 0

                                Text {
                                    anchors.fill: parent
                                    anchors.leftMargin: 4
                                    anchors.rightMargin: 4
                                    text: column.label || ""
                                    color: Kirigami.Theme.disabledTextColor
                                    font.pixelSize: 8
                                    font.bold: true
                                    horizontalAlignment: column.align === "center" ? Text.AlignHCenter
                                        : column.align === "right" ? Text.AlignRight : Text.AlignLeft
                                    verticalAlignment: Text.AlignVCenter
                                    maximumLineCount: 1
                                }
                            }
                        }
                    }
                }

                ListView {
                    id: classification
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    clip: true
                    spacing: 0
                    model: panel.state.rows || []

                    ScrollBar.vertical: ScrollBar {
                        policy: ScrollBar.AsNeeded
                    }

                    delegate: ClassificationRow {
                        required property int index
                        required property var modelData
                        width: classification.width
                        row: modelData
                        columns: panel.state.columns || []
                        driverWidth: panel.state.driverColumnWidth || 180
                        animations: true
                        rowIndex: index
                    }
                }

                Text {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 36
                    visible: !panel.state.rows || panel.state.rows.length === 0
                    text: panel.state.emptyMessage || "Waiting for timing data…"
                    color: Kirigami.Theme.disabledTextColor
                    font.pixelSize: 10
                    wrapMode: Text.Wrap
                }
            }

            ScheduleView {
                anchors.fill: parent
                anchors.margins: 8
                visible: panel.state.mode === "schedule"
                schedule: panel.state.schedule || []
            }

            Column {
                anchors.centerIn: parent
                width: parent.width - 40
                spacing: 8
                visible: panel.state.mode === "hidden"

                Text {
                    width: parent.width
                    text: panel.client && panel.client.serviceAvailable
                        ? "No session is on track right now."
                        : "RaceDock service is not available."
                    color: Kirigami.Theme.textColor
                    font.pixelSize: 12
                    font.bold: true
                    horizontalAlignment: Text.AlignHCenter
                    wrapMode: Text.Wrap
                }

                Text {
                    width: parent.width
                    text: panel.client && panel.client.serviceAvailable
                        ? "The panel will show the next published session when one is available."
                        : (panel.client && panel.client.lastError ? panel.client.lastError
                            : "Install or start racedock-service, then try again.")
                    color: Kirigami.Theme.disabledTextColor
                    font.pixelSize: 10
                    horizontalAlignment: Text.AlignHCenter
                    wrapMode: Text.Wrap
                }
            }
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 1
            implicitHeight: 1
            color: Qt.rgba(1, 1, 1, 0.08)
        }

        Item {
            Layout.fillWidth: true
            Layout.preferredHeight: panel.footerHeight

            Row {
                anchors.fill: parent
                anchors.leftMargin: 10
                anchors.rightMargin: 10
                spacing: 6

                Rectangle {
                    anchors.verticalCenter: parent.verticalCenter
                    width: 7
                    height: 7
                    radius: 4
                    color: panel.client && panel.client.connected ? Kirigami.Theme.positiveTextColor : Kirigami.Theme.negativeTextColor
                }

                Text {
                    anchors.verticalCenter: parent.verticalCenter
                    width: parent.width - 120
                    text: panel.client && panel.client.connected
                        ? "RaceDock · " + (panel.state.connection || "connected")
                        : "RaceDock service offline"
                    color: Kirigami.Theme.disabledTextColor
                    font.pixelSize: 8
                    elide: Text.ElideRight
                    maximumLineCount: 1
                }

                Text {
                    anchors.verticalCenter: parent.verticalCenter
                    width: 104
                    text: panel.state.mode === "live" ? "LIVE" : panel.state.mode === "schedule" ? "SCHEDULE" : ""
                    color: panel.state.accent || Kirigami.Theme.disabledTextColor
                    font.pixelSize: 8
                    font.bold: true
                    horizontalAlignment: Text.AlignRight
                    maximumLineCount: 1
                }
            }
        }
    }
}
