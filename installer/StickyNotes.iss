; StickyNotes kurulum paketi (Inno Setup 6)
; Derleme: iscc /DAppVersion=0.3.0 /DPublishDir=<tam yol> installer\StickyNotes.iss
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\dist\win-x64"
#endif

[Setup]
; AppId bir kez üretildi, ASLA değiştirme (güncelleme aynı kuruluma yazsın diye)
AppId={{6F1D2C58-3B7A-4E0B-9C41-5A8E7D2B90C3}
AppName=StickyNotes
AppVersion={#AppVersion}
AppPublisher=TeknoLAB
DefaultDirName={autopf}\StickyNotes
DefaultGroupName=StickyNotes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\StickyNotes.App\Assets\app.ico
UninstallDisplayIcon={app}\StickyNotes.exe
OutputDir=Output
OutputBaseFilename=StickyNotes-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Uygulama çalışırken kurulum "kapatın" uyarısı verir (App.OnStartup sabit mutex'leri açar)
AppMutex=StickyNotesSetupMutex,Global\StickyNotesSetupMutex
CloseApplications=no

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:AutoStartProgram,StickyNotes}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\StickyNotes.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\StickyNotes"; Filename: "{app}\StickyNotes.exe"
Name: "{group}\{cm:UninstallProgram,StickyNotes}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\StickyNotes"; Filename: "{app}\StickyNotes.exe"; Tasks: desktopicon
Name: "{userstartup}\StickyNotes"; Filename: "{app}\StickyNotes.exe"; Tasks: autostart

[Run]
Filename: "{app}\StickyNotes.exe"; Description: "{cm:LaunchProgram,StickyNotes}"; Flags: nowait postinstall skipifsilent

; [UninstallDelete] bilerek yok: %LOCALAPPDATA%\StickyNotes (notlar, ayarlar, log) kaldırmada silinmez.
