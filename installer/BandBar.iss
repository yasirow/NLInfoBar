; BandBar installer (Inno Setup 6).
; Per-user, no administrator rights; installs, upgrades and uninstalls only its own files (docs/DESIGN.md §10).
;
; Built by tools\package.ps1:
;   ISCC.exe /DAppVersion=x.y.z /DSourceDir=<build output> /DOutputDir=<artifacts> installer\BandBar.iss

#ifndef AppVersion
  #error AppVersion is not defined. Build with tools\package.ps1.
#endif
#ifndef SourceDir
  #define SourceDir "..\src\BandBar\bin\Release\net48"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define AppName "BandBar"
#define AppExe "BandBar.exe"
#define RepoUrl "https://github.com/yasirow/BandBar"

[Setup]
; Never change AppId: upgrades and uninstall rely on it. (New ID since 0.1.4, when the project was renamed;
; installs of 0.1.3 are a separate entry that the user uninstalls first, see README "Update".)
AppId={{61A572C7-9929-47AB-BE5F-75E7AF946196}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=BandBar contributors
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
AppCopyright=Copyright (c) 2026 BandBar contributors
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} setup

; Per-user install: %LocalAppData%\Programs\BandBar, Start menu of the current user, uninstall entry under HKCU.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 1809 or later.
MinVersion=10.0.17763

; Close a running BandBar before installing or uninstalling (same name as Program.MutexName).
AppMutex=Local\BandBar.SingleInstance
CloseApplications=yes
RestartApplications=no

LicenseFile=..\LICENSE
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir={#OutputDir}
OutputBaseFilename=BandBar-Setup-{#AppVersion}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
SetupLogging=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[CustomMessages]
english.StartupTask=Start BandBar when I sign in to Windows
english.OptionsGroup=Options:
turkish.StartupTask=Windows'ta oturum açtığımda BandBar'ı başlat
turkish.OptionsGroup=Seçenekler:

[Tasks]
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:OptionsGroup}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\BandBar.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\BandBar.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; DestName: "THIRD-PARTY-NOTICES.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only the files BandBar itself writes, by name (docs/DESIGN.md §8), then the folders if they are empty.
Type: files; Name: "{app}\settings.json"
Type: files; Name: "{app}\bandbar.log"
Type: files; Name: "{app}\bandbar.log.1"
Type: files; Name: "{localappdata}\BandBar\settings.json"
Type: files; Name: "{localappdata}\BandBar\bandbar.log"
Type: files; Name: "{localappdata}\BandBar\bandbar.log.1"
Type: dirifempty; Name: "{localappdata}\BandBar"
Type: dirifempty; Name: "{app}"
