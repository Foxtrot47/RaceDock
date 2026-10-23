// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Controls as QQC2
import QtQuick.Layouts
import org.kde.kcmutils as KCMUtils
import org.kde.kirigami as Kirigami
import "."

/// General settings page. The sidecar owns the settings file, so this page always round-trips a
/// complete `UserSettings` document: load with `GetSettings`, mutate a copy, send it back with
/// `ApplySettings`. Nothing here is stored in the applet's own configuration.
KCMUtils.SimpleKCM {
    id: page

    readonly property var catalog: SettingsCatalog {}
    /// A deep copy of the last settings document received from the service.
    property var draft: ({})
    property bool loaded: false
    property string status: ""

    RaceDockClient {
        id: client
        pollWidget: false
        pollFlyout: false
    }

    Component.onCompleted: client.loadSettings()

    Connections {
        target: client
        function onSettingsUpdated() {
            // Applying a change also emits settingsUpdated; only the first load seeds the draft so a
            // slow reply cannot overwrite what the user is editing.
            if (!page.loaded)
                page.loadDraft();
        }
    }

    Timer {
        id: applyTimer
        interval: 300
        onTriggered: page.flush()
    }

    function deepCopy(value) {
        return JSON.parse(JSON.stringify(value));
    }

    function loadDraft() {
        if (!client.settingsLoaded)
            return;
        draft = deepCopy(client.settings);
        loaded = true;
        syncControls();
    }

    function queueApply() {
        if (loaded)
            applyTimer.restart();
    }

    function flush() {
        if (!loaded)
            return;
        client.applySettings(deepCopy(draft), result => {
            page.status = result.ok
                ? "Saved to the RaceDock service."
                : "Could not save: " + (result.error || "unknown error");
        });
    }

    function restoreDefaults() {
        client.command("resetSettings", "", result => {
            if (result.ok && result.settings) {
                client.settings = result.settings;
                page.loadDraft();
                page.status = "Defaults restored.";
            } else {
                page.status = "Could not restore defaults: " + (result.error || "unknown error");
            }
        });
    }

    function profiles() {
        return draft.championshipDisplay || ({});
    }

    function forEachProfile(action) {
        const all = profiles();
        for (const key in all)
            action(all[key]);
    }

    function firstProfile() {
        const all = profiles();
        const keys = Object.keys(all);
        return keys.length > 0 ? all[keys[0]] : ({});
    }

    /// Writes control state from the draft. Controls only write back on user interaction (clicked,
    /// moved, activated), so assigning here cannot loop back into the draft.
    function syncControls() {
        rotationSlider.value = draft.rotationSeconds || 5;
        retentionSlider.value = draft.completedSessionRetentionMinutes || 5;
        taskbarCountSlider.value = firstProfile().taskbarCompetitorCount || 5;
        flyoutCountSlider.value = firstProfile().flyoutCompetitorCount || 20;
        singleRowSwitch.checked = draft.useSingleRowTaskbar === true;
        animationsSwitch.checked = draft.interfaceAnimations !== false;
        nextSessionSwitch.checked = draft.showNextSessionWhenIdle !== false;
        positionColorsSwitch.checked = firstProfile().flyoutShowPositionColors !== false;
        logosSwitch.checked = firstProfile().taskbarUseManufacturerLogos !== false;

        const hidden = draft.hiddenChampionships || [];
        for (let i = 0; i < championshipRepeater.count; ++i) {
            const box = championshipRepeater.itemAt(i);
            if (box)
                box.checked = hidden.indexOf(box.modelData.key) === -1;
        }
    }

    Kirigami.FormLayout {
        QQC2.Label {
            Kirigami.FormData.isSection: true
            text: "Changes are applied to the running RaceDock service immediately."
            color: Kirigami.Theme.disabledTextColor
            wrapMode: Text.WordWrap
            Layout.maximumWidth: Kirigami.Units.gridUnit * 26
        }

        GridLayout {
            Kirigami.FormData.label: "Championships:"
            columns: 3
            columnSpacing: Kirigami.Units.largeSpacing
            rowSpacing: 0

            Repeater {
                id: championshipRepeater
                model: page.catalog.championships

                delegate: QQC2.CheckBox {
                    required property var modelData
                    text: modelData.label
                    onClicked: {
                        const key = modelData.key;
                        const hidden = (page.draft.hiddenChampionships || []).filter(entry => entry !== key);
                        if (!checked)
                            hidden.push(key);
                        page.draft.hiddenChampionships = hidden;
                        page.queueApply();
                    }
                }
            }
        }

        RowLayout {
            Kirigami.FormData.label: "Rotate championships every:"
            QQC2.Slider {
                id: rotationSlider
                Layout.preferredWidth: Kirigami.Units.gridUnit * 16
                from: 2
                to: 30
                stepSize: 1
                snapMode: QQC2.Slider.SnapAlways
                onMoved: {
                    page.draft.rotationSeconds = Math.round(value);
                    page.queueApply();
                }
            }
            QQC2.Label { text: Math.round(rotationSlider.value) + " s" }
        }

        RowLayout {
            Kirigami.FormData.label: "Keep finished sessions for:"
            QQC2.Slider {
                id: retentionSlider
                Layout.preferredWidth: Kirigami.Units.gridUnit * 16
                from: 5
                to: 30
                stepSize: 1
                snapMode: QQC2.Slider.SnapAlways
                onMoved: {
                    page.draft.completedSessionRetentionMinutes = Math.round(value);
                    page.queueApply();
                }
            }
            QQC2.Label { text: Math.round(retentionSlider.value) + " min" }
        }

        RowLayout {
            Kirigami.FormData.label: "Taskbar competitors:"
            QQC2.Slider {
                id: taskbarCountSlider
                Layout.preferredWidth: Kirigami.Units.gridUnit * 16
                from: 1
                to: 5
                stepSize: 1
                snapMode: QQC2.Slider.SnapAlways
                onMoved: {
                    const count = Math.round(value);
                    page.forEachProfile(profile => profile.taskbarCompetitorCount = count);
                    page.queueApply();
                }
            }
            QQC2.Label { text: Math.round(taskbarCountSlider.value) }
        }

        RowLayout {
            Kirigami.FormData.label: "Flyout competitors:"
            QQC2.Slider {
                id: flyoutCountSlider
                Layout.preferredWidth: Kirigami.Units.gridUnit * 16
                from: 5
                to: 40
                stepSize: 1
                snapMode: QQC2.Slider.SnapAlways
                onMoved: {
                    const count = Math.round(value);
                    page.forEachProfile(profile => profile.flyoutCompetitorCount = count);
                    page.queueApply();
                }
            }
            QQC2.Label { text: Math.round(flyoutCountSlider.value) }
        }

        QQC2.Switch {
            id: singleRowSwitch
            Kirigami.FormData.label: "Single-row panel:"
            text: checked ? "On" : "Off"
            onClicked: {
                page.draft.useSingleRowTaskbar = checked;
                page.queueApply();
            }
        }

        QQC2.Switch {
            id: animationsSwitch
            Kirigami.FormData.label: "Animations:"
            text: checked ? "On" : "Off"
            onClicked: {
                page.draft.interfaceAnimations = checked;
                page.queueApply();
            }
        }

        QQC2.Switch {
            id: nextSessionSwitch
            Kirigami.FormData.label: "Show next session when idle:"
            text: checked ? "On" : "Off"
            onClicked: {
                page.draft.showNextSessionWhenIdle = checked;
                page.queueApply();
            }
        }

        QQC2.Switch {
            id: positionColorsSwitch
            Kirigami.FormData.label: "Position colours in the flyout:"
            text: checked ? "On" : "Off"
            onClicked: {
                page.forEachProfile(profile => profile.flyoutShowPositionColors = checked);
                page.queueApply();
            }
        }

        QQC2.Switch {
            id: logosSwitch
            Kirigami.FormData.label: "Manufacturer logos in the panel:"
            text: checked ? "On" : "Off"
            onClicked: {
                page.forEachProfile(profile => profile.taskbarUseManufacturerLogos = checked);
                page.queueApply();
            }
        }

        RowLayout {
            QQC2.Button {
                text: "Restore defaults"
                icon.name: "edit-reset"
                onClicked: page.restoreDefaults()
            }
        }
    }

    footer: QQC2.Label {
        text: page.status
        visible: text.length > 0
        color: Kirigami.Theme.disabledTextColor
        padding: Kirigami.Units.smallSpacing
    }
}
