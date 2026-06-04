; Taskbar MQTT FastSwitch installer
#define MyAppName "Taskbar MQTT FastSwitch"
#define MyAppVersion "1.8.1"
#define MyAppPublisher "yustAnotherUser"
#define MyAppURL "https://github.com/yustAnotherUser/taskbar-mqtt-fastswitch"
#define MyAppExeName "TaskbarMqtt.exe"

[Setup]
AppId={{B3A8F2E1-5C4D-4A7F-9B6E-2D1C8F3A5E7B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=dist
OutputBaseFilename=TaskbarMqtt_Setup_v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "src\TaskbarMqtt\bin\Release\TaskbarMqtt.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "src\TaskbarMqtt\bin\Release\TaskbarMqtt.exe.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
