; Instalador do Pitanga (Inno Setup 6).
; Instala por usuário, sem pedir senha de administrador, em %LOCALAPPDATA%\Programs\Pitanga.
;
; Compilar (a partir da raiz do repositório, depois do `dotnet publish` — ver build.ps1):
;   ISCC.exe /DAppVersion=1.0.0 installer\Pitanga.iss

#define AppName "Pitanga"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
; Campo de versão do arquivo só aceita números (1.2.3); sufixos como -ci.5 ficam só no AppVersion
#ifndef NumericVersion
  #define NumericVersion AppVersion
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#ifndef RepoUrl
  #define RepoUrl "https://github.com/GusGgk/pitanga"
#endif

[Setup]
; AppId fixo: é o que faz uma versão nova atualizar a anterior em vez de instalar ao lado. Não mude.
AppId={{F9DAC577-3739-4CF8-9C90-3DAC1CF4C14E}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=GusGk
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
AppCopyright=Copyright © 2026 GusGk
VersionInfoVersion={#NumericVersion}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DisableDirPage=auto
DisableProgramGroupPage=yes
DisableReadyPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\artifacts
OutputBaseFilename=Pitanga-Setup-{#AppVersion}
SetupIconFile=..\src\Pitanga\Assets\Pitanga.ico
UninstallDisplayIcon={app}\Pitanga.exe
UninstallDisplayName={#AppName}
LicenseFile=..\LICENSE
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
; Fecha o Pitanga em execução antes de substituir o .exe (atualizações)
CloseApplications=force
CloseApplicationsFilter=Pitanga.exe
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
en.StartupTask=Start Pitanga with Windows
ptbr.StartupTask=Iniciar o Pitanga junto com o Windows
en.LaunchNow=Launch Pitanga now
ptbr.LaunchNow=Abrir o Pitanga agora

[Tasks]
Name: "startup"; Description: "{cm:StartupTask}"

[Files]
Source: "{#PublishDir}\Pitanga.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PRIVACY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\Pitanga.exe"; Comment: "Drag a file, tap Shift"

[Registry]
; Mesma entrada que a opção "Start with Windows" do menu da bandeja escreve.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Pitanga"; ValueData: """{app}\Pitanga.exe"""; Tasks: startup

[Run]
Filename: "{app}\Pitanga.exe"; Description: "{cm:LaunchNow}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM Pitanga.exe /F"; Flags: runhidden; RunOnceId: "StopPitanga"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Pitanga"
Type: filesandordirs; Name: "{userappdata}\Pitanga"
Type: dirifempty; Name: "{app}"

[Code]
// A entrada de inicialização pode ter sido criada depois, pelo menu da bandeja (não pela tarefa do
// instalador). Remove sempre na desinstalação para não deixar o Windows tentando abrir um .exe apagado.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Pitanga');
end;
