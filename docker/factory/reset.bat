@echo off
REM reset.bat: reset My Morning News to factory defaults.
REM DESTRUCTIVE. Stops the stack, deletes every Docker volume (database, media with reports and audio, metrics),
REM restores server\mymorningnews.json from factory\mymorningnews.json, and starts fresh.
REM Usage: factory\reset.bat [--demo] [--yes]
REM   --demo  start with the factory demo overlay (factory\compose.factory.yaml)
REM   --yes   skip the confirmation prompt
setlocal
set DEMO=0
set YES=0
:args
if "%~1"=="" goto run
if /I "%~1"=="--demo" set DEMO=1
if /I "%~1"=="--yes" set YES=1
shift
goto args
:run
cd /d %~dp0\..
echo.
echo ==========================================================
echo   My Morning News: reset to factory defaults
echo ==========================================================
echo WARNING: this deletes all data: users, topics, briefings, audio, settings changes, and metrics.
if "%YES%"=="1" goto go
set /p CONFIRM=Type RESET to continue: 
if /I not "%CONFIRM%"=="RESET" (
  echo Aborted. Nothing was changed.
  exit /b 1
)
:go
echo [1/3] Stopping containers and removing volumes...
docker compose -f compose.yaml -f factory\compose.factory.yaml --profile tts --profile mcp --profile ollama down -v --remove-orphans
echo [2/3] Restoring factory settings...
copy /y factory\mymorningnews.json server\mymorningnews.json >nul
echo [3/3] Starting...
if "%DEMO%"=="1" (
  docker compose -f compose.yaml -f factory\compose.factory.yaml up -d --build
) else (
  docker compose up -d --build
)
docker ps -a --filter name=mymorningnews
echo.
echo Done. Open http://127.0.0.1:12101 and sign in as admin@mymorningnews / password (tenant default).
endlocal
