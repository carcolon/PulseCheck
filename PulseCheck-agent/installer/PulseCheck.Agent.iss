#define MyAppName "PulseCheck Agent"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef MyTrayPublishedDir
  #define MyTrayPublishedDir "..\\PulseCheck.Agent\\bin\\Release\\net8.0-windows\\win-x64\\publish"
#endif
#ifndef MyServicePublishedDir
  #define MyServicePublishedDir "..\\PulseCheck.Agent.Service\\bin\\Release\\net8.0-windows\\win-x64\\publish"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "..\\artifacts"
#endif
#ifndef MyOutputBaseFilename
  #define MyOutputBaseFilename "PulseCheck.Agent.Setup"
#endif

[Setup]
AppId={{A5D68F23-C843-4B60-A9B3-9BCAAD0C1CC8}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Solvo Global
AppPublisherURL=https://solvoglobal.com/
AppSupportURL=https://solvoglobal.com/
DefaultDirName={commonpf}\PulseCheck\Agent
DefaultGroupName=PulseCheck
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyOutputBaseFilename}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\PulseCheck.Agent\Assets\PulseCheck.Agent.ico
UninstallDisplayIcon={app}\PulseCheck.Agent.exe
ChangesAssociations=no
CloseApplications=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "startup"; Description: "Iniciar PulseCheck al abrir sesion"; GroupDescription: "Opciones de inicio:"; Flags: checkedonce
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Dirs]
Name: "{commonappdata}\PulseCheck\Agent"; Permissions: users-modify

[Files]
Source: "{#MyTrayPublishedDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyServicePublishedDir}\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PulseCheck Agent"; Filename: "{app}\PulseCheck.Agent.exe"; Parameters: "--tray"; IconFilename: "{app}\PulseCheck.Agent.exe"
Name: "{autodesktop}\PulseCheck Agent"; Filename: "{app}\PulseCheck.Agent.exe"; Parameters: "--tray"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PulseCheck Agent Tray"; ValueData: """{app}\PulseCheck.Agent.exe"" --tray"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{cmd}"; Parameters: "/c sc stop PulseCheckAgentService >nul 2>nul & sc delete PulseCheckAgentService >nul 2>nul"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "create PulseCheckAgentService binPath= ""{app}\service\PulseCheck.Agent.Service.exe"" start= auto DisplayName= ""PulseCheck Agent Service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description PulseCheckAgentService ""Runs PulseCheck background synchronization and campaign delivery for the interactive tray app."""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start PulseCheckAgentService"; Flags: runhidden waituntilterminated
Filename: "{app}\PulseCheck.Agent.exe"; Parameters: "--tray"; Description: "Abrir PulseCheck Agent"; Flags: nowait postinstall

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c sc stop PulseCheckAgentService >nul 2>nul & sc delete PulseCheckAgentService >nul 2>nul"; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\PulseCheck\Agent"
Type: filesandordirs; Name: "{commonappdata}\PulseCheck\Agent"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop PulseCheckAgentService', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000);
  Result := '';
end;
