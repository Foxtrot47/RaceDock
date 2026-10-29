// SPDX-License-Identifier: GPL-3.0-or-later
pragma ComponentBehavior: Bound

import QtQuick

/// Use the platform text rasterizer for crisp small labels in the panel and flyout.
Text {
    renderType: Text.NativeRendering
    antialiasing: true
    font.hintingPreference: Font.PreferDefaultHinting
}
