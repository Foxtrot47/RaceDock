#!/usr/bin/env bash
# Removes everything scripts/install-linux.sh installed for the current user.
#
#   scripts/uninstall-linux.sh            # remove the app, applet, wrapper, and activation file
#   scripts/uninstall-linux.sh --purge    # also delete settings and logs under ~/.local/share/RaceDock
#
# The D-Bus activation file and the sidecar are removed first, so Plasma stops respawning the service.
set -euo pipefail

data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
bin_home="${XDG_BIN_HOME:-$HOME/.local/bin}"
config_home="${XDG_CONFIG_HOME:-$HOME/.config}"
install_root="$data_home/racedock"
applet_id="org.foxtrot47.racedock"
purge=0

while [ $# -gt 0 ]; do
    case "$1" in
        --purge) purge=1 ;;
        --help|-h)
            sed -n '2,8p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        *)
            echo "Unknown option '$1'." >&2
            exit 2
            ;;
    esac
    shift
done

if command -v systemctl >/dev/null 2>&1 && systemctl --user list-unit-files racedock.service >/dev/null 2>&1; then
    systemctl --user disable --now racedock.service >/dev/null 2>&1 || true
fi
rm -f "$config_home/systemd/user/racedock.service"
if command -v systemctl >/dev/null 2>&1; then
    systemctl --user daemon-reload >/dev/null 2>&1 || true
fi

rm -f "$data_home/dbus-1/services/org.foxtrot47.RaceDock.service"
rm -f "$bin_home/racedock-service"
rm -rf "$install_root"

if command -v kpackagetool6 >/dev/null 2>&1; then
    if kpackagetool6 --type Plasma/Applet --list 2>/dev/null | grep -qx "$applet_id"; then
        kpackagetool6 --type Plasma/Applet --remove "$applet_id" || true
    fi
fi

if [ "$purge" = 1 ]; then
    rm -rf "$data_home/RaceDock"
    echo "Removed the RaceDock app, applet, settings, and logs."
else
    echo "Removed the RaceDock app and applet. Settings and logs are kept in $data_home/RaceDock."
    echo "Pass --purge to delete them as well."
fi
