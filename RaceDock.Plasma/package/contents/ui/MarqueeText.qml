// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick

/// A single-line label that scrolls horizontally when the text is wider than the item, like the
/// Windows flyout's event name. When it fits, the text simply stays put.
Item {
    id: marquee

    property string text: ""
    property color textColor: "#FFFFFF"
    property int pixelSize: 12
    property bool bold: false
    /// Extra room kept between the scrolling text and both edges.
    readonly property real overflow: Math.max(0, label.width - width + 8)

    // A text item has no height of its own, so give the marquee a sensible implicit height; a
    // layout that does not set one would otherwise treat it as zero-height.
    implicitHeight: Math.ceil(pixelSize * 1.4)

    clip: true

    // A new event name or a narrower strip restarts the scroll from the left edge; when the text
    // fits again, snap back so it is never left clipped mid-scroll.
    onTextChanged: {
        label.x = 0;
        if (scroll.running)
            scroll.restart();
    }
    onOverflowChanged: {
        if (overflow <= 0)
            label.x = 0;
    }

    Text {
        id: label
        y: (parent.height - height) / 2
        text: marquee.text
        color: marquee.textColor
        font.pixelSize: marquee.pixelSize
        font.bold: marquee.bold
        maximumLineCount: 1
    }

    SequentialAnimation {
        id: scroll
        running: marquee.overflow > 0 && marquee.visible
        loops: Animation.Infinite

        PauseAnimation { duration: 900 }
        NumberAnimation {
            target: label
            property: "x"
            to: -marquee.overflow
            duration: Math.max(1400, marquee.overflow * 30)
            easing.type: Easing.InOutSine
        }
        PauseAnimation { duration: 1200 }
        NumberAnimation {
            target: label
            property: "x"
            to: 0
            duration: Math.max(1400, marquee.overflow * 30)
            easing.type: Easing.InOutSine
        }
    }
}
