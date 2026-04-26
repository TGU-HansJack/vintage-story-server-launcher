#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-local"
#endif

#ifndef BuildDir
  #define BuildDir "..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{3C9FD4C0-D0B4-4D18-B1A6-9D3A2F9E2B5F}
AppName=VSSL
AppVersion={#MyAppVersion}
AppPublisher=VSSL
DefaultDirName={autopf}\VSSL
DefaultGroupName=VSSL
OutputDir={#OutputDir}
OutputBaseFilename=VSSL-Setup-{#MyAppVersion}-win-x64
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\VSSL.App.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
#ifexist "C:\Program Files (x86)\Inno Setup 6\Languages\ChineseSimplified.isl"
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\VSSL"; Filename: "{app}\VSSL.App.exe"
Name: "{autodesktop}\VSSL"; Filename: "{app}\VSSL.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VSSL.App.exe"; Description: "{cm:LaunchProgram,VSSL}"; Flags: nowait postinstall skipifsilent
