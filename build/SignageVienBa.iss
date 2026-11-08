; Signage Viên Ba installer (Inno Setup 6). Built by build.ps1 from dist\app (app + ffmpeg folder).
; Installs per user (no admin prompt).
#define AppVersion GetEnv("SIGNAGE_VERSION")
#if AppVersion == ""
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{20ACB4F7-3BA5-497B-A9D2-90A25AC2BE49}
AppName=Signage Viên Ba
AppVersion={#AppVersion}
AppVerName=Signage Viên Ba {#AppVersion}
AppPublisher=Viên Ba
AppComments=Đưa ảnh, video lên TV của tòa nhà Viên Ba
DefaultDirName={localappdata}\Programs\SignageVienBa
DisableDirPage=auto
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=SignageVienBa-Setup-{#AppVersion}
SetupIconFile=..\src\SignageVienBa.App\app.ico
UninstallDisplayIcon={app}\SignageVienBa.exe
UninstallDisplayName=Signage Viên Ba
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=2
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=no

[Languages]
Name: "vi"; MessagesFile: "Vietnamese.isl"

[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng ngoài màn hình"; GroupDescription: "Thêm:"

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Signage Viên Ba"; Filename: "{app}\SignageVienBa.exe"
Name: "{autodesktop}\Signage Viên Ba"; Filename: "{app}\SignageVienBa.exe"; Tasks: desktopicon

[UninstallDelete]
; the guide window's WebView2 cache (settings in %APPDATA%\SignageVienBa stay, as before)
Type: filesandordirs; Name: "{localappdata}\SignageVienBa\WebView2"
Type: dirifempty; Name: "{localappdata}\SignageVienBa"

[Run]
Filename: "{app}\SignageVienBa.exe"; Description: "Mở Signage Viên Ba ngay"; Flags: nowait postinstall skipifsilent
; An update started from inside the app runs silently: open the app again when it is done.
Filename: "{app}\SignageVienBa.exe"; Parameters: "--updated"; Flags: nowait; Check: IsUpdate

[Code]
// The app starts its own update with /UPDATE=1 and then closes.
function IsUpdate: Boolean;
begin
  Result := ExpandConstant('{param:update|0}') = '1';
end;

// Give the app up to 30 seconds to close before its files are replaced.
function InitializeSetup: Boolean;
var
  i: Integer;
begin
  if IsUpdate then
    for i := 1 to 60 do
    begin
      if not CheckForMutexes('Local\SignageVienBa-' + GetUserNameString) then Break;
      Sleep(500);
    end;
  Result := True;
end;
