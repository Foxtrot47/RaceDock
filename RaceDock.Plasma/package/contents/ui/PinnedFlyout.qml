// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Window

/// The pinned, free-floating flyout host. This is a detached top-level window on purpose: a
/// Window declared inside the applet is automatically transient for the panel, and the window
/// manager tags that as an applet popup and dismisses it whenever another window is activated.
/// main.qml creates this through a Component with no parent, so it is a real overlay.
Window {
    id: host

    required property var client

    signal dismissed()
    signal unpinRequested()
    signal configureRequested()

    visible: false
    title: "RaceDockPinned"
    transientParent: null
    flags: Qt.Dialog | Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint
    color: "transparent"
    width: panel.implicitWidth
    height: panel.implicitHeight
    x: Math.max(0, Screen.desktopAvailableWidth - width - 32)
    y: Math.max(0, 48)

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
