#!/usr/bin/env bash
set -euo pipefail

# Taskbar MQTT Client - Linux uninstaller
# Usage: ./uninstall.sh [PREFIX]   (must match the prefix used by install.sh)

PREFIX="${1:-$HOME/.local}"

AUTOSTART_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/autostart"
rm -f "$AUTOSTART_DIR/taskbar-mqtt.desktop"

rm -f "$PREFIX/bin/taskbar-mqtt"
rm -f "$PREFIX/share/applications/taskbar-mqtt.desktop"
rm -f "$PREFIX/share/icons/hicolor/64x64/apps/taskbar-mqtt.png"
rm -f "$PREFIX/share/icons/hicolor/128x128/apps/taskbar-mqtt.png"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$PREFIX/share/applications" 2>/dev/null || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "$PREFIX/share/icons/hicolor" 2>/dev/null || true
fi

echo "Removed taskbar-mqtt from $PREFIX."
echo "Note: user config is kept at \${XDG_CONFIG_HOME:-\$HOME/.config}/TaskbarMqtt/config.json."
