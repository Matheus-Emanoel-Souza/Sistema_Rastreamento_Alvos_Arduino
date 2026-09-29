@echo off
setlocal EnableDelayedExpansion
REM ============================================================
REM Compila o RadarTorres (Release), mostrando uma animacao de
REM progresso durante o restore/build, e abre o aplicativo ao final.
REM
REM Uso:
REM   build.bat            compila em Release (dotnet restore + build)
REM                         e ja roda o app compilado (via run.bat)
REM
REM A "%%" mostrada durante a compilacao e uma ANIMACAO/estimativa, nao
REM o progresso real do MSBuild (a CLI do dotnet nao expoe isso de
REM forma simples em texto puro) -- serve so pra indicar visualmente
REM que o processo esta rodando. O log completo de cada etapa e
REM mostrado no final, antes do "Pressione ENTER" -- pra dar tempo de
REM ler o que foi compilado sem a janela fechar sozinha.
REM
REM Para publicar um instalador distribuivel (self-contained +
REM Inno Setup), use build\publish.ps1 em vez deste script:
REM   powershell -ExecutionPolicy Bypass -File build\publish.ps1
REM ============================================================

set "SOLUTION=%~dp0RadarTorres.sln"
set "LOGDIR=%TEMP%\RadarTorres-build"
if not exist "%LOGDIR%" mkdir "%LOGDIR%" >nul 2>nul

REM Backspace de verdade (caractere 0x08) para reescrever a mesma linha
REM no console a cada atualizacao da animacao, em vez de imprimir uma
REM linha nova a cada tick (truque classico de batch).
for /F %%A in ('"prompt $H &for %%B in (1) do rem"') do set "BS=%%A"
set "APAGAR=%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERRO] .NET SDK nao encontrado no PATH. Instale o .NET 9 SDK:
    echo        https://dotnet.microsoft.com/download/dotnet/9.0
    goto :fim
)

set "RESTORE_LOG=%LOGDIR%\restore.log"
set "BUILD_LOG=%LOGDIR%\build.log"

call :RodarComAnimacao "Restaurando pacotes NuGet" restore restore "" "%RESTORE_LOG%"
if not "!STEP_OK!"=="1" (
    echo.
    echo [ERRO] Falha no dotnet restore. Log completo:
    echo ----------------------------------------------------------------------
    type "%RESTORE_LOG%"
    echo ----------------------------------------------------------------------
    goto :fim
)

call :RodarComAnimacao "Compilando RadarTorres.sln (Release)" build build "-c Release --no-restore" "%BUILD_LOG%"
if not "!STEP_OK!"=="1" (
    echo.
    echo [ERRO] Falha no dotnet build. Log completo:
    echo ----------------------------------------------------------------------
    type "%BUILD_LOG%"
    echo ----------------------------------------------------------------------
    goto :fim
)

echo.
echo ==^> Build concluido com sucesso. Log completo:
echo ----------------------------------------------------------------------
type "%BUILD_LOG%"
echo ----------------------------------------------------------------------
echo.
echo ==^> Abrindo o aplicativo...
call "%~dp0run.bat"

:fim
echo.
set /p _="Pressione ENTER para fechar..."
exit /b 0

REM ---------------------------------------------------------------
REM :RodarComAnimacao <rotulo exibido> <nome curto da etapa> <verbo do dotnet> <args extras> <arquivo de log>
REM Roda "dotnet <verbo> "%SOLUTION%" <args extras>" em segundo plano
REM (saida redirecionada para o log), mostra uma animacao de "%%"
REM enquanto isso e define STEP_OK=1/0 ao final, conforme o codigo de
REM saida real do comando. Verbo/args vem separados (em vez de um
REM comando pronto com aspas embutidas) porque batch nao tem como
REM escapar aspas dentro de aspas -- assim cada aspas so aparece uma
REM vez, sem aninhamento.
REM ---------------------------------------------------------------
:RodarComAnimacao
set "ROTULO=%~1"
set "ETAPA=%~2"
set "VERBO=%~3"
set "EXTRA=%~4"
set "LOG=%~5"
set "RUNNER=%LOGDIR%\%ETAPA%.cmd"
set "DONE_FLAG=%LOGDIR%\%ETAPA%.done"
set "EXIT_FLAG=%LOGDIR%\%ETAPA%.exit"
if exist "%DONE_FLAG%" del /q "%DONE_FLAG%" >nul 2>nul
if exist "%EXIT_FLAG%" del /q "%EXIT_FLAG%" >nul 2>nul

REM Mini-script proprio pra etapa, cada comando na sua propria linha: e
REM o jeito robusto de capturar o errorlevel do dotnet de verdade (numa
REM unica linha encadeada com "&", o cmd expande %%errorlevel%% de uma
REM vez so, ANTES do dotnet rodar de fato -- sempre voltaria 0). O
REM "(echo ...)" entre parenteses evita o cmd confundir um errorlevel
REM que comeca com digito (0, 1, 2...) com uma redireccao de handle
REM numerico (ex.: "0>arquivo" viraria redirecionar stdin, nao gravar
REM "0" no arquivo).
> "%RUNNER%" echo @echo off
>> "%RUNNER%" echo dotnet %VERBO% "%SOLUTION%" %EXTRA% ^>"%LOG%" 2^>^&1
>> "%RUNNER%" echo (echo %%errorlevel%%^)^>"%EXIT_FLAG%"
>> "%RUNNER%" echo (echo done^)^>"%DONE_FLAG%"

start "" /b cmd /c "%RUNNER%"

set "SPIN=- \ /"
set /a PCT=5
:anim_loop
if exist "%DONE_FLAG%" goto :anim_done
for %%S in (%SPIN%) do (
    set /a PCT+=!RANDOM! %% 4 + 1
    if !PCT! gtr 95 set /a PCT=95
    <nul set /p "=%APAGAR%  %ROTULO%... !PCT!%% %%S   "
    ping -n 2 127.0.0.1 >nul
)
goto :anim_loop

:anim_done
<nul set /p "=%APAGAR%  %ROTULO%... 100%%          "
echo.
set /p STEP_EXIT=<"%EXIT_FLAG%"
if "!STEP_EXIT!"=="0" (set "STEP_OK=1") else (set "STEP_OK=0")
exit /b 0
