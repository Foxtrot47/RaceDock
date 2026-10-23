// SPDX-License-Identifier: GPL-3.0-or-later
// Integration harness for the Plasma front end. It talks to a running racedock-service on the session
// bus, so start one first:
//
//   dbus-run-session -- bash -lc '
//     dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario &
//     sleep 2
//     QT_QPA_PLATFORM=offscreen qmltestrunner -input RaceDock.Plasma/tests/Test.qml
//   '
//
// This lives outside the shipped package: `kpackagetool6` installs everything under contents/.
import QtQuick
import QtTest
import "../package/contents/ui"

TestCase {
    name: "RaceDockUi"

    RaceDockClient {
        id: client
    }

    CompactWidget {
        id: compact
        client: client
    }

    FlyoutPanel {
        id: flyout
        client: client
    }

    ConfigGeneral {
        id: generalPage
    }

    ConfigColumns {
        id: columnsPage
    }

    ConfigDeveloper {
        id: developerPage
    }

    function test_live_state() {
        client.refreshWidget();
        client.refreshFlyout();
        wait(3000);
        verify(client.connected);
        compare(client.widgetState.mode, "live");
        compare(client.flyoutState.mode, "live");
        verify(compact.implicitWidth > 100);
        verify(flyout.implicitWidth > 300);
    }

    function test_settings_and_commands() {
        let commandResult = null;
        client.command("pause", "", result => { commandResult = result; });
        wait(800);
        verify(commandResult !== null);
        verify(commandResult.ok);

        client.loadSettings();
        wait(800);
        verify(client.settingsLoaded);
        verify(client.settings.rotationSeconds >= 2);

        let applied = null;
        client.applySettings(client.settings, result => { applied = result; });
        wait(1200);
        verify(applied !== null);
        verify(applied.ok);
    }

    function test_developer_page() {
        developerPage.run("refreshSchedule", "");
        wait(2000);
        compare(developerPage.status, "Command 'refreshSchedule' accepted.");
    }

    function test_settings_reset() {
        client.loadSettings();
        wait(800);
        verify(client.settingsLoaded);

        const mutated = JSON.parse(JSON.stringify(client.settings));
        mutated.rotationSeconds = 9;
        let applied = null;
        client.applySettings(mutated, result => { applied = result; });
        wait(1200);
        verify(applied !== null);
        verify(applied.ok);
        compare(applied.settings.rotationSeconds, 9);

        let reset = null;
        client.command("resetSettings", "", result => { reset = result; });
        wait(800);
        verify(reset !== null);
        verify(reset.ok);
        compare(reset.settings.rotationSeconds, 5);
    }

    function test_config_pages() {
        // The pages load the complete settings document when they are created.
        wait(1500);
        verify(generalPage.loaded);
        verify(columnsPage.loaded);
        verify(generalPage.draft.rotationSeconds >= 2);
        compare(columnsPage.order.length, 9);
        compare(columnsPage.championship, "formula1");
        verify(columnsPage.columnVisible("interval"));

        // A page edits its draft copy and sends the complete document back.
        generalPage.draft.rotationSeconds = 11;
        generalPage.flush();
        wait(900);
        compare(generalPage.status, "Saved to the RaceDock service.");
        client.loadSettings();
        wait(800);
        compare(client.settings.rotationSeconds, 11);

        // Column edits work the same way and only touch the selected championship.
        const firstColumn = columnsPage.order[0];
        columnsPage.move(0, 1);
        columnsPage.flush();
        wait(900);
        client.loadSettings();
        wait(800);
        compare(client.settings.championshipDisplay.formula1.flyoutColumnOrder[1], firstColumn);

        generalPage.restoreDefaults();
        wait(900);
        compare(generalPage.status, "Defaults restored.");
        client.loadSettings();
        wait(800);
        compare(client.settings.rotationSeconds, 5);
    }
}
