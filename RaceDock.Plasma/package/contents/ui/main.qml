// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Layouts
import QtQuick.Window
import org.kde.plasma.plasmoid
import org.kde.plasma.core as PlasmaCore
import org.kde.kirigami as Kirigami

PlasmoidItem {
    id: root

    property bool pinned: false
    property var client: raceDockClient
    readonly property var widgetState: raceDockClient.widgetState
    readonly property var flyoutState: raceDockClient.flyoutState

    preferredRepresentation: compactRepresentation
    Plasmoid.backgroundHints: PlasmaCore.Types.NoBackground

    Plasmoid.toolTipMainText: {
        if (widgetState.mode === "live") {
            if (widgetState.alert && widgetState.alert.title)
                return widgetState.alert.title;
            if (widgetState.flag && widgetState.flag.label)
                return widgetState.championshipName + " · " + widgetState.flag.label;
            return (widgetState.fullEvent || widgetState.event || "RaceDock") + " · " + (widgetState.session || "");
        }
        if (widgetState.mode === "idle" && widgetState.idle)
            return widgetState.idle.headline;
        return "RaceDock";
    }
    Plasmoid.toolTipSubText: {
        if (widgetState.mode === "live")
            return (widgetState.progress || "") + (widgetState.alert && widgetState.alert.detail ? "\n" + widgetState.alert.detail : "");
        if (widgetState.mode === "idle" && widgetState.idle)
            return widgetState.idle.tooltip || widgetState.idle.detail;
        return raceDockClient.connected ? "No session on track" : "RaceDock service unavailable";
    }

    function toggleFlyout() {
        if (root.pinned) {
            root.pinned = false;
            return;
        }
        root.expanded = !root.expanded;
    }

    function pinFromAnchored(shouldPin) {
        if (shouldPin) {
            root.expanded = false;
            root.pinned = true;
        } else {
            root.pinned = false;
        }
    }

    compactRepresentation: CompactWidget {
        client: raceDockClient
        onToggleRequested: root.toggleFlyout()
    }

    fullRepresentation: FlyoutPanel {
        client: raceDockClient
        pinned: false
        Layout.preferredWidth: implicitWidth
        Layout.preferredHeight: implicitHeight
        onCloseRequested: root.expanded = false
        onPinRequested: function (shouldPin) { root.pinFromAnchored(shouldPin); }
        onConfigureRequested: {
            root.expanded = false;
            Plasmoid.internalAction("configure").trigger();
        }
    }

    PlasmaCore.Dialog {
        id: floatingDialog

        visible: root.pinned
        type: PlasmaCore.Dialog.DialogWindow
        location: PlasmaCore.Types.Floating
        flags: Qt.WindowStaysOnTopHint
        hideOnWindowDeactivate: false
        backgroundHints: PlasmaCore.Dialog.NoBackground
        width: floatingPanel.implicitWidth
        height: floatingPanel.implicitHeight
        x: Math.max(0, Screen.desktopAvailableWidth - width - 32)
        y: Math.max(0, 48)

        onVisibleChanged: {
            if (!visible && root.pinned)
                root.pinned = false;
            else if (visible)
                floatingPanel.forceActiveFocus();
        }

        mainItem: Item {
            width: floatingDialog.width
            height: floatingDialog.height

            FlyoutPanel {
                id: floatingPanel
                anchors.fill: parent
                client: raceDockClient
                pinned: true
                hostWindow: floatingDialog
                onCloseRequested: root.pinned = false
                onPinRequested: function (shouldPin) {
                    if (!shouldPin)
                        root.pinned = false;
                }
                onConfigureRequested: Plasmoid.internalAction("configure").trigger()
            }
        }
    }

    RaceDockClient {
        id: raceDockClient
        pollFlyout: root.expanded || root.pinned
    }

    Connections {
        target: raceDockClient

        function onWidgetUpdated() {
            // The root ready property is intentionally untouched; keeping the applet passive while
            // idle lets Plasma reclaim panel space when the service says "hidden".
        }

        function onSettingsUpdated() {
        }
    }

    onExpandedChanged: {
        if (expanded) {
            raceDockClient.refreshFlyout();
            if (!raceDockClient.settingsLoaded)
                raceDockClient.loadSettings();
        }
    }

    onPinnedChanged: {
        if (pinned) {
            raceDockClient.refreshFlyout();
            if (!raceDockClient.settingsLoaded)
                raceDockClient.loadSettings();
        }
    }

    Component.onCompleted: {
        raceDockClient.loadSettings();
    }
}
