@echo off
rem Run the My Morning News JavaScript SDK test harness against a live server.
rem Arguments pass through, for example: run-tests.bat --base-url http://127.0.0.1:12100 --verbose
rem (c) 2026 Lattice AI LLC
setlocal
cd /d "%~dp0"
node test-harness.js %*
exit /b %ERRORLEVEL%
