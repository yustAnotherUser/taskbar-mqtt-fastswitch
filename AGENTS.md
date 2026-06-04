# AGENTS.md

## Build
```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build src\TaskbarMqtt\TaskbarMqtt.csproj -c Release
```

## Icons
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File generate-icons.ps1
```
- Script: `generate-icons.ps1` in repo root
- Output: `src/TaskbarMqtt/Assets/app.ico`, `src/TaskbarMqtt/Assets/button-default.ico`
- Must be run before build to embed updated icons

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

## Project
- .NET Framework 4.8 WinForms tray app
- `src/TaskbarMqtt/` — single-project solution
- Key files:
  - `App/TrayContext.cs` — tray icons, popup, entry point
  - `UI/SettingsForm.cs` — settings dialog
  - `UI/PopupForm.cs` — popup panel
  - `Mqtt/MqttService.cs` — MQTT client
  - `Config/AppConfig.cs` — config model
- Config: `config.json` alongside the exe
- Packages: `MQTTnet`, `Newtonsoft.Json`

## Conventions
- SDK-style `.csproj` targeting `net48`
- No code comments
- No emojis in code or docs
- No README/doc files unless explicitly asked
- Keep `README.md` up to date with any changes
- Keep `AGENTS.md` up to date when crucial things change
