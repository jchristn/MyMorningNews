@echo off
REM reset.bat: reset My Morning News to factory defaults.
REM DESTRUCTIVE. Stops the stack, deletes every Docker volume of its compose project (PostgreSQL, media with reports and
REM audio, Prometheus, Tempo, Loki, Grafana), restores server\mymorningnews.json from factory\mymorningnews.json, and
REM starts fresh. The project is "mymorningnews" unless COMPOSE_PROJECT_NAME is set; other projects are never touched.
REM Usage: factory\reset.bat [--demo] [--keep-ollama] [--yes]
REM   --demo         start with the factory demo overlay (factory\compose.factory.yaml): a demo user (demo@mymorningnews /
REM                  password) with topics, completed runs, findings, evidence, predictions, suggestions, briefings, and
REM                  feedback, seeded without model calls or an API key
REM   --keep-ollama  keep the Ollama model volume (only relevant with --profile ollama) so models are not downloaded again
REM   --yes          skip the confirmation prompt (for scripted resets)
setlocal
set DEMO=0
set KEEP_OLLAMA=0
set YES=0
:args
if "%~1"=="" goto run
if /I "%~1"=="--demo" (
  set DEMO=1
) else if /I "%~1"=="--keep-ollama" (
  set KEEP_OLLAMA=1
) else if /I "%~1"=="--yes" (
  set YES=1
) else (
  echo Unknown option: %~1
  exit /b 2
)
shift
goto args
:run
cd /d %~dp0\..
REM The compose project this reset applies to; only its containers and volumes are touched.
set PROJECT=
for /f "tokens=1,* delims=: " %%a in ('docker compose -f compose.yaml -f factory\compose.factory.yaml config 2^>nul ^| findstr /b "name:"') do set PROJECT=%%~b
if "%PROJECT%"=="" (
  echo Could not read the compose project name ^(docker compose config failed^). Nothing was changed.
  exit /b 1
)
echo.
echo ==========================================================
echo   My Morning News: reset to factory defaults
echo ==========================================================
echo Compose project: %PROJECT%
echo WARNING: this deletes all data in this project: users, topics, briefings, audio, settings changes, and metrics.
if "%YES%"=="1" goto go
set /p CONFIRM=Type RESET to continue:
if /I not "%CONFIRM%"=="RESET" (
  echo Aborted. Nothing was changed.
  exit /b 1
)
:go
echo [1/3] Stopping containers and removing volumes...
if not "%KEEP_OLLAMA%"=="1" goto dropall
docker compose -f compose.yaml -f factory\compose.factory.yaml --profile tts --profile mcp --profile ollama down --remove-orphans
REM Select volumes by the labels compose puts on them, never by name, so another project's volumes are never touched.
set OLLAMA_VOLUME=
for /f %%o in ('docker volume ls -q --filter "label=com.docker.compose.project=%PROJECT%" --filter "label=com.docker.compose.volume=ollama"') do set OLLAMA_VOLUME=%%o
for /f %%v in ('docker volume ls -q --filter "label=com.docker.compose.project=%PROJECT%"') do if /I not "%%v"=="%OLLAMA_VOLUME%" docker volume rm %%v >nul
goto restore
:dropall
docker compose -f compose.yaml -f factory\compose.factory.yaml --profile tts --profile mcp --profile ollama down -v --remove-orphans
:restore
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
if "%DEMO%"=="1" echo Demo data: sign in as demo@mymorningnews / password. Seeder logs: docker logs mymorningnews-factory-seed-data
endlocal
