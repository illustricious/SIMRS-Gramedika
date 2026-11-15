; Installer Windows SIMRS Gramedika (Inno Setup 6).
; Dipanggil oleh installer\build-installers.ps1, yang mengirim:
;   /DAppVersion=1.0.0  /DSourceDir=<folder publish win-x64>  /DOutputDir=<folder dist>

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif

#define AppName "SIMRS Gramedika"
#define AppExe "SIMRS Gramedika.exe"

[Setup]
AppId={{7B8E3C2A-5D41-4E9B-9C3F-6A2D8E1F4B70}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=RSU Gramedika
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=SIMRS-Gramedika-Setup-{#AppVersion}-win-x64
SetupIconFile=..\..\src\SimrsKhanza.UI\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Admin install into Program Files by default; the user may choose "only for me" instead.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; Koneksi database disimpan per user di %APPDATA%\SimrsKhanza dan sengaja TIDAK dihapus
; saat uninstall, supaya upgrade/instal ulang tidak perlu mengatur koneksi lagi.
