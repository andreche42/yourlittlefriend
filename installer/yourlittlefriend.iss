; installer di yourlittlefriend (Inno Setup 6). si compila con:
;   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
;   ISCC /DAppVersion=1.0.0 installer\yourlittlefriend.iss
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{B7E0C1A2-5C3D-4E8F-9A1B-6D2F4C8E7A30}
AppName=yourlittlefriend
AppVersion={#AppVersion}
AppPublisher=andreche42
; cartella predefinita: AppData\Roaming (si può cambiare nella procedura guidata)
DefaultDirName={userappdata}\yourlittlefriend
DisableProgramGroupPage=yes
; installazione per l'utente corrente, senza chiedere i permessi di amministratore
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=yourlittlefriend-setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; all'inizio chiede la lingua (italiano / english)
ShowLanguageDialog=yes
UsePreviousLanguage=no
; se l'app è aperta l'installer (e il disinstallatore) chiedono di chiuderla
AppMutex=yourlittlefriend-single
CloseApplications=yes
UninstallDisplayIcon={app}\yourlittlefriend.exe

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
italian.StartupTask=Avvia yourlittlefriend all'avvio del PC
english.StartupTask=Start yourlittlefriend when the PC starts
italian.LaunchApp=Avvia yourlittlefriend
english.LaunchApp=Launch yourlittlefriend

[Tasks]
; la domanda "vuoi farlo avviare all'avvio del pc?" (si può cambiare anche dalle impostazioni dell'app)
Name: "startup"; Description: "{cm:StartupTask}"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\yourlittlefriend"; Filename: "{app}\yourlittlefriend.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "yourlittlefriend"; ValueData: """{app}\yourlittlefriend.exe"""; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "yourlittlefriend"; Flags: deletevalue; Tasks: not startup
; la lingua scelta qui viene usata dall'app al primo avvio
Root: HKCU; Subkey: "Software\yourlittlefriend"; ValueType: string; ValueName: "Language"; ValueData: "it"; Languages: italian; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\yourlittlefriend"; ValueType: string; ValueName: "Language"; ValueData: "en"; Languages: english; Flags: uninsdeletekey

[Run]
Filename: "{app}\yourlittlefriend.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{userappdata}\yourlittlefriend\settings.json"
