; Inno Setup script for Temperature Intensity Monitor.
; Built in CI (see .github/workflows/release.yml); expects the app already
; published to ..\publish. Version is passed with /DMyAppVersion=x.y.z.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppName "Temperature Intensity Monitor"
#define MyAppExeName "TempMonitor.exe"

[Setup]
AppId={{9B2A6F4C-1E63-4A0B-9C7D-5A31D0F2B8E1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Temperature Intensity Monitor contributors
DefaultDirName={autopf}\TempMonitor
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
OutputBaseFilename=TempMonitor-Setup-{#MyAppVersion}
OutputDir=Output
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Tasks]
Name: "autostart"; Description: "Start with Windows (scheduled task, runs elevated without a UAC prompt)"
Name: "pawnio"; Description: "Install PawnIO driver (required for CPU temperature/power readings)"; Check: not PawnIOInstalled

[Run]
; Autostart via Task Scheduler so the elevated app can start at logon UAC-free.
; ExecutionTimeLimit 0 stops Windows from killing the overlay after 72 h.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Register-ScheduledTask -TaskName 'TempMonitor' -Action (New-ScheduledTaskAction -Execute '{app}\{#MyAppExeName}') -Trigger (New-ScheduledTaskTrigger -AtLogOn) -RunLevel Highest -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit 0 -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries) -Force"""; \
  Tasks: autostart; Flags: runhidden; StatusMsg: "Registering autostart task..."
Filename: "winget"; \
  Parameters: "install namazso.PawnIO --accept-source-agreements --accept-package-agreements --silent"; \
  Tasks: pawnio; Flags: runhidden shellexec waituntilterminated; StatusMsg: "Installing PawnIO driver..."
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: postinstall nowait

[UninstallRun]
Filename: "schtasks"; Parameters: "/Delete /TN TempMonitor /F"; Flags: runhidden; RunOnceId: "RemoveAutostartTask"

[Code]
function PawnIOInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO');
end;
