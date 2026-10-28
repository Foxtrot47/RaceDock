// SPDX-License-Identifier: GPL-3.0-or-later
// Reconnect harness. It needs the sidecar to be stopped and started again while it polls, so it is
// driven from the shell rather than run on its own:
//
//   dbus-run-session -- bash -lc '
//     dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario --paused --settings /tmp/rc.json &
//     pid=$!
//     sleep 3
//     QT_QPA_PLATFORM=offscreen qmltestrunner -input RaceDock.Plasma/tests/Reconnect.qml &
//     runner=$!
//     sleep 4
//     kill $pid; wait $pid
//     sleep 5
//     dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario --paused --settings /tmp/rc.json &
//     pid=$!
//     wait $runner
//     kill $pid
//   '
//
// A poll against a vanished service must report the outage (connected = false, lastError set) without
// throwing, and the polls after the replacement takes the name must recover on their own. `--settings`
// keeps the harness off the real settings file.
import QtQuick
import QtTest
import "../package/contents/ui"

TestCase {
    name: "RaceDockReconnect"

    RaceDockClient {
        id: client
        pollFlyout: true
    }

    function test_survives_sidecar_restart() {
        client.refreshWidget();
        client.refreshFlyout();
        tryVerify(function() { return client.connected && client.widgetState.mode === "live"; },
            15000, "the first sidecar never produced a live snapshot");

        // The orchestration script stops the service now; the next poll must notice.
        tryVerify(function() { return client.connected === false; }, 30000,
            "the client never reported the stopped sidecar");
        verify(client.lastError.length > 0);

        // ...and the polls after the replacement owns the name must recover without a restart.
        tryVerify(function() { return client.connected === true; }, 60000,
            "the client never reconnected to the replacement sidecar");
        // A fresh sidecar can publish "hidden" until its source produces the first snapshot.
        tryVerify(function() { return client.widgetState.mode === "live"; }, 15000,
            "the widget never left the hidden startup state after the restart");
        tryVerify(function() { return client.flyoutState.mode === "live"; }, 15000,
            "the flyout never left the hidden startup state after the restart");
        compare(client.lastError, "");
    }
}
