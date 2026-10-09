; installer di yourlittlefriend (Inno Setup 6). si compila con:
;   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
;   ISCC /DAppVersion=1.1.3 installer\yourlittlefriend.iss
#ifndef AppVersion
  #define AppVersion "1.1.3"
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
; all'inizio chiede la lingua (italiano / english), proponendo quella usata l'ultima volta
ShowLanguageDialog=yes
UsePreviousLanguage=yes
; aggiornamenti: stesso AppId = si installa sopra la versione vecchia, nella stessa cartella (la pagina della cartella
; viene saltata), e impostazioni e portafile non si toccano. l'app aperta la chiude lo script qui sotto
DisableDirPage=auto
UsePreviousAppDir=yes
UsePreviousTasks=no
CloseApplications=no
SetupMutex=yourlittlefriend-setup
SetupIconFile=..\icon.ico
UninstallDisplayIcon={app}\yourlittlefriend.exe

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
italian.StartupTask=Avvia yourlittlefriend all'avvio del PC
english.StartupTask=Start yourlittlefriend when the PC starts
italian.DowngradeWarn=Hai già installato una versione più recente (%1) di quella di questo installer (%2).%nVuoi installare comunque la versione più vecchia?
english.DowngradeWarn=A newer version (%1) than this installer's (%2) is already installed.%nDo you want to install the older version anyway?
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
Type: files; Name: "{userappdata}\yourlittlefriend\holder.json"

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B7E0C1A2-5C3D-4E8F-9A1B-6D2F4C8E7A30}_is1';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

var
  TasksAdjusted: Boolean;

// versione già installata ('' se è la prima installazione)
function InstalledVersion(): String;
begin
  if not RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', Result) then
    Result := '';
end;

// chiude l'app se è aperta (aggiornamento o disinstallazione)
procedure CloseApp();
var
  R: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM yourlittlefriend.exe', '', SW_HIDE, ewWaitUntilTerminated, R);
  Sleep(400);
end;

// se è già installata una versione più nuova avvisa prima di tornare indietro
function InitializeSetup(): Boolean;
var
  OldV, NewV: Int64;
  Old: String;
begin
  Result := True;
  Old := InstalledVersion();
  if (Old <> '') and StrToVersion(Old, OldV) and StrToVersion('{#AppVersion}', NewV) then
    if ComparePackedVersion(OldV, NewV) > 0 then
      Result := MsgBox(FmtMessage(CustomMessage('DowngradeWarn'), [Old, '{#AppVersion}']), mbConfirmation, MB_YESNO) = IDYES;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseApp();
  Result := '';
end;

// in un aggiornamento la casella dell'avvio automatico rispecchia com'è adesso (anche se l'hai cambiata dalle impostazioni dell'app)
procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpSelectTasks) and (not TasksAdjusted) then
  begin
    TasksAdjusted := True;
    if InstalledVersion() <> '' then
    begin
      if RegValueExists(HKCU, RunKey, 'yourlittlefriend') then
        WizardSelectTasks('startup')
      else
        WizardSelectTasks('!startup');
    end;
  end;
end;

function InitializeUninstall(): Boolean;
begin
  CloseApp();
  Result := True;
end;
