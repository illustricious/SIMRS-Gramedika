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
; Numeric version for the Windows file info (AppVersion may carry "-alpha...").
#ifndef FileVersion
  #define FileVersion "0.1.0"
#endif
; x64 or arm64 (the publish folder must match).
#ifndef Arch
  #define Arch "x64"
#endif

#define AppName "SIMRS Gramedika"
#define AppExe "SIMRS Gramedika.exe"

[Setup]
AppId={{7B8E3C2A-5D41-4E9B-9C3F-6A2D8E1F4B70}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=RSU Gramedika
VersionInfoVersion={#FileVersion}
VersionInfoProductTextVersion={#AppVersion}
VersionInfoProductName={#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=SIMRS-Gramedika-Setup-{#AppVersion}-win-{#Arch}
SetupIconFile=..\..\src\SimrsKhanza.UI\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
#endif
MinVersion=10.0
; Admin install into Program Files by default; the user may choose "only for me" instead.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; Koneksi database disimpan per user di %APPDATA%\SimrsKhanza dan sengaja TIDAK dihapus
; saat uninstall, supaya upgrade/instal ulang tidak perlu mengatur koneksi lagi.
