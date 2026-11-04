#define AppVersion "0.5.0"
[Setup]
#ifdef TestBuild
AppId={{9B32C142-B7D2-4635-A0D8-19D029D8D8C7}
OutputDir=..\..\work\test-installer
OutputBaseFilename=Incranalytic-0.5.0-TestSetup
DefaultGroupName=Incranalytic Test
#else
AppId={{82D861BC-A02B-4C41-B9EA-2878D66918B3}
OutputDir=..\..\outputs
OutputBaseFilename=Incranalytic-0.5.0-Setup
DefaultGroupName=Incranalytic
#endif
AppName=Incranalytic Studio
AppVersion={#AppVersion}
AppPublisher=Incranalytic Project
DefaultDirName={localappdata}\Programs\Incranalytic
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
DisableProgramGroupPage=no
LicenseFile=Terms.txt
UninstallDisplayIcon={app}\Incranalytic.exe
SetupLogging=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
#include "payload.iss"
Source: "Terms.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\studio-guide.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\IFCLOUD.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\samples\conditional-counter.incr"; DestDir: "{app}\examples"; Flags: ignoreversion
Source: "{#ManagerFile}"; DestDir: "{app}\manager"; Flags: ignoreversion
Source: "..\docs\version-manager.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "Studio-Source-Terms.txt"; Flags: ignoreversion
Source: "..\EXPORT-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\AI-DEVELOPMENT.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Incranalytic Studio"; Filename: "{app}\Incranalytic.exe"
Name: "{group}\Incranalytic Version Manager"; Filename: "{app}\manager\Incranalytic-Installer.exe"
Name: "{group}\Uninstall Incranalytic"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Incranalytic Studio"; Filename: "{app}\Incranalytic.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Incranalytic.exe"; Description: "Launch Incranalytic Studio"; Flags: nowait postinstall skipifsilent

[Code]
var
  LanguagePage: TInputOptionWizardPage;
  CleanPrivateRuntime: Boolean;
  CleanPreferences: Boolean;
  CleanRecovery: Boolean;
  RuntimeFiles: TArrayOfString;

#ifndef TestBuild
function DeleteManagerCredential(TargetName: String; CredentialType, Flags: LongWord): Boolean;
  external 'CredDeleteW@advapi32.dll stdcall';
#endif

procedure InitializeWizard;
begin
  LanguagePage := CreateInputOptionPage(wpWelcome, 'Installer language',
    'Choose the language for this installation', 'Currently available:', True, False);
  LanguagePage.Add('English');
  LanguagePage.SelectedValueIndex := 0;
end;

function InitializeUninstall: Boolean;
var
  Form: TSetupForm;
  RemoveRuntime, RemoveSettings, RemoveRecovery: TNewCheckBox;
  LabelText: TNewStaticText;
  OKButton, CancelButton: TNewButton;
begin
  LoadStringsFromFile(ExpandConstant('{app}\runtime-files.txt'), RuntimeFiles);
  CleanPrivateRuntime := CompareText(ExpandConstant('{param:KEEPRUNTIME|0}'), '1') <> 0;
  CleanPreferences := ExpandConstant('{param:REMOVECACHE|0}') = '1';
  CleanRecovery := ExpandConstant('{param:REMOVERECOVERY|0}') = '1';
  Result := True;
  if UninstallSilent then Exit;
  Form := CreateCustomForm(ScaleX(540), ScaleY(285), False, True);
  try
  Form.Caption := 'Uninstall Incranalytic - cleanup options';
  LabelText := TNewStaticText.Create(Form);
  LabelText.Parent := Form;
  LabelText.SetBounds(ScaleX(15), ScaleY(15), Form.ClientWidth - ScaleX(30), ScaleY(65));
  LabelText.AutoSize := False; LabelText.WordWrap := True;
  LabelText.Caption := 'Choose cleanup options. Your saved projects are preserved. Shared runtimes used by other applications are never removed.';
  RemoveRuntime := TNewCheckBox.Create(Form);
  RemoveRuntime.Parent := Form;
  RemoveRuntime.SetBounds(ScaleX(15), ScaleY(85), Form.ClientWidth - ScaleX(30), ScaleY(35));
  RemoveRuntime.Caption := 'Remove Incranalytic''s bundled .NET dependencies';
  RemoveRuntime.Checked := True;
  RemoveSettings := TNewCheckBox.Create(Form);
  RemoveSettings.Parent := Form;
  RemoveSettings.SetBounds(ScaleX(15), ScaleY(125), Form.ClientWidth - ScaleX(30), ScaleY(35));
  RemoveSettings.Caption := 'Also remove preferences, cache and saved GitHub token';
  RemoveSettings.Checked := False;
  RemoveRecovery := TNewCheckBox.Create(Form); RemoveRecovery.Parent := Form;
  RemoveRecovery.SetBounds(ScaleX(15), ScaleY(165), Form.ClientWidth - ScaleX(30), ScaleY(35));
  RemoveRecovery.Caption := 'Also delete unsaved recovery copies (cannot be undone)';
  RemoveRecovery.Checked := False;
  OKButton := TNewButton.Create(Form); OKButton.Parent := Form;
  OKButton.SetBounds(ScaleX(290), ScaleY(240), ScaleX(110), ScaleY(28));
  OKButton.Caption := 'Uninstall'; OKButton.ModalResult := mrOk;
  CancelButton := TNewButton.Create(Form); CancelButton.Parent := Form;
  CancelButton.SetBounds(ScaleX(410), ScaleY(240), ScaleX(110), ScaleY(28));
  CancelButton.Caption := 'Cancel'; CancelButton.ModalResult := mrCancel; CancelButton.Cancel := True;
  Result := Form.ShowModal() = mrOk;
  CleanPrivateRuntime := RemoveRuntime.Checked; CleanPreferences := RemoveSettings.Checked;
  CleanRecovery := RemoveRecovery.Checked;
  finally Form.Free(); end;
end;

function RuntimePathSafe(Name: String): Boolean;
begin
  Result := (Name <> '') and (Pos('..', Name) = 0) and (Pos(':', Name) = 0) and (Copy(Name, 1, 1) <> '\') and (Copy(Name, 1, 1) <> '/');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var I: Integer; Target, ParentDir: String; CleanRuntime: Boolean;
begin
  if CurUninstallStep = usPostUninstall then begin
    CleanRuntime := CleanPrivateRuntime;
    if CleanRuntime then begin
      for I := 0 to GetArrayLength(RuntimeFiles) - 1 do
        if RuntimePathSafe(RuntimeFiles[I]) then begin
          Target := ExpandConstant('{app}\') + RuntimeFiles[I];
          DeleteFile(Target);
          ParentDir := ExtractFileDir(Target);
          if CompareText(ParentDir, ExpandConstant('{app}')) <> 0 then RemoveDir(ParentDir);
        end;
      DeleteFile(ExpandConstant('{app}\runtime-files.txt'));
    end;
    if CleanPreferences then begin
#ifndef TestBuild
      DeleteManagerCredential('Incranalytic/GitHubReleases', 1, 0);
      DeleteFile(ExpandConstant('{localappdata}\Incranalytic\preferences.json'));
      DelTree(ExpandConstant('{localappdata}\Incranalytic\cache'), True, True, True);
      RemoveDir(ExpandConstant('{localappdata}\Incranalytic'));
#endif
    end;
    if CleanRecovery then begin
#ifndef TestBuild
      DelTree(ExpandConstant('{localappdata}\Incranalytic\recovery'), True, True, True);
      RemoveDir(ExpandConstant('{localappdata}\Incranalytic'));
#endif
    end;
    RemoveDir(ExpandConstant('{app}'));
  end;
end;
