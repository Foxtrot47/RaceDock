// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Window

/// Detached top-level host for the pinned flyout. It briefly uses the anchored popup as its
/// transient parent for compositor placement, then detaches before that popup closes.
Window {
    id: host

    required property var client
    property real initialX: 0
    property real initialY: 0
    property var anchorWindow: null

    signal dismissed()
    signal unpinRequested()
    signal configureRequested()
    signal anchorDetached()

    visible: false
    title: "RaceDockPinned"
    transientParent: anchorWindow
    flags: Qt.Dialog | Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint
    color: "transparent"
    width: panel.implicitWidth
    height: panel.implicitHeight
    x: initialX
    y: initialY

    onVisibleChanged: {
        if (visible && anchorWindow)
            detachAnchorTimer.restart();
        else
            detachAnchorTimer.stop();
    }

    Timer {
        id: detachAnchorTimer
        interval: 150
        onTriggered: {
            host.anchorWindow = null;
            host.anchorDetached();
        }
    }

    onClosing: function (close) {
        close.accepted = true;
        host.dismissed();
    }

    FlyoutPanel {
        id: panel
        anchors.fill: parent
        client: host.client
        pinned: true
        hostWindow: host
        onCloseRequested: host.dismissed()
        onPinRequested: function (shouldPin) {
            if (!shouldPin)
                host.unpinRequested();
        }
        onConfigureRequested: host.configureRequested()
    }
}
