#define MyAppName "KeySnap"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "유디연구소"
#define MyAppURL "https://blog.naver.com/factoryud"
#define MyAppExeName "KeySnap.exe"

[Setup]
AppId={{5E47BF31-893C-4DA8-8547-A2692BF55829}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist\installer
OutputBaseFilename=KeySnap-Setup-v{#MyAppVersion}
SetupIconFile=..\app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter=KeySnap.exe
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "runatstartup"; Description: "Windows 시작 시 자동 실행 (Start with Windows)"; GroupDescription: "기타 설정 (Other Settings):"; Flags: unchecked

[Files]
Source: "..\dist\KeySnap.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\app_icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{#MyAppName}\KeySnap 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "KeySnap"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: runatstartup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\KeySnap"
Type: filesandordirs; Name: "{userappdata}\QuickReplace"
Type: filesandordirs; Name: "{localappdata}\KeySnap"
Type: filesandordirs; Name: "{app}"

[Code]
function InitializeUninstall(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  // 프로그램 제거 전 실행 중인 KeySnap 프로세스 강제 종료
  ShellExec('open', 'taskkill.exe', '/F /IM KeySnap.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataDir: string;
  OldAppDataDir: string;
  LocalAppDataDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    AppDataDir := ExpandConstant('{userappdata}\KeySnap');
    if DirExists(AppDataDir) then
    begin
      DelTree(AppDataDir, True, True, True);
    end;

    OldAppDataDir := ExpandConstant('{userappdata}\QuickReplace');
    if DirExists(OldAppDataDir) then
    begin
      DelTree(OldAppDataDir, True, True, True);
    end;

    LocalAppDataDir := ExpandConstant('{localappdata}\KeySnap');
    if DirExists(LocalAppDataDir) then
    begin
      DelTree(LocalAppDataDir, True, True, True);
    end;
  end;
end;
