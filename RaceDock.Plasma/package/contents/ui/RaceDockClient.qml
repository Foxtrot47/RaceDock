// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import org.kde.plasma.workspace.dbus as DBus

/// Small polling client for the RaceDock sidecar. The Plasma D-Bus QML module does not expose signal
/// subscriptions, so the widget and flyout poll immutable JSON snapshots instead.
Item {
    id: client
    visible: false
    width: 0
    height: 0

    readonly property string service: "org.foxtrot47.RaceDock"
    readonly property string path: "/org/foxtrot47/RaceDock"
    readonly property string iface: "org.foxtrot47.RaceDock1"

    property var widgetState: ({
        mode: "hidden",
        viewMode: "live",
        competitors: [],
        display: { singleRow: false, competitorCount: 5, animations: true, positionColors: true, widthHint: 520 },
        updatedAt: ""
    })
    property var flyoutState: ({
        mode: "hidden",
        rows: [],
        columns: [],
        schedule: [],
        splitPoints: [],
        width: 460,
        height: 320
    })
    property var settings: ({})
    property bool connected: false
    property bool serviceAvailable: false
    property string lastError: ""
    property bool pollFlyout: false
    property bool pollWidget: true
    property bool settingsLoaded: false

    signal widgetUpdated()
    signal flyoutUpdated()
    signal settingsUpdated()
    signal settingsApplied(bool ok, string message)

    function messageFor(member, args) {
        const message = new DBus.dbusMessage({
            service: client.service,
            path: client.path,
            iface: client.iface,
            interface: client.iface,
            member: member
        });
        // The Plasma D-Bus plugin reads service/path/interface/member from the constructor map but
        // ignores `arguments`, and an explicit `signature` makes the call arrive with no arguments at
        // all. Assigning the arguments afterwards works, and Qt then derives the wire signature.
        message.arguments = args || [];
        return message;
    }

    function replyValue(reply) {
        if (reply.value !== undefined && reply.value !== null)
            return reply.value;
        if (reply.values !== undefined && reply.values.length > 0)
            return reply.values[0];
        return "";
    }

    function call(message, onSuccess, onFailure) {
        const reply = DBus.SessionBus.asyncCall(message);
        reply.finished.connect(() => {
            if (reply.isError) {
                const messageText = reply.error && reply.error.message ? reply.error.message : "RaceDock service unavailable";
                client.connected = false;
                client.serviceAvailable = false;
                client.lastError = messageText;
                if (onFailure)
                    onFailure(messageText);
                return;
            }
            client.connected = true;
            client.serviceAvailable = true;
            client.lastError = "";
            if (onSuccess)
                onSuccess(replyValue(reply));
        });
    }

    function refreshWidget() {
        call(messageFor("GetWidgetState", []), raw => {
            try {
                client.widgetState = JSON.parse(String(raw));
                client.widgetUpdated();
            } catch (exception) {
                client.lastError = "Invalid widget payload: " + exception;
            }
        });
    }

    function refreshFlyout() {
        call(messageFor("GetFlyoutState", []), raw => {
            try {
                client.flyoutState = JSON.parse(String(raw));
                client.flyoutUpdated();
            } catch (exception) {
                client.lastError = "Invalid flyout payload: " + exception;
            }
        });
    }

    function loadSettings() {
        call(messageFor("GetSettings", []), raw => {
            try {
                client.settings = JSON.parse(String(raw));
                client.settingsLoaded = true;
                client.settingsUpdated();
            } catch (exception) {
                client.lastError = "Invalid settings payload: " + exception;
            }
        });
    }

    function applySettings(value, done) {
        call(messageFor("ApplySettings", [JSON.stringify(value)]), raw => {
            let result = ({ ok: false, error: raw });
            try { result = JSON.parse(String(raw)); } catch (exception) { }
            if (result.ok && result.settings) {
                client.settings = result.settings;
                client.settingsUpdated();
            }
            if (done)
                done(result);
            client.settingsApplied(result.ok === true, result.error || "");
        }, message => {
            if (done)
                done({ ok: false, error: message });
            client.settingsApplied(false, message);
        });
    }

    function command(name, argument, done) {
        call(messageFor("Command", [name, argument || ""]), raw => {
            let result = ({ ok: false, error: raw });
            try { result = JSON.parse(String(raw)); } catch (exception) { }
            if (done)
                done(result);
        }, message => {
            if (done)
                done({ ok: false, error: message });
        });
    }

    Timer {
        interval: 1000
        running: client.pollWidget
        repeat: true
        onTriggered: client.refreshWidget()
    }

    Timer {
        interval: 750
        running: client.pollFlyout
        repeat: true
        onTriggered: client.refreshFlyout()
    }

    onPollFlyoutChanged: {
        if (pollFlyout)
            refreshFlyout();
    }

    Component.onCompleted: refreshWidget()
}
