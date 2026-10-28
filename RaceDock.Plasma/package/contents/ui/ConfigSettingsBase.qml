// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Controls as QQC2
import org.kde.kcmutils as KCMUtils
import org.kde.kirigami as Kirigami

/// Shared plumbing for the configuration pages. The sidecar owns the settings file, so every page
/// round-trips a complete `UserSettings` document: load with `GetSettings`, mutate a copy, send it
/// back with `ApplySettings`. Partial documents must never be sent because missing properties would
/// fall back to defaults. Pages override `syncControls()` to push the draft into their controls.
KCMUtils.SimpleKCM {
    id: page

    /// A deep copy of the last settings document received from the service.
    property var draft: ({})
    property bool loaded: false
    property string status: ""
    /// Pages that need live widget state (the Testing transport bar) turn this on.
    property bool pollWidgetEnabled: false

    // The placeholder kcfg group in main.xml exists only so Plasma offers the Configure action.
    // Plasma sets cfg_* initial properties for its entries on every config page; absorbing them
    // here keeps the pages warning-free. The real settings live in the service.
    property var cfg_settingsVersion: 0
    property var cfg_settingsVersionDefault: 0

    readonly property var catalog: SettingsCatalog {}

    property RaceDockClient client: RaceDockClient {
        pollWidget: page.pollWidgetEnabled
        pollFlyout: false
    }

    Component.onCompleted: client.loadSettings()

    Connections {
        target: page.client

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

    /// Pages override this to write control state from the draft. Controls only write back on user
    /// interaction, so assigning here cannot loop back into the draft.
    function syncControls() {
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

    function profileFor(key) {
        return profiles()[key] || ({});
    }

    function writeProfile(key, mutate) {
        const all = deepCopy(profiles());
        const profile = all[key] || ({});
        mutate(profile);
        all[key] = profile;
        draft.championshipDisplay = all;
    }

    function championshipEnabled(key) {
        const hidden = draft.hiddenChampionships || [];
        return hidden.indexOf(key) === -1;
    }

    function setChampionshipEnabled(key, enabled) {
        const hidden = (draft.hiddenChampionships || []).filter(entry => entry !== key);
        if (!enabled)
            hidden.push(key);
        draft.hiddenChampionships = hidden;
        // The service derives the hidden list and the power switches from each other, but the feed
        // set reads the power switch, so both must be written with the correct flag name.
        draft[catalog.enableKeyFor(key)] = enabled;
        queueApply();
    }

    footer: QQC2.Label {
        text: page.status
        visible: text.length > 0
        color: Kirigami.Theme.disabledTextColor
        padding: Kirigami.Units.smallSpacing
    }
}
