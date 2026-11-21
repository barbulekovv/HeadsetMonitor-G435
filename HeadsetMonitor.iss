
#define MyAppName "Headset Monitor"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Headset Monitor"
#define MyAppExeName "HeadsetMonitor.exe"

[Setup]
AppId={{A8E3F67C-9D21-4F35-B8A2-6C71D4E90215}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Headset Monitor
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\HeadsetMonitor.exe
SetupIconFile=HeadsetMonitor.ico
OutputDir=Installer
OutputBaseFilename=HeadsetMonitor_Setup
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные значки:"; Flags: unchecked

[Files]
Source: "bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Headset Monitor"; Filename: "{app}\HeadsetMonitor.exe"; IconFilename: "{app}\HeadsetMonitor.ico"
Name: "{autodesktop}\Headset Monitor"; Filename: "{app}\HeadsetMonitor.exe"; IconFilename: "{app}\HeadsetMonitor.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\HeadsetMonitor.exe"; Description: "Запустить Headset Monitor"; Flags: postinstall nowait skipifsilent

