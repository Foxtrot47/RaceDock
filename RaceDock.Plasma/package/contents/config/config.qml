// SPDX-License-Identifier: GPL-3.0-or-later
import QtQuick
import org.kde.plasma.configuration

/// Configuration categories. The pages live in contents/ui next to the applet, and every change is
/// pushed to the RaceDock service rather than stored in the applet's own configuration group.
ConfigModel {
    ConfigCategory {
        name: "General"
        icon: "preferences-system"
        source: "ConfigGeneral.qml"
    }
    ConfigCategory {
        name: "Columns"
        icon: "view-sort-ascending"
        source: "ConfigColumns.qml"
    }
    ConfigCategory {
        name: "Developer"
        icon: "applications-engineering"
        source: "ConfigDeveloper.qml"
    }
}
