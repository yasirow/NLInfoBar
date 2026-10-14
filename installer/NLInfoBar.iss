; NLInfoBar installer (Inno Setup 6).
; Per-user, no administrator rights; installs, upgrades and uninstalls only its own files (docs/DESIGN.md §10).
;
; Built by tools\package.ps1:
;   ISCC.exe /DAppVersion=x.y.z /DSourceDir=<build output> /DOutputDir=<artifacts> installer\NLInfoBar.iss

#ifndef AppVersion
  #error AppVersion is not defined. Build with tools\package.ps1.
#endif
#ifndef SourceDir
  #define SourceDir "..\src\NLInfoBar\bin\Release\net48"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define AppName "NLInfoBar"
#define AppExe "NLInfoBar.exe"
#define RepoUrl "https://github.com/yasirow/NLInfoBar"

[Setup]
; Never change AppId: upgrades and uninstall rely on it.
AppId={{BDE1165A-30D1-49FD-B9F2-4ACBB0B03CD3}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=NLInfoBar contributors
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
AppCopyright=Copyright (c) 2026 NLInfoBar contributors
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} setup

; Per-user install: %LocalAppData%\Programs\NLInfoBar, Start menu of the current user, uninstall entry under HKCU.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 1809 or later.
MinVersion=10.0.17763

; Close a running NLInfoBar before installing or uninstalling (same name as Program.MutexName).
AppMutex=Local\NLInfoBar.SingleInstance
CloseApplications=yes
RestartApplications=no

LicenseFile=..\LICENSE
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir={#OutputDir}
OutputBaseFilename=NLInfoBar-Setup-{#AppVersion}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
SetupLogging=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[CustomMessages]
english.StartupTask=Start NLInfoBar when I sign in to Windows
english.OptionsGroup=Options:
turkish.StartupTask=Windows'ta oturum açtığımda NLInfoBar'ı başlat
turkish.OptionsGroup=Seçenekler:

[Tasks]
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:OptionsGroup}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\NLInfoBar.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\NLInfoBar.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; DestName: "THIRD-PARTY-NOTICES.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only the files NLInfoBar itself writes, by name (docs/DESIGN.md §8), then the folders if they are empty.
Type: files; Name: "{app}\settings.json"
Type: files; Name: "{app}\nlinfobar.log"
Type: files; Name: "{app}\nlinfobar.log.1"
Type: files; Name: "{localappdata}\NLInfoBar\settings.json"
Type: files; Name: "{localappdata}\NLInfoBar\nlinfobar.log"
Type: files; Name: "{localappdata}\NLInfoBar\nlinfobar.log.1"
Type: dirifempty; Name: "{localappdata}\NLInfoBar"
Type: dirifempty; Name: "{app}"
