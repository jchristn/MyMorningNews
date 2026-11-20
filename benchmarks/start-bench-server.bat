@echo off
rem Build the server from the working tree in Release and run it against the isolated bench stack
rem (benchmarks\docker\compose.yaml). The committed bench settings are copied into benchmarks\.run\server on every
rem start, so the server never reuses settings it saved earlier. Every MYMORNINGNEWS_* variable from the caller's
rem environment is cleared first, so a developer's own keys and overrides can never reach the bench server.
rem Runs in the foreground; stop it with Ctrl+C.
rem
rem   docker compose -f benchmarks\docker\compose.yaml up -d --wait
rem   benchmarks\start-bench-server.bat
setlocal
set "HERE=%~dp0"
set "ROOT=%HERE%.."
set "RUN=%HERE%.run\server"
if "%FRAMEWORK%"=="" set "FRAMEWORK=net10.0"

if not exist "%RUN%" mkdir "%RUN%"
dotnet build "%ROOT%\src\MyMorningNews.Server\MyMorningNews.Server.csproj" -c Release -f %FRAMEWORK% -nologo -v quiet || exit /b 1
copy /Y "%HERE%docker\config\mymorningnews.bench.json" "%RUN%\mymorningnews.json" >nul

for /f "delims==" %%v in ('set MYMORNINGNEWS_ 2^>nul') do set "%%v="
set "ANTHROPIC_API_KEY="
set "OPENAI_API_KEY="
set "GEMINI_API_KEY="

set "MYMORNINGNEWS_SETTINGS_FILE=%RUN%\mymorningnews.json"
cd /d "%RUN%"
dotnet "%ROOT%\src\MyMorningNews.Server\bin\Release\%FRAMEWORK%\MyMorningNews.Server.dll"
exit /b %ERRORLEVEL%
