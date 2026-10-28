// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Controls as QQC2
import QtQuick.Layouts
import org.kde.kirigami as Kirigami

/// One settings row, mirroring the Windows settings cards: a bold title with an optional secondary
/// description on the left and the control on the right. No strings of its own.
ColumnLayout {
    id: card

    property string title: ""
    property string description: ""
    property bool enabledCard: true
    default property alias control: controlSlot.data

    spacing: 0

    RowLayout {
        Layout.fillWidth: true
        Layout.topMargin: Kirigami.Units.largeSpacing
        Layout.bottomMargin: Kirigami.Units.largeSpacing
        spacing: Kirigami.Units.largeSpacing
        opacity: card.enabledCard ? 1 : 0.5

        ColumnLayout {
            Layout.fillWidth: true
            spacing: 0

            QQC2.Label {
                Layout.fillWidth: true
                text: card.title
                font.weight: Font.DemiBold
                elide: Text.ElideRight
            }

            QQC2.Label {
                Layout.fillWidth: true
                visible: card.description.length > 0
                text: card.description
                color: Kirigami.Theme.disabledTextColor
                font.pointSize: Kirigami.Theme.smallFont.pointSize
                wrapMode: Text.WordWrap
            }
        }

        Item {
            id: controlSlot
            Layout.alignment: Qt.AlignRight | Qt.AlignVCenter
            implicitWidth: childrenRect.width
            implicitHeight: childrenRect.height
        }
    }
}
