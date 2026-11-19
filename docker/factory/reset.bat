@echo off
REM DESTRUCTIVE: stops the stack and deletes all data volumes, then starts fresh.
cd /d %~dp0\..
set /p CONFIRM=This deletes all My Morning News data. Type RESET to continue: 
if /I not "%CONFIRM%"=="RESET" (
  echo Aborted.
  exit /b 1
)
docker compose down -v
copy /y factory\mymorningnews.json server\mymorningnews.json
docker compose up -d --build
docker ps -a
