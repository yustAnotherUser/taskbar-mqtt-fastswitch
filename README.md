# Taskbar MQTT FastSwitch

A tiny portable Windows tray app that lives in the notification area and publishes pre-configured MQTT messages at the click of a button. Configurable 1–9 buttons, each with its own topic, payload, QoS, retain flag and custom icon.

## Quick start

1. Copy `dist\TaskbarMqtt.exe` (and optionally `TaskbarMqtt.exe.config`) anywhere on your machine.
2. Run it. The app starts silently in the notification area (bottom-right of the taskbar).
3. Right-click the tray icon → **Settings…**.
4. Fill in the **Broker** tab (host, port, credentials, TLS) and click **Test connection** to verify.
5. In the **Buttons** tab, define up to 9 buttons — each with a label, MQTT topic, payload, QoS, retain, and an optional custom icon (PNG / JPG / ICO).
6. **OK** to save. `config.json` is written next to the executable (portable, no registry footprint except optional autostart).

## How to use

- **Popup-panel mode** (default): one tray icon. **Left-click** it to open a small floating panel with your buttons. Click a button to publish. The panel closes when it loses focus or the mouse leaves it.
- **Multi-icon mode**: one tray icon per button. **Left-click** an icon to publish directly.

Right-click any tray icon for:

- **Settings…** — open the configuration dialog
- **Reconnect now** — force an MQTT reconnect
- **Quit** — exit the app

## Settings

### General
- **Display mode** — popup panel from one tray icon, or one tray icon per button
- **Start with Windows** — toggle autostart via `HKCU\…\Run`

### Broker
- **Host / Port / Username / Password / Client ID / Keep-alive**
- **Use TLS** — enables encrypted connection
- **Allow invalid / self-signed TLS certificates** — checked by default; uncheck to enforce certificate validation

Click **Test connection** in the Broker tab to verify settings before saving.

### Buttons (per row)
- **Label** — shown in tooltip / tray tooltip
- **Topic** — the MQTT topic to publish to (required)
- **Payload** — the message body (string)
- **QoS** — 0, 1, or 2
- **Retain** — whether the broker should retain the message
- **Icon** — optional path to `.ico`, `.png`, `.jpg`, `.bmp`. PNGs/JPGs are auto-resized to fit the tray and the popup button.

Buttons are added and removed dynamically in the Buttons tab using the **+ Add Button** and **✕** buttons (minimum 1, maximum 9).

## Config file format

`config.json` is created on first launch and re-written on every **Apply** / **OK** in Settings. It lives next to `TaskbarMqtt.exe` (portable mode). When installed in `Program Files`, the app falls back to `%LOCALAPPDATA%\TaskbarMqtt\config.json` because the install directory is read-only.

```json
{
  "DisplayMode": "PopupPanel",
  "ButtonCount": 4,
  "StartWithWindows": false,
  "Broker": {
    "Host": "192.168.1.10",
    "Port": 1883,
    "UseTls": false,
    "AllowInvalidCerts": true,
    "Username": "",
    "Password": "",
    "ClientId": "",
    "KeepAliveSeconds": 30
  },
  "Buttons": [
    { "Label": "Lights On",  "Topic": "home/lights/main", "Payload": "ON",  "Qos": 0, "Retain": false, "IconPath": "" },
    { "Label": "Lights Off", "Topic": "home/lights/main", "Payload": "OFF", "Qos": 0, "Retain": false, "IconPath": "" }
  ]
}
```

`DisplayMode` is either `"PopupPanel"` or `"MultipleIcons"`. The file is hand-editable; changes take effect on next launch.

## Build from source

Requirements: .NET SDK 9 (or 8) with NuGet. Restore assemblies for .NET Framework 4.8 are pulled in automatically via the `Microsoft.NETFramework.ReferenceAssemblies.net48` NuGet package.

```bash
dotnet build src/TaskbarMqtt/TaskbarMqtt.csproj -c Release
# Output: src/TaskbarMqtt/bin/Release/TaskbarMqtt.exe
```

The build embeds `MQTTnet` and `Newtonsoft.Json` into the .exe via Costura.Fody, so the final `TaskbarMqtt.exe` (~470 KB) runs on any Windows 10/11 machine without additional runtime installation. `.NET Framework 4.8` must be present (it ships with Windows 10/11 and is updated automatically).

## Project structure

```
src/TaskbarMqtt/
  Program.cs            # Entry, single-instance mutex, message-only window for 2nd-instance
  App/
    TrayContext.cs      # ApplicationContext: owns NotifyIcons, popup, MQTT, context menu
    AutoStart.cs        # HKCU\…\Run helper
  Config/
    AppConfig.cs        # POCOs: BrokerSettings, ButtonConfig, AppConfig
    ConfigStore.cs      # JSON load/save next to .exe
  Mqtt/
    MqttService.cs      # MQTTnet v4 wrapper: connect, publish, auto-reconnect
  UI/
    PopupForm.cs        # Borderless hover panel
    SettingsForm.cs     # Tabbed settings dialog (General / Broker / Buttons)
  Assets/
    app.ico             # Tray icon (multi-resolution 16/32/48/64)
    button-default.ico  # Default button icon
```

## Regenerating the icons

```powershell
powershell -ExecutionPolicy Bypass -File generate-icons.ps1
```

Produces multi-resolution `.ico` files (16/32/48/64) embedded into the assembly.

## License

MIT. Third-party: MQTTnet (MIT), Newtonsoft.Json (MIT), Costura.Fody (MIT).
