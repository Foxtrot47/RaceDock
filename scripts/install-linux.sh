#!/usr/bin/env bash
# Installs RaceDock for the current user: the .NET sidecar, its D-Bus activation file, and the Plasma
# applet. Nothing is written outside the user prefix unless --systemd is passed.
#
#   scripts/install-linux.sh                 # publish, install, activate on the session bus
#   scripts/install-linux.sh --systemd       # also install and start the systemd user unit
#   scripts/install-linux.sh --no-applet     # sidecar only
#   scripts/install-linux.sh --skip-build    # reuse an existing publish folder
#
# Idiomatic .NET installs live under ~/.dotnet; install one with the official installer if needed:
#   https://learn.microsoft.com/dotnet/core/install/linux
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
bin_home="${XDG_BIN_HOME:-$HOME/.local/bin}"
config_home="${XDG_CONFIG_HOME:-$HOME/.config}"
install_root="$data_home/racedock"
applet_id="org.foxtrot47.racedock"
service_name="org.foxtrot47.RaceDock"
use_systemd=0
install_applet=1

while [ $# -gt 0 ]; do
    case "$1" in
        --systemd) use_systemd=1 ;;
        --no-applet) install_applet=0 ;;
        --help|-h)
            sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        *)
            echo "Unknown option '$1'." >&2
            exit 2
            ;;
    esac
    shift
done

if ! command -v dotnet >/dev/null 2>&1; then
    if [ -x "${DOTNET_ROOT:-$HOME/.dotnet}/dotnet" ]; then
        export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
        export PATH="$DOTNET_ROOT:$PATH"
    else
        echo "The .NET SDK was not found. Install .NET 10 and set DOTNET_ROOT if it is not on PATH." >&2
        exit 1
    fi
fi

echo "Publishing the RaceDock sidecar…"
publish_dir="$(mktemp -d)"
trap 'rm -rf "$publish_dir"' EXIT
dotnet publish "$repo_root/RaceDock.Linux/RaceDock.Linux.csproj" \
    -c Release --nologo --no-self-contained -o "$publish_dir"

echo "Installing to $install_root…"
mkdir -p "$install_root" "$bin_home"
# Replace only the published files so a stale DLL from an older build cannot linger.
find "$install_root" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
cp -a "$publish_dir/." "$install_root/"
install -m 755 "$repo_root/scripts/linux/racedock-service" "$bin_home/racedock-service"

mkdir -p "$data_home/dbus-1/services"
activation_file="$data_home/dbus-1/services/$service_name.service"
{
    echo "[D-BUS Service]"
    echo "Name=$service_name"
    echo "Exec=$bin_home/racedock-service"
    if [ "$use_systemd" = 1 ]; then
        echo "SystemdService=racedock.service"
    fi
} > "$activation_file"
echo "Wrote $activation_file"

if [ "$use_systemd" = 1 ]; then
    mkdir -p "$config_home/systemd/user"
    unit_file="$config_home/systemd/user/racedock.service"
    cat > "$unit_file" <<EOF
[Unit]
Description=RaceDock motorsport timing sidecar
Documentation=https://github.com/foxtrot47/RaceDock
PartOf=graphical-session.target
After=graphical-session.target

[Service]
Type=simple
ExecStart=$bin_home/racedock-service
Restart=on-failure
RestartSec=5

[Install]
WantedBy=graphical-session.target
EOF
    systemctl --user daemon-reload
    systemctl --user enable --now racedock.service
    echo "Enabled the systemd user unit at $unit_file"
fi

if [ "$install_applet" = 1 ]; then
    if ! command -v kpackagetool6 >/dev/null 2>&1; then
        echo "kpackagetool6 was not found; install plasma-framework or skip with --no-applet." >&2
        exit 1
    fi
    package_dir="$repo_root/RaceDock.Plasma/package"
    if kpackagetool6 --type Plasma/Applet --list 2>/dev/null | grep -qx "$applet_id"; then
        kpackagetool6 --type Plasma/Applet --upgrade "$package_dir"
    else
        kpackagetool6 --type Plasma/Applet --install "$package_dir"
    fi
    echo "Installed the Plasma applet. Add RaceDock from *Add Widgets* if it is not on the panel yet."
fi

echo
echo "Done. The applet starts the sidecar through D-Bus on first use."
echo "Logs: $data_home/RaceDock/Logs/app.ndjson (JSON lines, one object per event)."
echo "If the applet does not appear, restart Plasma: systemctl --user restart plasma-plasmashell"
