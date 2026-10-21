@echo off
REM ============================================================
REM Roda o RadarTorres ja compilado (use build.bat antes, se
REM ainda nao tiver compilado).
REM
REM Uso:
REM   run.bat            roda o build Release existente
REM ============================================================
setlocal

set "EXE=%~dp0src\RadarTorres.App\bin\Release\net9.0-windows\RadarTorres.App.exe"

if not exist "%EXE%" (
    echo [ERRO] Executavel nao encontrado em:
    echo        %EXE%
    echo.
    echo Compile o app primeiro com build.bat.
    exit /b 1
)

echo ==^> Iniciando RadarTorres.App...
start "" "%EXE%"
