// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Controls as QQC2
import QtQuick.Dialogs
import QtQuick.Layouts
import org.kde.kirigami as Kirigami
import "."

/// Testing page, mirroring the Windows Testing page: a transport bar that shows what is driving
/// the widget and every playback control, the two non-live sources (deterministic scenario and
/// timing archive replay), and the scenario triggers. Every action is a service `Command`.
ConfigSettingsBase {
    id: page

    pollWidgetEnabled: true

    /// Last archive path accepted by the service, shown in the transport bar.
    property string archiveName: ""
    /// Result of the last archive action, shown next to the archive card so a failed load is
    /// visible without scrolling to the page footer.
    property string archiveStatus: ""
    property bool archiveFailed: false

    readonly property string viewMode: client.widgetState.viewMode || "live"
    /// The scenario and archive sources are playback sources; the transport controls only apply
    /// while one of them is driving the widget.
    readonly property bool playbackActive: viewMode !== "live"

    Connections {
        target: page.client

        function onWidgetUpdated() {
            scenarioSwitch.checked = page.viewMode === "scenario";
        }
    }

    function run(name, argument) {
        client.command(name, argument, result => {
            page.status = result.ok
                ? "Command '" + name + "' accepted."
                : "Could not run '" + name + "': " + (result.error || "unknown error");
        });
    }

    function loadArchive(path) {
        path = (path || "").trim();
        if (path.length === 0) {
            page.archiveStatus = "Choose a file with Browse, or paste a path first.";
            page.archiveFailed = true;
            return;
        }
        client.command("archive", path, result => {
            if (result.ok) {
                page.archiveName = path.substring(path.lastIndexOf("/") + 1);
                page.archiveStatus = "Replaying '" + page.archiveName + "'.";
                page.archiveFailed = false;
                page.status = page.archiveStatus;
            } else {
                page.archiveStatus = "Could not load the archive: " + (result.error || "unknown error");
                page.archiveFailed = true;
                page.status = page.archiveStatus;
            }
        });
    }

    /// Turns the picker's `file://` URL into a plain path the service can open.
    function localPath(urlValue) {
        if (!urlValue)
            return "";
        const text = String(urlValue);
        if (text.indexOf("file://") !== 0)
            return text;
        let path = text.substring(7);
        try {
            path = decodeURIComponent(path);
        } catch (error) {
            // Leave the raw path if it is not valid percent-encoding.
        }
        return path;
    }

    function returnToLive() {
        client.command("live", "", result => {
            page.status = result.ok ? "Live streams are active." : "Could not return to live: " + (result.error || "unknown error");
            page.archiveStatus = result.ok ? "" : page.status;
            page.archiveFailed = !result.ok;
        });
    }

    // The KCM content area sizes its children before the page has its final width, and Qt Quick
    // Layouts keeps that first-pass geometry, so every row ends up about one spacing wider than the
    // page column and the right edge is clipped. Capping each row at the column width pins them to
    // the real content width without changing the layout otherwise.
    ColumnLayout {
        id: testingColumn
        Layout.fillWidth: true
        spacing: 0

        QQC2.Label {
            Layout.topMargin: Kirigami.Units.largeSpacing
            Layout.maximumWidth: testingColumn.width
            text: "Testing"
            font.weight: Font.Bold
            font.pointSize: Kirigami.Theme.defaultFont.pointSize + 2
        }

        QQC2.Label {
            Layout.bottomMargin: Kirigami.Units.largeSpacing
            Layout.fillWidth: true
            Layout.maximumWidth: testingColumn.width
            text: "Replay a timing archive or RaceDock F1/GTWC/WEC feed dump, or drive a deterministic scenario without waiting for a live session."
            color: Kirigami.Theme.disabledTextColor
            wrapMode: Text.WordWrap
        }

        // Transport bar: what is driving the widget, and every playback control, in one fixed place.
        Rectangle {
            Layout.fillWidth: true
            Layout.maximumWidth: testingColumn.width
            Layout.bottomMargin: Kirigami.Units.largeSpacing
            radius: Kirigami.Units.smallSpacing
            color: Kirigami.Theme.alternateBackgroundColor
            implicitHeight: transportColumn.implicitHeight + (Kirigami.Units.largeSpacing * 2)

            ColumnLayout {
                id: transportColumn
                anchors.fill: parent
                anchors.margins: Kirigami.Units.largeSpacing
                spacing: Kirigami.Units.largeSpacing

                RowLayout {
                    Layout.fillWidth: true
                    spacing: Kirigami.Units.largeSpacing

                    Rectangle {
                        radius: height / 2
                        implicitWidth: badgeRow.implicitWidth + Kirigami.Units.largeSpacing
                        implicitHeight: Kirigami.Units.gridUnit * 1.1
                        color: page.playbackActive
                            ? (page.viewMode === "scenario" ? "#B7A019" : "#2468B0")
                            : "#2E8B57"

                        RowLayout {
                            id: badgeRow
                            anchors.centerIn: parent
                            spacing: Kirigami.Units.smallSpacing

                            Rectangle {
                                Layout.preferredWidth: Kirigami.Units.smallSpacing
                                Layout.preferredHeight: Kirigami.Units.smallSpacing
                                radius: width / 2
                                color: "white"
                            }

                            QQC2.Label {
                                text: page.viewMode === "scenario" ? "SCENARIO" : page.viewMode === "archive" ? "ARCHIVE" : "LIVE"
                                color: "white"
                                font.weight: Font.Bold
                                font.pointSize: Kirigami.Theme.smallFont.pointSize
                            }
                        }
                    }

                    ColumnLayout {
                        Layout.fillWidth: true
                        spacing: 0
                        QQC2.Label {
                            Layout.fillWidth: true
                            text: page.viewMode === "scenario"
                                ? "Deterministic scenario"
                                : page.viewMode === "archive"
                                    ? (page.archiveName.length > 0 ? page.archiveName : "Archive replay")
                                    : "Live streams are active"
                            elide: Text.ElideRight
                            font.weight: Font.DemiBold
                        }
                    }
                }

                RowLayout {
                    Layout.fillWidth: true
                    enabled: page.playbackActive
                    opacity: page.playbackActive ? 1 : 0.5
                    spacing: Kirigami.Units.smallSpacing

                    QQC2.Button {
                        icon.name: "media-playback-start"
                        QQC2.ToolTip.text: "Resume"
                        onClicked: page.run("resume", "")
                    }

                    QQC2.Button {
                        icon.name: "media-skip-forward"
                        QQC2.ToolTip.text: "Advance one frame"
                        onClicked: page.run("step", "")
                    }

                    QQC2.Button {
                        icon.name: "view-refresh"
                        QQC2.ToolTip.text: "Restart from the first frame"
                        onClicked: page.run("restart", "")
                    }

                    Item { Layout.fillWidth: true }

                    QQC2.Label { text: "Speed" }

                    QQC2.Slider {
                        id: speedSlider
                        Layout.preferredWidth: Kirigami.Units.gridUnit * 12
                        from: 0.25
                        to: 20
                        stepSize: 0.25
                        snapMode: QQC2.Slider.SnapAlways
                        onMoved: page.run("speed", value.toFixed(2))
                    }

                    QQC2.Label {
                        Layout.preferredWidth: Kirigami.Units.gridUnit * 3
                        horizontalAlignment: Text.AlignRight
                        text: speedSlider.value.toFixed(2) + "x"
                        font.family: "monospace"
                    }
                }
            }
        }

        QQC2.Label {
            Layout.maximumWidth: testingColumn.width
            text: "Source"
            font.weight: Font.DemiBold
            color: Kirigami.Theme.disabledTextColor
        }

        QQC2.Label {
            Layout.fillWidth: true
            Layout.maximumWidth: testingColumn.width
            text: "One source drives the widget at a time. Either option replaces the live feeds until you return to live."
            color: Kirigami.Theme.disabledTextColor
            font.pointSize: Kirigami.Theme.smallFont.pointSize
            wrapMode: Text.WordWrap
        }

        SettingsCard {
            Layout.maximumWidth: testingColumn.width
            title: "Deterministic scenario"
            description: "Replace live streams with the scripted feed you drive by hand."
            QQC2.Switch {
                id: scenarioSwitch
                onClicked: page.run("scenario", checked ? "true" : "false")
            }
        }

        // The archive controls need the full page width, so this card does not use SettingsCard's
        // title-left/control-right row: the path field spans the card and the buttons sit on their
        // own row, which keeps them inside the dialog.
        Rectangle {
            id: archiveCard
            Layout.fillWidth: true
            Layout.maximumWidth: testingColumn.width
            Layout.topMargin: Kirigami.Units.largeSpacing
            Layout.bottomMargin: Kirigami.Units.largeSpacing
            radius: Kirigami.Units.smallSpacing
            color: "transparent"
            implicitHeight: archiveColumn.implicitHeight + (Kirigami.Units.largeSpacing * 2)

            ColumnLayout {
                id: archiveColumn
                anchors.fill: parent
                anchors.margins: Kirigami.Units.largeSpacing
                spacing: Kirigami.Units.smallSpacing

                QQC2.Label {
                    Layout.fillWidth: true
                    text: "Timing archive"
                    font.weight: Font.DemiBold
                }

                QQC2.Label {
                    Layout.fillWidth: true
                    text: "Replay a Timing71 ZIP, HAR capture, or RaceDock F1/GTWC/WEC NDJSON dump. Browse for one, or paste its path."
                    color: Kirigami.Theme.disabledTextColor
                    font.pointSize: Kirigami.Theme.smallFont.pointSize
                    wrapMode: Text.WordWrap
                }

                QQC2.TextField {
                    id: archiveField
                    Layout.fillWidth: true
                    Layout.topMargin: Kirigami.Units.smallSpacing
                    placeholderText: "Path to a .ndjson, .zip, or .har file"
                    onAccepted: page.loadArchive(text)
                }

                // The buttons get their own row so a long path can never push them past the dialog
                // edge, which is where they ended up when they shared a row with the field.
                RowLayout {
                    Layout.fillWidth: true
                    spacing: Kirigami.Units.smallSpacing

                    Item { Layout.fillWidth: true }

                    // The picker is created on first use. Instantiating a dialog while plasmashell
                    // builds this page off-scene is what crashed the config dialog, so nothing exists
                    // until the user asks for it.
                    QQC2.Button {
                        text: "Browse…"
                        icon.name: "document-open"
                        onClicked: archivePicker.active = true
                    }

                    QQC2.Button {
                        text: "Load archive"
                        enabled: archiveField.text.trim().length > 0
                        onClicked: page.loadArchive(archiveField.text)
                    }

                    QQC2.Button {
                        text: "Return to live"
                        enabled: page.playbackActive
                        onClicked: page.returnToLive()
                    }
                }

                QQC2.Label {
                    Layout.fillWidth: true
                    visible: page.archiveStatus.length > 0
                    text: page.archiveStatus
                    color: page.archiveFailed ? "#C0392B" : Kirigami.Theme.disabledTextColor
                    font.pointSize: Kirigami.Theme.smallFont.pointSize
                    wrapMode: Text.WordWrap
                }
            }
        }

        Loader {
            id: archivePicker
            active: false
            sourceComponent: archivePickerComponent
        }

        QQC2.Label {
            Layout.topMargin: Kirigami.Units.largeSpacing
            Layout.maximumWidth: testingColumn.width
            text: "Scenario"
            font.weight: Font.DemiBold
            color: Kirigami.Theme.disabledTextColor
        }

        QQC2.Label {
            Layout.fillWidth: true
            Layout.maximumWidth: testingColumn.width
            text: "Enable the deterministic scenario to drive track, feed, and classification states."
            color: Kirigami.Theme.disabledTextColor
            font.pointSize: Kirigami.Theme.smallFont.pointSize
            wrapMode: Text.WordWrap
        }

        SettingsCard {
            Layout.maximumWidth: testingColumn.width
            title: "Track state"
            description: "One condition is active at a time."
            enabledCard: page.playbackActive
            RowLayout {
                spacing: Kirigami.Units.smallSpacing
                Repeater {
                    model: [
                        { label: "Green", command: "Green" },
                        { label: "Yellow", command: "Yellow" },
                        { label: "Double yellow", command: "DoubleYellow" },
                        { label: "Safety car", command: "SafetyCar" },
                        { label: "VSC", command: "VirtualSafetyCar" },
                        { label: "VSC ending", command: "VirtualSafetyCarEnding" },
                        { label: "Red flag", command: "RedFlag" }
                    ]
                    delegate: QQC2.Button {
                        required property var modelData
                        text: modelData.label
                        flat: true
                        onClicked: page.run("trigger", modelData.command)
                    }
                }
            }
        }

        SettingsCard {
            Layout.maximumWidth: testingColumn.width
            title: "Feed state"
            description: "Drive the connection badge and the stale-data guard."
            enabledCard: page.playbackActive
            RowLayout {
                spacing: Kirigami.Units.smallSpacing
                Repeater {
                    model: [
                        { label: "Connected", command: "Reconnect" },
                        { label: "Stale", command: "Stale" },
                        { label: "Disconnected", command: "Disconnect" }
                    ]
                    delegate: QQC2.Button {
                        required property var modelData
                        text: modelData.label
                        flat: true
                        onClicked: page.run("trigger", modelData.command)
                    }
                }
            }
        }

        SettingsCard {
            Layout.maximumWidth: testingColumn.width
            title: "Session events"
            description: "Fastest lap fires once; the chequered flag ends the session."
            enabledCard: page.playbackActive
            RowLayout {
                spacing: Kirigami.Units.smallSpacing
                Repeater {
                    model: [
                        { label: "Fastest lap", command: "FastestLap" },
                        { label: "Chequered", command: "Chequered" }
                    ]
                    delegate: QQC2.Button {
                        required property var modelData
                        text: modelData.label
                        flat: true
                        onClicked: page.run("trigger", modelData.command)
                    }
                }
            }
        }
    }

    // Created only when the user asks to browse. plasmashell builds config pages off-scene, so a
    // dialog instantiated at build time would open a window before the page is visible.
    Component {
        id: archivePickerComponent

        FileDialog {
            title: "Choose timing archive"
            fileMode: FileDialog.OpenFile
            nameFilters: [
                "Timing archives (*.zip *.har *.ndjson)",
                "Timing71 exports (*.zip)",
                "HTTP archives (*.har)",
                "RaceDock F1/GTWC/WEC dumps (*.ndjson)",
                "All files (*)"
            ]
            onAccepted: {
                archiveField.text = page.localPath(selectedFile);
                page.loadArchive(archiveField.text);
                archivePicker.active = false;
            }
            onRejected: archivePicker.active = false
            Component.onCompleted: open()
        }
    }
}
