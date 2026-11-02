#!/usr/bin/env bash
set -euo pipefail

# Taskbar MQTT Client - Linux installer
# Usage: ./install.sh [PREFIX]
#   PREFIX/bin/taskbar-mqtt        executable
#   PREFIX/share/applications      desktop entry
#   PREFIX/share/icons/hicolor     app icon
# PREFIX defaults to $HOME/.local (no root required); use /usr for a system-wide install.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BIN_SRC="$REPO_ROOT/publish/linux-x64/taskbar-mqtt"

if [ ! -x "$BIN_SRC" ]; then
    echo "Error: $BIN_SRC not found." >&2
    echo "Build it first:" >&2
    echo "  dotnet publish src/TaskbarMqtt.Linux/TaskbarMqtt.Linux.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64" >&2
    exit 1
fi

PREFIX="${1:-$HOME/.local}"
BIN_DIR="$PREFIX/bin"
DATA_DIR="$PREFIX/share"
APP_DIR="$DATA_DIR/applications"
ICON_DIR_64="$DATA_DIR/icons/hicolor/64x64/apps"
ICON_DIR_128="$DATA_DIR/icons/hicolor/128x128/apps"

mkdir -p "$BIN_DIR" "$APP_DIR" "$ICON_DIR_64" "$ICON_DIR_128"

install -m 755 "$BIN_SRC" "$BIN_DIR/taskbar-mqtt"
install -m 644 "$REPO_ROOT/src/TaskbarMqtt.Linux/Assets/taskbar-mqtt.png" "$ICON_DIR_64/taskbar-mqtt.png"
install -m 644 "$REPO_ROOT/src/TaskbarMqtt.Linux/Assets/taskbar-mqtt-128.png" "$ICON_DIR_128/taskbar-mqtt.png"

cat > "$APP_DIR/taskbar-mqtt.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Taskbar MQTT Client
GenericName=MQTT publisher
Comment=Publish pre-configured MQTT messages from the notification area
Exec=$BIN_DIR/taskbar-mqtt
Icon=taskbar-mqtt
Terminal=false
Categories=Network;Utility;
Keywords=mqtt;tray;iot;home automation;
StartupNotify=false
X-KDE-StartupNotify=false
EOF

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$APP_DIR" 2>/dev/null || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "$DATA_DIR/icons/hicolor" 2>/dev/null || true
fi

echo "Installed:"
echo "  $BIN_DIR/taskbar-mqtt"
echo "  $APP_DIR/taskbar-mqtt.desktop"
echo "  $DATA_DIR/icons/hicolor/.../apps/taskbar-mqtt.png"
echo
echo "Run 'taskbar-mqtt' to start it. Enable autostart in the app settings."
