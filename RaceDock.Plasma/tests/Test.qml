// SPDX-License-Identifier: GPL-3.0-or-later
// Integration harness for the Plasma front end. It talks to a running racedock-service on the session
// bus, so start one first:
//
//   dbus-run-session -- bash -lc '
//     dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario --paused \
//       --settings /tmp/opencode/racedock-test.json &
//     sleep 2
//     QT_QPA_PLATFORM=offscreen qmltestrunner -input RaceDock.Plasma/tests/Test.qml
//   '
//
// Always pass --settings: the harness applies settings and runs `resetSettings`, which would
// otherwise rewrite the user's real ~/.local/share/RaceDock/settings.json.
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

    ConfigChampionshipPage {
        championship: "formula1"
        id: championshipsPage
    }

    ConfigChampionshipPage {
        championship: "europeanLeMansSeries"
        id: elmsPage
    }

    ConfigTesting {
        id: testingPage
    }

    // The pinned overlay is a detached top-level window created by main.qml. Declaring it here
    // checks that the host loads, sizes itself from the flyout, and stays hidden until shown.
    PinnedFlyout {
        id: pinnedHost
        client: client
    }

    function test_pinned_host() {
        verify(!pinnedHost.visible);
        verify(pinnedHost.width > 300);
        verify(pinnedHost.height > 200);
        pinnedHost.visible = true;
        wait(200);
        verify(pinnedHost.visible);
        // A dismissed overlay only hides itself; the applet owns the pin state.
        pinnedHost.visible = false;
        wait(100);
        verify(!pinnedHost.visible);
    }

    function test_live_state() {
        client.refreshWidget();
        client.refreshFlyout();
        wait(3000);
        verify(client.connected);
        compare(client.widgetState.mode, "live");
        compare(client.flyoutState.mode, "live");
        compare(client.widgetState.viewMode, "scenario");
        verify(compact.implicitWidth > 100);
        verify(flyout.implicitWidth > 300);
        // Core resolves the manufacturer mark even though the flyout only draws it when the
        // profile asks for it; the widget uses the same field.
        verify(client.widgetState.competitors.length > 0);
        verify(!!client.widgetState.competitors[0].manufacturerLogo);
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

    function test_testing_page() {
        testingPage.run("refreshSchedule", "");
        wait(2000);
        compare(testingPage.status, "Command 'refreshSchedule' accepted.");
        compare(testingPage.viewMode, "scenario");
        verify(testingPage.playbackActive);
    }

    function test_championship_enable() {
        // ELMS uses `enableElms`, not a name derived from the enum key. The page must write the
        // real power switch, because the service builds the feed set from those flags: a page that
        // only clears `hiddenChampionships` leaves the championship visible but never fetched.
        wait(1500);
        verify(elmsPage.loaded);
        const wasEnabled = elmsPage.championshipEnabled("europeanLeMansSeries");
        elmsPage.setChampionshipEnabled("europeanLeMansSeries", true);
        wait(900);
        client.loadSettings();
        wait(800);
        compare(client.settings.enableElms, true);
        compare(client.settings.hiddenChampionships.indexOf("europeanLeMansSeries"), -1);

        elmsPage.setChampionshipEnabled("europeanLeMansSeries", wasEnabled);
        wait(900);
    }

    function test_archive_picker_path() {
        // The Browse button hands the picker's file:// URL to the service, so it has to become a
        // plain path first, and a failed load has to be visible in the card instead of the footer.
        compare(testingPage.localPath("file:///tmp/racedock%20dump.ndjson"), "/tmp/racedock dump.ndjson");
        compare(testingPage.localPath("/tmp/plain.ndjson"), "/tmp/plain.ndjson");
        compare(testingPage.localPath(""), "");

        // A picked file that the service cannot open surfaces the loader error in the card.
        testingPage.loadArchive("file:///tmp/racedock-missing-pick.ndjson".substring(7));
        wait(1500);
        verify(testingPage.archiveFailed);
        verify(testingPage.archiveStatus.indexOf("Could not load the archive") === 0);

        // An empty path explains itself rather than leaving the button dead.
        testingPage.loadArchive("   ");
        verify(testingPage.archiveStatus.indexOf("Choose a file with Browse") === 0);
    }

    function test_archive_replay() {
        // A missing archive must surface the loader error instead of silently switching sources.
        testingPage.loadArchive("/tmp/racedock-missing-dump.ndjson");
        wait(1500);
        verify(testingPage.status.indexOf("Could not load the archive") === 0);

        // The scenario source the harness started with is still driving the widget.
        verify(testingPage.viewMode === "scenario" || testingPage.viewMode === "live");
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
        verify(championshipsPage.loaded);
        verify(generalPage.draft.rotationSeconds >= 2);
        compare(championshipsPage.championship, "formula1");
        // Formula 1 supports sector timing but not stage splits or energy.
        const order = championshipsPage.order;
        verify(order.indexOf("sectors") !== -1);
        verify(order.indexOf("splits") === -1);
        verify(order.indexOf("energy") === -1);
        verify(championshipsPage.columnVisible("interval"));

        // A page edits its draft copy and sends the complete document back.
        generalPage.draft.rotationSeconds = 11;
        generalPage.flush();
        wait(900);
        compare(generalPage.status, "Saved to the RaceDock service.");
        client.loadSettings();
        wait(800);
        compare(client.settings.rotationSeconds, 11);

        // Championship edits work the same way and only touch the selected championship.
        const firstColumn = championshipsPage.order[0];
        championshipsPage.move(0, 1);
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
