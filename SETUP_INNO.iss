#define MyAppName "Windows Master"
#define MyAppVersion "5.2"
#define MyAppPublisher "Anas Mohamed | Tech"
#define MyAppURL "https://a50826213-hub.github.io/anas-mohamed-tech/"
#define MyAppExeName "Windows_Master_v5.2.exe"

[Setup]
AppId={{B6A9E1A5-8D5C-4B15-A6BA-9F5A5D0C5E52}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

DefaultDirName={autopf}\Windows Master
DefaultGroupName=Windows Master

OutputDir=installer_output
OutputBaseFilename=Windows_Master_v5.2_Setup

Compression=lzma
SolidCompression=yes
WizardStyle=modern

PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

SetupIconFile=logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Windows Master 5.2 - Native Windows control center.
VersionInfoProductName={#MyAppName}
VersionInfoVersion={#MyAppVersion}.0

[Files]
Source: "dist\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "logo.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Windows Master"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\logo.ico"
Name: "{commondesktop}\Windows Master"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\logo.ico"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#MyAppExeName}"
Description: "Launch Windows Master"
Flags: nowait postinstall skipifsilent