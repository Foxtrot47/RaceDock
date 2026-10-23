// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Controls as QQC2
import QtQuick.Layouts
import org.kde.kcmutils as KCMUtils
import org.kde.kirigami as Kirigami
import "."

/// Developer actions for trying the front end without waiting for a race. The scenario source is
/// deterministic and runs entirely inside the service, so it is safe to leave running.
KCMUtils.SimpleKCM {
    id: page

    property string status: ""

    RaceDockClient {
        id: client
        pollWidget: false
        pollFlyout: false
    }

    function run(command, argument) {
        client.command(command, argument || "", result => {
            page.status = result.ok
                ? "Command '" + command + "' accepted."
                : "Command '" + command + "' failed: " + (result.error || "unknown error");
        });
    }

    Kirigami.FormLayout {
        QQC2.Label {
            Kirigami.FormData.isSection: true
            text: "These actions change what the service is showing for every RaceDock front end on this machine."
            color: Kirigami.Theme.disabledTextColor
            wrapMode: Text.WordWrap
            Layout.maximumWidth: Kirigami.Units.gridUnit * 26
        }

        RowLayout {
            Kirigami.FormData.label: "Test data:"
            QQC2.Button {
                text: "Start test scenario"
                icon.name: "media-playback-start"
                onClicked: page.run("scenario", "true")
            }
            QQC2.Button {
                text: "Return to live timing"
                icon.name: "network-connect"
                onClicked: page.run("live", "")
            }
        }

        RowLayout {
            Kirigami.FormData.label: "Schedule:"
            QQC2.Button {
                text: "Refresh calendar"
                icon.name: "view-refresh"
                onClicked: page.run("refreshSchedule", "")
            }
        }

        QQC2.Label {
            text: "A test scenario stays active until you return to live timing or restart the service."
            color: Kirigami.Theme.disabledTextColor
            wrapMode: Text.WordWrap
            Layout.maximumWidth: Kirigami.Units.gridUnit * 26
        }
    }

    footer: QQC2.Label {
        text: page.status
        visible: text.length > 0
        color: Kirigami.Theme.disabledTextColor
        padding: Kirigami.Units.smallSpacing
    }
}
