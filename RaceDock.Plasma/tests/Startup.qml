// SPDX-License-Identifier: GPL-3.0-or-later
// Startup harness: the applet can be loaded before the sidecar exists (D-Bus activation happens on the
// first call, so the first polls fail). It is driven from the shell:
//
//   dbus-run-session -- bash -lc '
//     QT_QPA_PLATFORM=offscreen qmltestrunner -input RaceDock.Plasma/tests/Startup.qml &
//     runner=$!
//     sleep 5
//     dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario --paused \
//       --settings /tmp/startup.json &
//     pid=$!
//     wait $runner
//     kill $pid
//   '
//
// The polls that run before the service exists must report an outage rather than throw, and the next
// poll after it takes the name must connect.
import QtQuick
import QtTest
import "../package/contents/ui"

TestCase {
    name: "RaceDockStartup"

    RaceDockClient {
        id: client
        pollFlyout: true
    }

    function test_connects_when_the_sidecar_appears() {
        client.refreshWidget();
        tryVerify(function() { return client.connected === false; }, 5000,
            "the client connected without a sidecar");
        verify(client.lastError.length > 0);

        tryVerify(function() { return client.connected === true; }, 60000,
            "the client never connected to the sidecar that started later");
        // The first snapshot can still be "hidden" for a moment while the source starts.
        tryVerify(function() { return client.widgetState.mode === "live"; }, 15000,
            "the widget never left the hidden startup state");
        tryVerify(function() { return client.flyoutState.mode === "live"; }, 15000,
            "the flyout never left the hidden startup state");
    }
}
