// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Controls as QQC2
import QtQuick.Layouts
import org.kde.kirigami as Kirigami
import "."

/// General settings page. Entries and wording mirror the Windows General page; the Windows-only
/// cards (sign-in autostart, Mica blur, taskbar position) have no Plasma equivalent and are
/// deliberately not shown.
ConfigSettingsBase {
    id: page

    function syncControls() {
        nextSessionSwitch.checked = draft.showNextSessionWhenIdle !== false;
        animationsSwitch.checked = draft.interfaceAnimations !== false;
        singleRowSwitch.checked = draft.useSingleRowTaskbar === true;
        rotationSlider.value = draft.rotationSeconds || 5;
        retentionSlider.value = draft.completedSessionRetentionMinutes || 5;
    }

    ColumnLayout {
        Layout.fillWidth: true
        Layout.maximumWidth: Kirigami.Units.gridUnit * 34
        spacing: 0

        QQC2.Label {
            Layout.topMargin: Kirigami.Units.largeSpacing
            Layout.bottomMargin: Kirigami.Units.largeSpacing
            text: "General"
            font.weight: Font.Bold
            font.pointSize: Kirigami.Theme.defaultFont.pointSize + 2
        }

        SettingsCard {
            title: "Show the next session when nothing is live"
            description: "Between sessions the widget counts down to the next published session instead of leaving the taskbar"
            QQC2.Switch {
                id: nextSessionSwitch
                onClicked: {
                    page.draft.showNextSessionWhenIdle = checked;
                    page.queueApply();
                }
            }
        }

        SettingsCard {
            title: "Enable Animations"
            QQC2.Switch {
                id: animationsSwitch
                onClicked: {
                    page.draft.interfaceAnimations = checked;
                    page.queueApply();
                }
            }
        }

        SettingsCard {
            title: "Use a one-row taskbar layout"
            QQC2.Switch {
                id: singleRowSwitch
                onClicked: {
                    page.draft.useSingleRowTaskbar = checked;
                    page.queueApply();
                }
            }
        }

        SettingsCard {
            title: "Live Championship Display Rotation"
            description: "When several enabled championships are live, rotate between them at this interval"
            RowLayout {
                QQC2.Slider {
                    id: rotationSlider
                    Layout.preferredWidth: Kirigami.Units.gridUnit * 14
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
        }

        SettingsCard {
            title: "Stale Data Timeout"
            description: "Keep the latest classification on the taskbar between sessions"
            RowLayout {
                QQC2.Slider {
                    id: retentionSlider
                    Layout.preferredWidth: Kirigami.Units.gridUnit * 14
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
        }

        SettingsCard {
            title: "Reset to defaults"
            QQC2.Button {
                text: "Reset"
                icon.name: "edit-reset"
                onClicked: page.restoreDefaults()
            }
        }
    }
}
