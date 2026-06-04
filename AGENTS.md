# AGENTS.md

## Build
```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build src\TaskbarMqtt\TaskbarMqtt.csproj -c Release
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
