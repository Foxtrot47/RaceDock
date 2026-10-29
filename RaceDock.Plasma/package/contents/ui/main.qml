// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Layouts
import QtQuick.Window
import org.kde.plasma.plasmoid
import org.kde.plasma.core as PlasmaCore

PlasmoidItem {
    id: root

    property bool pinned: false
    property var client: raceDockClient
    readonly property var widgetState: raceDockClient.widgetState
    readonly property var flyoutState: raceDockClient.flyoutState

    preferredRepresentation: compactRepresentation
    Plasmoid.backgroundHints: PlasmaCore.Types.NoBackground

    // Plasma 6.3 removed the tooltip properties from the Applet object;
    // PlasmoidItem exposes them directly.
    toolTipMainText: {
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
    toolTipSubText: {
        if (widgetState.mode === "live")
            return (widgetState.progress || "") + (widgetState.alert && widgetState.alert.detail ? "\n" + widgetState.alert.detail : "");
        if (widgetState.mode === "idle" && widgetState.idle)
            return widgetState.idle.tooltip || widgetState.idle.detail;
        return raceDockClient.connected ? "No session on track" : "RaceDock service unavailable";
    }

    function toggleFlyout() {
        // A pinned overlay is independent of the panel: clicking the widget must not dismiss it.
        // Windows keeps the overlay up in this case too.
        if (root.pinned)
            return;
        root.expanded = !root.expanded;
    }

    function pinFromAnchored(shouldPin, popupPosition, popupWindow) {
        if (shouldPin) {
            const creatingPinnedWindow = !pinnedWindow;
            if (creatingPinnedWindow) {
                pinnedPosition = popupPosition;
                popupAnchorWindow = popupWindow;
            }
            root.pinned = true;
            if (!creatingPinnedWindow)
                root.expanded = false;
        } else {
            root.pinned = false;
        }
    }

    /// Unpinning returns the flyout to the anchored popup instead of making it vanish, which is
    /// what the Windows pin button does: the content stays on screen, only its host changes.
    function unpinToFlyout() {
        root.pinned = false;
        root.expanded = true;
    }

    compactRepresentation: CompactWidget {
        client: raceDockClient
        onToggleRequested: root.toggleFlyout()
    }

    fullRepresentation: FlyoutPanel {
        id: anchoredFlyout
        client: raceDockClient
        pinned: false
        Layout.preferredWidth: implicitWidth
        Layout.minimumWidth: implicitWidth
        Layout.maximumWidth: implicitWidth
        Layout.preferredHeight: implicitHeight
        Layout.minimumHeight: implicitHeight
        Layout.maximumHeight: implicitHeight
        onCloseRequested: root.expanded = false
        onPinRequested: function (shouldPin) {
            root.pinFromAnchored(shouldPin, anchoredFlyout.mapToGlobal(Qt.point(0, 0)),
                anchoredFlyout.Window.window);
        }
        onConfigureRequested: {
            root.expanded = false;
            Plasmoid.internalAction("configure").trigger();
        }
    }

    // Create the host detached from the applet. It temporarily uses the popup as a transient parent
    // for placement, then PinnedFlyout clears that relationship before the popup closes.
    Component {
        id: pinnedComponent
        PinnedFlyout {
            onDismissed: root.pinned = false
            onUnpinRequested: root.unpinToFlyout()
            onConfigureRequested: Plasmoid.internalAction("configure").trigger()
            onAnchorDetached: {
                root.popupAnchorWindow = null;
                root.expanded = false;
            }
        }
    }

    property var pinnedWindow: null
    property point pinnedPosition: Qt.point(0, 0)
    property var popupAnchorWindow: null

    function showPinnedWindow() {
        if (!pinnedWindow) {
            pinnedWindow = pinnedComponent.createObject(null, {
                client: raceDockClient,
                initialX: pinnedPosition.x,
                initialY: pinnedPosition.y,
                anchorWindow: popupAnchorWindow
            });
            if (!pinnedWindow)
                return;
        }
        pinnedWindow.visible = true;
        pinnedWindow.raise();
    }

    function hidePinnedWindow() {
        if (pinnedWindow)
            pinnedWindow.visible = false;
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
            showPinnedWindow();
        } else {
            hidePinnedWindow();
        }
    }

    Component.onCompleted: {
        raceDockClient.loadSettings();
    }

    Component.onDestruction: {
        if (pinnedWindow) {
            pinnedWindow.destroy();
            pinnedWindow = null;
        }
    }
}
