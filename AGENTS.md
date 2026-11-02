# AGENTS.md

## Build (Windows)
```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build src\TaskbarMqtt\TaskbarMqtt.csproj -c Release
```

## Build (Linux)
```bash
dotnet publish src/TaskbarMqtt.Linux/TaskbarMqtt.Linux.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/linux-x64
./install.sh [PREFIX]    # default ~/.local; ./uninstall.sh [PREFIX] to remove
```
- Project: `src/TaskbarMqtt.Linux/` (.NET 9, Avalonia 11); shares `Config/` and `Mqtt/` sources with the Windows project via `<Compile Include>` links
- Config on Linux: `~/.config/TaskbarMqtt/config.json` (a `config.json` next to the binary wins, portable layout)
- Tray: StatusNotifierItem via Avalonia; test registration with `gdbus call --session --dest org.kde.StatusNotifierWatcher --object-path /StatusNotifierWatcher --method org.freedesktop.DBus.Properties.Get org.kde.StatusNotifierWatcher RegisteredStatusNotifierItems`

## Icons
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File generate-icons.ps1
```
- Script: `generate-icons.ps1` in repo root
- Output: `src/TaskbarMqtt/Assets/app.ico`, `src/TaskbarMqtt/Assets/button-default.ico`
- Must be run before build to embed updated icons
- `src/TaskbarMqtt.Linux/Assets/taskbar-mqtt.png` is generated from `app.ico` with ImageMagick (`magick app.ico -thumbnail 64x64 ...`)

## Installer
```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer.iss
```
- Script: `installer.iss` in repo root
- Output: `dist\TaskbarMqtt_Setup_v1.1.exe`
- Upload to GitHub Releases via `gh release upload <tag> <file>`

## GitHub Releases
```powershell
gh release create v<ver> src\TaskbarMqtt\bin\Release\TaskbarMqtt.exe src\TaskbarMqtt\bin\Release\TaskbarMqtt.exe.config dist\TaskbarMqtt_Setup_v<ver>.exe --title "v<ver>" --notes "<notes>"
```
- Pushing a tag `v*` also triggers `.github/workflows/build.yml`, which attaches `taskbar-mqtt-linux-x64` (self-contained) and the Windows `TaskbarMqtt.exe` to the release

## Project
- Windows: .NET Framework 4.8 WinForms tray app, `src/TaskbarMqtt/`
- Linux: .NET 9 Avalonia tray app, `src/TaskbarMqtt.Linux/` (single-project solution file `TaskbarMqtt.sln`)
- Key files:
  - `App/TrayContext.cs` / `App/LinuxTrayContext.cs` — tray icons, popup, entry point
  - `UI/SettingsForm.cs` / `UI/SettingsWindow.cs` — settings dialog
  - `UI/PopupForm.cs` / `UI/PopupWindow.cs` — popup panel
  - `Mqtt/MqttService.cs` — MQTT client (shared)
  - `Config/AppConfig.cs` — config model (shared)
- Config: `config.json` alongside the exe (Windows), `~/.config/TaskbarMqtt/config.json` (Linux)
- Packages: `MQTTnet`, `Newtonsoft.Json`; Linux additionally `Avalonia`, `SkiaSharp`

## Conventions
- SDK-style `.csproj` targeting `net48` (Windows) / `net9.0` (Linux)
- No code comments
- No emojis in code or docs
- No README/doc files unless explicitly asked
- Keep `README.md` up to date with any changes
- Keep `AGENTS.md` up to date when crucial things change
