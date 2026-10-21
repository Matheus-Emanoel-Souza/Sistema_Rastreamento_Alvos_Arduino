@echo off
setlocal
title Instalador Arduino IDE

echo ============================================
echo   Instalacao Arduino IDE
echo ============================================
echo.

where winget >nul 2>&1
if %errorlevel%==0 (
    echo Winget achado. Instalando Arduino IDE...
    winget install --id ArduinoSA.IDE.stable -e --accept-package-agreements --accept-source-agreements
    if %errorlevel%==0 (
        echo.
        echo Instalacao concluida via winget.
        goto :fim
    ) else (
        echo Winget falhou. Tentando download direto...
    )
)

set "DEST=%~dp0ArduinoIDE_Installer.exe"
set "URL=https://downloads.arduino.cc/arduino-ide/arduino-ide_latest_Windows_64bit.exe"

echo Baixando instalador de %URL%
powershell -NoProfile -Command "Invoke-WebRequest -Uri '%URL%' -OutFile '%DEST%'"

if not exist "%DEST%" (
    echo Falha no download. Verifique conexao ou baixe manualmente em https://www.arduino.cc/en/software
    goto :erro
)

echo Executando instalador...
start /wait "" "%DEST%" /S

echo.
echo Instalacao concluida via instalador baixado.
del "%DEST%" >nul 2>&1
goto :fim

:erro
echo.
echo ============================================
echo   Instalacao NAO concluida.
echo ============================================
pause
exit /b 1

:fim
echo.
echo ============================================
echo   Arduino IDE instalado com sucesso.
echo ============================================
pause
exit /b 0
