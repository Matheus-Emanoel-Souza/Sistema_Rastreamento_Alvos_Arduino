<#
.SYNOPSIS
    Compila o RadarTorres, publica como .NET self-contained (win-x64) e gera
    o instalador Windows (Setup.exe) com o Inno Setup.

.DESCRIPTION
    Pipeline completo usado para gerar uma nova versão distribuível do app:
      1. dotnet restore + build (Release)
      2. dotnet publish self-contained win-x64 (inclui o .NET Desktop Runtime)
      3. Compila installer\RadarTorres.iss com o Inno Setup (ISCC.exe)
      4. Copia/gera o instalador final em dist\Setup.exe

.PARAMETER SkipInstaller
    Só compila/publica o app; não gera o instalador (útil para testar o build
    sem precisar do Inno Setup instalado).

.PARAMETER InnoSetupPath
    Caminho para o ISCC.exe. Se omitido, tenta localizar automaticamente nos
    locais de instalação mais comuns (Program Files e instalação por usuário).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\publish.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\publish.ps1 -SkipInstaller
#>
param(
    [switch]$SkipInstaller,
    [string]$InnoSetupPath
)

$ErrorActionPreference = "Stop"

function Write-Step($message) {
    Write-Host ""
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Write-Ok($message) {
    Write-Host "    OK: $message" -ForegroundColor Green
}

function Write-Err($message) {
    Write-Host "    ERRO: $message" -ForegroundColor Red
}

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot "src\RadarTorres.App"
$Csproj     = Join-Path $ProjectDir "RadarTorres.App.csproj"
$IssFile    = Join-Path $RepoRoot "installer\RadarTorres.iss"
$DistDir    = Join-Path $RepoRoot "dist"
$Runtime    = "win-x64"
$PublishDir = Join-Path $ProjectDir "bin\Release\net9.0-windows\$Runtime\publish"

if (-not (Test-Path $Csproj)) {
    Write-Err "Projeto não encontrado em $Csproj"
    exit 1
}

# --- 1. Verifica pré-requisitos -------------------------------------------
Write-Step "Verificando .NET SDK"
try {
    $dotnetVersion = (& dotnet --version).Trim()
    Write-Ok "dotnet SDK $dotnetVersion encontrado"
}
catch {
    Write-Err "dotnet SDK não encontrado no PATH. Instale o .NET 9 SDK: https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
}

# --- 2. Lê a versão do .csproj (fonte única da verdade) --------------------
$csprojContent = Get-Content $Csproj -Raw
if ($csprojContent -match "<Version>(.*?)</Version>") {
    $AppVersion = $Matches[1]
}
else {
    $AppVersion = "1.0.0"
}
Write-Step "Versão do aplicativo: $AppVersion"

# --- 3. Restore + build ------------------------------------------------
Write-Step "Restaurando pacotes NuGet"
& dotnet restore $Csproj
if ($LASTEXITCODE -ne 0) { Write-Err "Falha no dotnet restore"; exit 1 }
Write-Ok "Pacotes restaurados"

Write-Step "Compilando (Release)"
& dotnet build $Csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { Write-Err "Falha no dotnet build"; exit 1 }
Write-Ok "Build concluído"

# --- 4. Publish self-contained ------------------------------------------
Write-Step "Publicando self-contained ($Runtime) - inclui o .NET Desktop Runtime"
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}
& dotnet publish $Csproj `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --no-restore
if ($LASTEXITCODE -ne 0) { Write-Err "Falha no dotnet publish"; exit 1 }

$ExePath = Join-Path $PublishDir "RadarTorres.App.exe"
if (-not (Test-Path $ExePath)) {
    Write-Err "Publicação concluiu mas RadarTorres.App.exe não foi encontrado em $PublishDir"
    exit 1
}
$publishSizeMb = [Math]::Round(((Get-ChildItem $PublishDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
Write-Ok "Publicado em $PublishDir ($publishSizeMb MB, self-contained)"

if ($SkipInstaller) {
    Write-Step "SkipInstaller ativo - encerrando sem gerar o Setup.exe"
    exit 0
}

# --- 5. Localiza o Inno Setup (ISCC.exe) --------------------------------
Write-Step "Localizando o Inno Setup (ISCC.exe)"
if (-not $InnoSetupPath) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $InnoSetupPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $InnoSetupPath -or -not (Test-Path $InnoSetupPath)) {
    Write-Err "ISCC.exe (Inno Setup) não encontrado. Instale o Inno Setup 6: https://jrsoftware.org/isdl.php"
    Write-Err "Ou informe o caminho manualmente: -InnoSetupPath 'C:\caminho\ISCC.exe'"
    exit 1
}
Write-Ok "Inno Setup encontrado em $InnoSetupPath"

# --- 6. Compila o instalador ---------------------------------------------
Write-Step "Gerando o instalador (Setup.exe)"
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

$relativePublishDir = Resolve-Path $PublishDir
& $InnoSetupPath "/DMyAppVersion=$AppVersion" "/DPublishDir=$relativePublishDir" $IssFile
if ($LASTEXITCODE -ne 0) { Write-Err "Falha ao compilar o instalador com o Inno Setup"; exit 1 }

$SetupExe = Join-Path $DistDir "Setup.exe"
if (-not (Test-Path $SetupExe)) {
    Write-Err "Setup.exe não encontrado em $DistDir após a compilação"
    exit 1
}
$setupSizeMb = [Math]::Round(((Get-Item $SetupExe).Length / 1MB), 1)
Write-Ok "Instalador gerado: $SetupExe ($setupSizeMb MB)"

Write-Step "Concluído"
Write-Host "    Aplicativo publicado : $PublishDir" -ForegroundColor Yellow
Write-Host "    Instalador final     : $SetupExe" -ForegroundColor Yellow
Write-Host "    Versão               : $AppVersion" -ForegroundColor Yellow
