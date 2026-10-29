// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Layouts
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

    function pinFromAnchored(shouldPin) {
        if (shouldPin) {
            root.expanded = false;
            root.pinned = true;
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

    // The pinned host is created on demand with no parent item, so it is not transient for the
    // panel. See PinnedFlyout.qml for why that matters.
    Component {
        id: pinnedComponent
        PinnedFlyout {
            onDismissed: root.pinned = false
            onUnpinRequested: root.unpinToFlyout()
            onConfigureRequested: Plasmoid.internalAction("configure").trigger()
        }
    }

    property var pinnedWindow: null

    function showPinnedWindow() {
        if (!pinnedWindow) {
            pinnedWindow = pinnedComponent.createObject(null, { client: raceDockClient });
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
