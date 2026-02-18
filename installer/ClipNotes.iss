; ClipNotes Inno Setup Script
; Requires Inno Setup 6.x: https://jrsoftware.org/isdl.php
;
; Usage:
;   1. Build the app first: .\build.ps1
;   2. Open this file in Inno Setup Compiler (or run from cmd):
;      "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" ClipNotes.iss
;   Output: installer\Output\ClipNotes-Setup.exe

#define MyAppName        "ClipNotes"
#define MyAppVersion     "1.0.0"
#define MyAppPublisher   "ClipNotes"
#define MyAppURL         "https://github.com/your-username/ClipNotes"
#define MyAppExeName     "ClipNotes.exe"
#define AppDir           "..\app"
#define InstallerDir     "."

[Setup]
AppId={{A3F7E2C1-4D89-4B2E-9F1A-3C8D5E7F9B0A}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir={#InstallerDir}\Output
OutputBaseFilename=ClipNotes-Setup
SetupIconFile={#AppDir}\ClipNotes.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardImageFile={#InstallerDir}\wizard_sidebar.bmp
WizardSmallImageFile={#InstallerDir}\wizard_header.bmp
; Require Windows 10 x64
MinVersion=10.0
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; Privacy / UAC
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; Uninstall
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}

[Languages]
Name: "russian";  MessagesFile: "compiler:Languages\Russian.isl"
Name: "english";  MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";      Description: "{cm:CreateDesktopIcon}";  GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon";      Description: "Добавить в автозапуск Windows";  GroupDescription: "При запуске:"; Flags: unchecked
Name: "whisper_cpu";      Description: "CPU (OpenBLAS) — работает на любом компьютере";  GroupDescription: "Движок транскрипции:"; Flags: exclusive
Name: "whisper_cuda";     Description: "GPU CUDA — только NVIDIA (требует CUDA 12 Runtime)";  GroupDescription: "Движок транскрипции:"; Flags: exclusive unchecked

[Files]
; Main executable
Source: "{#AppDir}\{#MyAppExeName}";  DestDir: "{app}"; Flags: ignoreversion

; Tools — CPU whisper (default)
Source: "{#AppDir}\tools\*"; DestDir: "{app}\tools"; Flags: ignoreversion recursesubdirs; Tasks: whisper_cpu
; Tools — CUDA whisper (user choice)
; NOTE: Build CUDA whisper first: .\tools\download-whisper.ps1 -Backend cuda
; Then put it in app-cuda\tools\ and uncomment below:
; Source: "{#InstallerDir}\..\app-cuda\tools\*"; DestDir: "{app}\tools"; Flags: ignoreversion recursesubdirs; Tasks: whisper_cuda

; Models (large file — user may have downloaded separately)
Source: "{#AppDir}\models\*"; DestDir: "{app}\models"; Flags: ignoreversion skipifsourcedoesntexist

; Licenses
Source: "{#AppDir}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion

; Runtime DLLs (published alongside exe)
Source: "{#AppDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#AppDir}\*.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Удалить {#MyAppName}";  Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}";  Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "ClipNotes"; \
  ValueData: """{app}\{#MyAppExeName}"" --tray"; \
  Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Logs"
Type: filesandordirs; Name: "{app}\tools\.cache"

[Code]
// Check .NET 8 presence (optional — app is self-contained so always works)
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

// Warn if no model files found
procedure CurPageChanged(CurPageID: Integer);
var
  ModelDir: String;
  FindRec: TFindRec;
  HasModel: Boolean;
begin
  if CurPageID = wpSelectDir then
  begin
    // nothing
  end;
  if CurPageID = wpReady then
  begin
    ModelDir := ExpandConstant('{src}') + '\..\app\models\';
    HasModel := FindFirst(ModelDir + 'ggml-*.bin', FindRec);
    FindClose(FindRec);
    if not HasModel then
      MsgBox(
        'Файл модели Whisper не найден в app\models\.' + #13#10 +
        'Транскрипция будет недоступна до загрузки модели.' + #13#10#13#10 +
        'После установки запустите в папке приложения:' + #13#10 +
        '  .\build.ps1 -SkipDependencies -Model base' + #13#10 +
        'или скачайте модель вручную с Hugging Face.',
        mbInformation, MB_OK);
  end;
end;
