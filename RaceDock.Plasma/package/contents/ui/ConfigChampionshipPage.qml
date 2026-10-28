// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Controls as QQC2
import QtQuick.Layouts
import org.kde.kirigami as Kirigami
import "."

/// Per-championship settings: enable a championship, taskbar and flyout display options, and the
/// ordered column list. Entries, wording, and per-championship wording match the Windows
/// Championship page; every edit writes only the selected championship's display profile.
ConfigSettingsBase {
    id: page

    /// Enum key of the championship being edited, for example "formula1".
    property string championship: "formula1"
    /// A copy of the current profile's column order, so the Repeater rebuilds when it changes.
    property var order: []

    /// Service defaults for the per-column visibility flags, used when the profile omits one.
    readonly property var columnDefaults: ({
        gap: false,
        interval: true,
        lapTime: true,
        sectors: false,
        splits: false,
        tyre: true,
        category: true,
        status: true,
        energy: true
    })

    readonly property var presentation: catalog.presentationFor(championship)

    onChampionshipChanged: {
        if (loaded)
            syncControls();
    }

    function columnKeys() {
        return catalog.columnsFor(championship).filter(column => column.supported).map(column => column.key);
    }

    function syncControls() {
        const profile = profileFor(championship);
        masterSwitch.checked = championshipEnabled(championship);
        taskbarCountSlider.value = profile.taskbarCompetitorCount || 5;
        flyoutCountSlider.value = profile.flyoutCompetitorCount || 20;
        taskbarLogosSwitch.checked = profile.taskbarUseManufacturerLogos === true;
        flyoutLogosSwitch.checked = profile.flyoutUseManufacturerLogos === true;
        teamSwitch.checked = profile.flyoutShowTeamNames !== false;
        positionColorsSwitch.checked = profile.flyoutShowPositionColors !== false;
        sectorModeBox.currentIndex = profile.flyoutSectorTimeMode === "bestLap" ? 1 : 0;
        const supported = columnKeys();
        const stored = profile.flyoutColumnOrder || [];
        order = stored.filter(key => supported.indexOf(key) !== -1);
        for (let i = 0; i < supported.length; ++i) {
            if (order.indexOf(supported[i]) === -1)
                order.push(supported[i]);
        }
    }

    function columnVisible(key) {
        const column = catalog.columnFor(key, championship);
        const value = profileFor(championship)[column.flag];
        return value === undefined ? columnDefaults[key] === true : value === true;
    }

    function setColumnVisible(key, visible) {
        const column = catalog.columnFor(key, championship);
        writeProfile(championship, profile => { profile[column.flag] = visible; });
        queueApply();
    }

    /// Writes the edited order back to the profile. Columns this championship does not support keep
    /// their stored position because the service re-normalizes the full list.
    function flushOrder() {
        writeProfile(championship, profile => { profile.flyoutColumnOrder = order.slice(); });
        queueApply();
    }

    function move(from, to) {
        if (from < 0 || from >= order.length || to < 0 || to >= order.length || from === to)
            return;
        const keys = order.splice(from, 1);
        order.splice(to, 0, keys[0]);
        order = order.slice();
        flushOrder();
    }

    ColumnLayout {
        Layout.fillWidth: true
        Layout.maximumWidth: Kirigami.Units.gridUnit * 34
        spacing: 0

        // Header: the championship name with its power state spelled out, mirroring the Windows
        // header card. The switch is the same live-timing toggle.
        RowLayout {
            Layout.topMargin: Kirigami.Units.largeSpacing
            Layout.bottomMargin: Kirigami.Units.largeSpacing
            Layout.fillWidth: true
            spacing: Kirigami.Units.largeSpacing

            ColumnLayout {
                Layout.fillWidth: true
                spacing: 0

                QQC2.Label {
                    text: page.catalog.labelFor(page.championship)
                    font.weight: Font.Bold
                    font.pointSize: Kirigami.Theme.defaultFont.pointSize + 2
                }

                QQC2.Label {
                    text: masterSwitch.checked
                        ? "Live timing is on. This championship can appear in the widget."
                        : "Live timing is off. Nothing is fetched for this championship."
                    color: Kirigami.Theme.disabledTextColor
                    font.pointSize: Kirigami.Theme.smallFont.pointSize
                    wrapMode: Text.WordWrap
                    Layout.fillWidth: true
                }
            }

            QQC2.Switch {
                id: masterSwitch
                Layout.alignment: Qt.AlignRight | Qt.AlignVCenter
                QQC2.ToolTip.text: "Toggle live timing"
                Accessible.name: "Enable live timing"
                onClicked: page.setChampionshipEnabled(page.championship, checked)
            }
        }

        ColumnLayout {
            Layout.fillWidth: true
            spacing: 0
            opacity: masterSwitch.checked ? 1 : 0.5
            enabled: masterSwitch.checked

            QQC2.Label {
                Layout.topMargin: Kirigami.Units.largeSpacing
                text: "Taskbar widget"
                font.weight: Font.DemiBold
                color: Kirigami.Theme.disabledTextColor
            }

            SettingsCard {
                title: page.presentation.taskbarCountTitle
                description: page.presentation.taskbarCountDescription
                RowLayout {
                    QQC2.Slider {
                        id: taskbarCountSlider
                        Layout.preferredWidth: Kirigami.Units.gridUnit * 14
                        from: 1
                        to: 5
                        stepSize: 1
                        snapMode: QQC2.Slider.SnapAlways
                        onMoved: {
                            page.writeProfile(page.championship, profile => {
                                profile.taskbarCompetitorCount = Math.round(value);
                            });
                            page.queueApply();
                        }
                    }
                    QQC2.Label { text: Math.round(taskbarCountSlider.value) }
                }
            }

            SettingsCard {
                visible: page.presentation.showManufacturerSettings
                title: page.presentation.manufacturerTitle
                description: page.presentation.taskbarManufacturerDescription
                QQC2.Switch {
                    id: taskbarLogosSwitch
                    onClicked: {
                        page.writeProfile(page.championship, profile => {
                            profile.taskbarUseManufacturerLogos = checked;
                        });
                        page.queueApply();
                    }
                }
            }

            QQC2.Label {
                Layout.topMargin: Kirigami.Units.largeSpacing
                text: "Classification flyout"
                font.weight: Font.DemiBold
                color: Kirigami.Theme.disabledTextColor
            }

            SettingsCard {
                title: page.presentation.flyoutCountTitle
                description: page.presentation.flyoutCountDescription
                RowLayout {
                    QQC2.Slider {
                        id: flyoutCountSlider
                        Layout.preferredWidth: Kirigami.Units.gridUnit * 14
                        from: 5
                        to: 40
                        stepSize: 1
                        snapMode: QQC2.Slider.SnapAlways
                        onMoved: {
                            page.writeProfile(page.championship, profile => {
                                profile.flyoutCompetitorCount = Math.round(value);
                            });
                            page.queueApply();
                        }
                    }
                    QQC2.Label { text: Math.round(flyoutCountSlider.value) }
                }
            }

            SettingsCard {
                visible: page.presentation.showTeamSetting
                title: page.presentation.teamTitle
                description: page.presentation.teamDescription
                QQC2.Switch {
                    id: teamSwitch
                    onClicked: {
                        page.writeProfile(page.championship, profile => {
                            profile.flyoutShowTeamNames = checked;
                        });
                        page.queueApply();
                    }
                }
            }

            SettingsCard {
                visible: page.presentation.showManufacturerSettings
                title: page.presentation.manufacturerTitle
                description: page.presentation.flyoutManufacturerDescription
                QQC2.Switch {
                    id: flyoutLogosSwitch
                    onClicked: {
                        page.writeProfile(page.championship, profile => {
                            profile.flyoutUseManufacturerLogos = checked;
                        });
                        page.queueApply();
                    }
                }
            }

            SettingsCard {
                title: "Position colours"
                description: "Colour the leading position cards."
                QQC2.Switch {
                    id: positionColorsSwitch
                    onClicked: {
                        page.writeProfile(page.championship, profile => {
                            profile.flyoutShowPositionColors = checked;
                        });
                        page.queueApply();
                    }
                }
            }

            SettingsCard {
                visible: page.presentation.showSectorTimingSetting
                title: "Sector timing mode"
                description: "Choose current/last-lap sectors or the sectors from each competitor’s best lap. Unavailable sectors show —."
                QQC2.ComboBox {
                    id: sectorModeBox
                    model: ["Current / last lap", "Best lap"]
                    onActivated: {
                        page.writeProfile(page.championship, profile => {
                            profile.flyoutSectorTimeMode = currentIndex === 1 ? "bestLap" : "currentLast";
                        });
                        page.queueApply();
                    }
                }
            }

            QQC2.Label {
                Layout.topMargin: Kirigami.Units.largeSpacing
                text: "Columns"
                font.weight: Font.DemiBold
                color: Kirigami.Theme.disabledTextColor
            }

            QQC2.Label {
                text: "Shown after the position and driver, in this order."
                color: Kirigami.Theme.disabledTextColor
                font.pointSize: Kirigami.Theme.smallFont.pointSize
                wrapMode: Text.WordWrap
                Layout.fillWidth: true
            }

            Repeater {
                model: page.order

                delegate: RowLayout {
                    id: columnRow
                    required property int index
                    required property string modelData

                    Layout.fillWidth: true
                    Layout.topMargin: Kirigami.Units.smallSpacing
                    spacing: Kirigami.Units.smallSpacing

                    readonly property var column: page.catalog.columnFor(modelData, page.championship)

                    QQC2.Label {
                        text: (columnRow.index + 1)
                        color: Kirigami.Theme.disabledTextColor
                        Layout.preferredWidth: Kirigami.Units.gridUnit
                        horizontalAlignment: Text.AlignHCenter
                    }

                    ColumnLayout {
                        Layout.fillWidth: true
                        spacing: 0
                        QQC2.Label {
                            text: columnRow.column.title
                            elide: Text.ElideRight
                            Layout.fillWidth: true
                        }
                        QQC2.Label {
                            visible: columnRow.column.description.length > 0
                            text: columnRow.column.description
                            color: Kirigami.Theme.disabledTextColor
                            font.pointSize: Kirigami.Theme.smallFont.pointSize
                            wrapMode: Text.WordWrap
                            Layout.fillWidth: true
                        }
                    }

                    QQC2.Button {
                        icon.name: "arrow-up"
                        enabled: columnRow.index > 0
                        Accessible.name: "Move " + columnRow.column.title + " up"
                        onClicked: page.move(columnRow.index, columnRow.index - 1)
                    }

                    QQC2.Button {
                        icon.name: "arrow-down"
                        enabled: columnRow.index < page.order.length - 1
                        Accessible.name: "Move " + columnRow.column.title + " down"
                        onClicked: page.move(columnRow.index, columnRow.index + 1)
                    }

                    QQC2.Switch {
                        checked: page.columnVisible(columnRow.modelData)
                        Accessible.name: columnRow.column.title
                        onClicked: page.setColumnVisible(columnRow.modelData, checked)
                    }
                }
            }
        }
    }
}
