// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Controls as QQC2
import QtQuick.Layouts
import org.kde.kcmutils as KCMUtils
import org.kde.kirigami as Kirigami
import "."

/// Column order and visibility for one championship at a time. The service still decides widths,
/// labels, and whether a feed can fill a column, so this page only edits the settings document.
KCMUtils.SimpleKCM {
    id: page

    readonly property var catalog: SettingsCatalog {}
    property var draft: ({})
    property bool loaded: false
    /// Enum key of the championship being edited, for example "formula1".
    property string championship: "formula1"
    /// A copy of the current profile's order, so the Repeater rebuilds when it changes.
    property var order: []
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
        syncOrder();
    }

    function profile() {
        const all = draft.championshipDisplay || ({});
        if (!all[championship]) {
            all[championship] = ({});
            draft.championshipDisplay = all;
        }
        return all[championship];
    }

    function syncOrder() {
        order = (profile().flyoutColumnOrder || []).slice();
    }

    function columnVisible(key) {
        const column = catalog.columnFor(key);
        if (!column.flag)
            return true;
        return profile()[column.flag] !== false;
    }

    function setVisible(key, visible) {
        const column = catalog.columnFor(key);
        if (!column.flag)
            return;
        profile()[column.flag] = visible;
        queueApply();
    }

    function move(index, delta) {
        const target = index + delta;
        if (target < 0 || target >= order.length)
            return;
        const next = order.slice();
        const moved = next.splice(index, 1)[0];
        next.splice(target, 0, moved);
        order = next;
        profile().flyoutColumnOrder = next;
        queueApply();
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

    Kirigami.FormLayout {
        QQC2.Label {
            Kirigami.FormData.isSection: true
            text: "Column changes are applied immediately and only affect the selected championship."
            color: Kirigami.Theme.disabledTextColor
            wrapMode: Text.WordWrap
            Layout.maximumWidth: Kirigami.Units.gridUnit * 26
        }

        QQC2.ComboBox {
            id: championshipCombo
            Kirigami.FormData.label: "Championship:"
            Layout.preferredWidth: Kirigami.Units.gridUnit * 18
            textRole: "label"
            valueRole: "key"
            model: page.catalog.championships
            onActivated: {
                page.championship = page.catalog.championships[index].key;
                page.syncOrder();
            }
        }

        ColumnLayout {
            Kirigami.FormData.label: "Columns:"
            spacing: Kirigami.Units.smallSpacing

            Repeater {
                model: page.order

                delegate: RowLayout {
                    id: columnRow
                    required property var modelData
                    required property int index
                    Layout.fillWidth: true
                    spacing: Kirigami.Units.smallSpacing

                    QQC2.CheckBox {
                        checked: page.columnVisible(modelData)
                        onClicked: page.setVisible(modelData, checked)
                    }

                    QQC2.Label {
                        Layout.fillWidth: true
                        text: page.catalog.columnFor(modelData).label
                    }

                    QQC2.ToolButton {
                        text: "↑"
                        enabled: columnRow.index > 0
                        Accessible.name: "Move column left"
                        onClicked: page.move(columnRow.index, -1)
                    }

                    QQC2.ToolButton {
                        text: "↓"
                        enabled: columnRow.index < page.order.length - 1
                        Accessible.name: "Move column right"
                        onClicked: page.move(columnRow.index, 1)
                    }
                }
            }
        }

        QQC2.Label {
            text: "Columns a championship's feed cannot fill stay hidden even when ticked."
            color: Kirigami.Theme.disabledTextColor
            wrapMode: Text.WordWrap
            Layout.maximumWidth: Kirigami.Units.gridUnit * 26
        }
    }

    footer: QQC2.Label {
        text: page.status
        visible: text.length > 0
        color: Kirigami.Theme.disabledTextColor
        padding: Kirigami.Units.smallSpacing
    }
}
