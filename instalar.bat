@echo off
REM ============================================================
REM Gera o instalador Windows do RadarTorres (Setup.exe).
REM
REM Uso:
REM   instalar.bat            publica self-contained + gera dist\Setup.exe
REM
REM Requer o Inno Setup 6 instalado (https://jrsoftware.org/isdl.php).
REM Este script e apenas um atalho para build\publish.ps1 (o pipeline
REM completo de build -> publish -> instalador).
REM ============================================================
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build\publish.ps1"
if errorlevel 1 (
    echo [ERRO] Falha ao gerar o instalador. Veja as mensagens acima.
    exit /b 1
)

echo.
echo ==^> Instalador gerado em dist\Setup.exe
exit /b 0
