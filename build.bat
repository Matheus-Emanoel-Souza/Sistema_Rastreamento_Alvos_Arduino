@echo off
REM ============================================================
REM Compila o RadarTorres (Release) automaticamente.
REM
REM Uso:
REM   build.bat            compila em Release (dotnet restore + build)
REM
REM Depois de compilar, rode run.bat para abrir o aplicativo.
REM
REM Para publicar um instalador distribuivel (self-contained +
REM Inno Setup), use build\publish.ps1 em vez deste script:
REM   powershell -ExecutionPolicy Bypass -File build\publish.ps1
REM ============================================================
setlocal

set "SOLUTION=%~dp0RadarTorres.sln"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERRO] .NET SDK nao encontrado no PATH. Instale o .NET 9 SDK:
    echo        https://dotnet.microsoft.com/download/dotnet/9.0
    exit /b 1
)

echo ==^> Restaurando pacotes NuGet...
dotnet restore "%SOLUTION%"
if errorlevel 1 (
    echo [ERRO] Falha no dotnet restore.
    exit /b 1
)

echo.
echo ==^> Compilando RadarTorres.sln (Release)...
dotnet build "%SOLUTION%" -c Release --no-restore
if errorlevel 1 (
    echo [ERRO] Falha no dotnet build.
    exit /b 1
)

echo.
echo ==^> Build concluido com sucesso.
echo     Para rodar o aplicativo, use: run.bat
exit /b 0
