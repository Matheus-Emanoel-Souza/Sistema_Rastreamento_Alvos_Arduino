; ============================================================================
; RadarTorres - Script do instalador (Inno Setup 6)
; ============================================================================
; Gera um instalador Windows (Setup.exe) para o aplicativo RadarTorres
; (Sistema de Detecção e Seleção de Torres - TCC), publicado como .NET 9
; self-contained (win-x64) - ou seja, o instalador já inclui o .NET Desktop
; Runtime dentro do pacote e a máquina do usuário final NÃO precisa ter
; nenhuma versão do .NET instalada previamente.
;
; Pré-requisito para COMPILAR este script: publicar o app antes com
;   build\publish.ps1
; que gera a pasta de publicação usada abaixo em #define PublishDir.
;
; Compilar manualmente:
;   "C:\Users\<usuario>\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer\RadarTorres.iss
; ============================================================================

#define MyAppName "RadarTorres"
#define MyAppExeName "RadarTorres.App.exe"
#define MyAppPublisher "TCC - Engenharia da Computacao"
#define MyAppURL "https://github.com/"
; Sem chaves aqui de propósito — "{" tem significado especial (constantes) nas
; seções do Inno Setup; a chave literal é adicionada só no uso, em AppId= abaixo.
#define MyAppId "6D13EAAB-61F6-4AE8-97B7-34498455E000"

; Versão pode ser sobrescrita na linha de comando com /DMyAppVersion=X.Y.Z
; (o script build\publish.ps1 faz isso automaticamente lendo o .csproj).
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

; Pasta com a publicação self-contained (saída de `dotnet publish`), relativa
; a este arquivo .iss. Pode ser sobrescrita com /DPublishDir=caminho.
#ifndef PublishDir
  #define PublishDir "..\src\RadarTorres.App\bin\Release\net9.0-windows\win-x64\publish"
#endif

#define IconFile "..\src\RadarTorres.App\Assets\RadarTorres.ico"

[Setup]
AppId={{{#MyAppId}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}

; Instala em Program Files\RadarTorres (Requisito 5).
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Self-contained x64: não há necessidade de checar/instalar .NET (ver README,
; seção "Por que self-contained"). O próprio Inno Setup já valida SO e
; arquitetura abaixo, com mensagens de erro nativas e claras caso incompatível.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

; Precisa de admin por instalar em Program Files e criar entrada em
; "Programas e Recursos" (Requisito 8). Pode ser reduzido via linha de
; comando (/ALLUSERS=0) só para testes automatizados de instalação.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline

OutputDir=..\dist
OutputBaseFilename=Setup
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=100

; Sem tela de licença (o app em si não exige aceite de licença para uso).

DisableWelcomePage=no
ShowLanguageDialog=auto

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Atalho na área de trabalho é OPCIONAL — desmarcado por padrão (Requisito 7).
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Todo o conteúdo publicado (self-contained: app + .NET Desktop Runtime),
; exceto o appsettings.json, que é tratado à parte para preservar
; configurações do usuário em atualizações (Requisito 9).
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "appsettings.json"; Flags: ignoreversion recursesubdirs createallsubdirs

; "onlyifdoesntexist": em uma atualização, se o usuário já tiver um
; appsettings.json (com porta COM, torres etc. customizadas), ele é
; preservado; só é gravado na primeira instalação.
Source: "{#PublishDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
; Atalho no Menu Iniciar (Requisito 7 - sempre criado).
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
; Atalho na área de trabalho (Requisito 7 - só se a tarefa for marcada).
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// O Inno Setup já cobre nativamente, com mensagens de erro claras e
// traduzidas (Requisito 10):
//  - SO/arquitetura incompatível (MinVersion / ArchitecturesAllowed acima);
//  - espaço em disco insuficiente;
//  - falta de permissão de administrador;
//  - arquivo em uso durante a cópia (pede para fechar o programa e tenta de novo).
// Não foi necessário código customizado adicional para isso.
