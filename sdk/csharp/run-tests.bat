@echo off
rem Run the My Morning News C# SDK harness against a live server.
rem Usage: run-tests.bat [--base-url URL] [--email EMAIL] [--password PASSWORD] [--tenant ID] [--with-run] [--verbose]
rem Environment: MYMORNINGNEWS_BASE_URL, MYMORNINGNEWS_EMAIL, MYMORNINGNEWS_PASSWORD, MYMORNINGNEWS_FRAMEWORK (default net10.0).
rem (c) 2026 Lattice AI LLC
setlocal
cd /d "%~dp0"
if "%MYMORNINGNEWS_FRAMEWORK%"=="" set MYMORNINGNEWS_FRAMEWORK=net10.0
dotnet build MyMorningNews.Sdk.sln --nologo -v q
if errorlevel 1 exit /b %errorlevel%
dotnet run --no-build --project MyMorningNews.Sdk.TestHarness -f %MYMORNINGNEWS_FRAMEWORK% -- %*
exit /b %errorlevel%
