// SPDX-License-Identifier: GPL-3.0-or-later
// Renders the applet surfaces and config pages to PNG files so their layout can be inspected
// without a Plasma session. Run it from the directory that should receive the images, with a
// sidecar on the session bus:
//
//   dbus-run-session -- bash -lc '
//     dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario --paused &
//     sleep 3
//     mkdir -p /tmp/racedock-shots && cd /tmp/racedock-shots
//     QT_QPA_PLATFORM=offscreen qmltestrunner -input "$OLDPWD/RaceDock.Plasma/tests/Screenshots.qml"
//   '
//
// The files land in the working directory: panel.png, flyout.png, config-general.png,
// config-columns.png, config-developer.png. This lives outside contents/, so the package does not
// ship it. The offscreen platform has no Breeze style, so colours come from Kirigami's fallback
// theme; the captures verify layout and content, not the exact Plasma palette.
import QtQuick
import QtTest
import org.kde.kirigami as Kirigami
import "../package/contents/ui"

TestCase {
    name: "RaceDockScreenshots"
    width: 1200
    height: 2400
    visible: true

    RaceDockClient {
        id: client
    }

    Column {
        spacing: 12

        Rectangle {
            id: panelFrame
            width: strip.width + 24
            height: strip.height + 16
            color: "#232629"

            CompactWidget {
                id: strip
                client: client
                anchors.centerIn: parent
                width: implicitWidth
                height: implicitHeight
            }
        }

        Rectangle {
            id: flyoutFrame
            width: flyout.width + 24
            height: flyout.height + 24
            color: "#1b1e20"

            FlyoutPanel {
                id: flyout
                client: client
                anchors.centerIn: parent
                width: implicitWidth
                height: implicitHeight
            }
        }

        Rectangle {
            id: generalFrame
            width: 720
            height: generalPage.implicitHeight
            color: Kirigami.Theme.backgroundColor

            ConfigGeneral {
                id: generalPage
                width: parent.width
                height: parent.height
            }
        }

        Rectangle {
            id: columnsFrame
            width: 720
            height: columnsPage.implicitHeight
            color: Kirigami.Theme.backgroundColor

            ConfigColumns {
                id: columnsPage
                width: parent.width
                height: parent.height
            }
        }

        Rectangle {
            id: developerFrame
            width: 720
            height: developerPage.implicitHeight
            color: Kirigami.Theme.backgroundColor

            ConfigDeveloper {
                id: developerPage
                width: parent.width
                height: parent.height
            }
        }
    }

    function test_render_single_row() {
        if (client.widgetState.mode !== "live")
            return;

        client.loadSettings();
        tryVerify(function() { return client.settingsLoaded; }, 4000);
        const edited = JSON.parse(JSON.stringify(client.settings));
        edited.useSingleRowTaskbar = true;
        let applied = null;
        client.applySettings(edited, result => { applied = result; });
        tryVerify(function() { return applied !== null; }, 5000);
        verify(applied.ok);
        client.refreshWidget();
        tryVerify(function() { return client.widgetState.display.singleRow === true; }, 4000);
        wait(600);
        grab(panelFrame, "panel-single-row.png");

        // The flag chip shares the combined line, so check it does not push the cells around.
        let answered = false;
        client.command("trigger", "yellow", result => { answered = true; });
        tryVerify(function() { return answered; }, 3000);
        wait(1800);
        grab(panelFrame, "panel-single-row-yellow.png");

        answered = false;
        client.command("trigger", "green", result => { answered = true; });
        tryVerify(function() { return answered; }, 3000);
    }

    function grab(item, file) {
        let saved = false;
        item.grabToImage(result => { saved = result.saveToFile(file); });
        tryVerify(function() { return saved; }, 5000, "could not write " + file);
    }

    function test_render_live_and_flags() {
        client.refreshWidget();
        client.refreshFlyout();
        wait(2500);
        verify(client.connected);
        verify(strip.width > 10);
        verify(flyout.width > 300);
        grab(panelFrame, "panel.png");
        grab(flyoutFrame, "flyout.png");

        // Track flags render as a chip in the panel strip; race-control alerts add a border.
        if (client.widgetState.mode !== "live")
            return;

        let answered = false;
        client.command("trigger", "yellow", result => { answered = true; });
        tryVerify(function() { return answered; }, 3000);
        wait(1800);
        grab(panelFrame, "panel-yellow.png");
        // The flyout header carries the same track condition as a badge.
        client.refreshFlyout();
        wait(900);
        grab(flyoutFrame, "flyout-yellow.png");

        answered = false;
        client.command("trigger", "redflag", result => { answered = true; });
        tryVerify(function() { return answered; }, 3000);
        wait(1800);
        grab(panelFrame, "panel-redflag.png");

        answered = false;
        client.command("trigger", "green", result => { answered = true; });
        tryVerify(function() { return answered; }, 3000);
    }

    function test_render_sectors() {
        if (client.widgetState.mode !== "live")
            return;

        client.loadSettings();
        tryVerify(function() { return client.settingsLoaded; }, 4000);
        const edited = JSON.parse(JSON.stringify(client.settings));
        const formula1 = edited.championshipDisplay.formula1;
        formula1.flyoutShowSectorTimes = true;
        formula1.flyoutShowGaps = true;
        let applied = null;
        client.applySettings(edited, result => { applied = result; });
        tryVerify(function() { return applied !== null; }, 5000);
        verify(applied.ok);
        client.refreshFlyout();
        wait(1200);
        grab(flyoutFrame, "flyout-sectors.png");
    }

    function test_render_config_pages() {
        wait(1500);
        verify(generalPage.loaded);
        verify(columnsPage.loaded);
        console.log("page heights:", generalPage.implicitHeight, columnsPage.implicitHeight,
                    developerPage.implicitHeight);
        grab(generalFrame, "config-general.png");
        grab(columnsFrame, "config-columns.png");
        grab(developerFrame, "config-developer.png");
    }

    function test_render_scrolled_classification() {
        if (client.flyoutState.mode !== "live")
            return;

        // Only a full classification has anything to scroll; the scenario and schedule views do not.
        if ((client.flyoutState.rows || []).length < 12)
            return;

        mouseWheel(flyout, flyout.width / 2, flyout.height / 2, 0, -600);
        wait(500);
        grab(flyoutFrame, "flyout-scrolled.png");
    }
}
