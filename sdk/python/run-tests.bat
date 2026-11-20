@echo off
REM Runs the My Morning News Python SDK test harness against a live server.
REM Creates sdk\python\.venv on first use, installs requirements, and passes
REM every argument through to test_harness.py (for example --with-run or --verbose).
REM (c) 2026 Lattice AI LLC
setlocal
cd /d "%~dp0"

if not exist ".venv\Scripts\python.exe" (
  echo Creating virtual environment in .venv
  python -m venv .venv
  if errorlevel 1 exit /b 1
)

".venv\Scripts\python.exe" -m pip install --quiet --disable-pip-version-check -r requirements.txt
if errorlevel 1 exit /b 1

".venv\Scripts\python.exe" test_harness.py %*
exit /b %ERRORLEVEL%
