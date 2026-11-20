@echo off
rem Deterministic regression gate. Runs only stub-backed checks whose results contain no real model judgment, no
rem judge, and no wall-clock latency: the dataset checks, quality on both committed datasets in every arm (fresh
rem workspaces), the resilience outcomes, and the end-to-end checks. Each report is compared with its committed
rem baseline in benchmarks\baselines\ in deterministic mode, where any single probe that gets worse is a regression.
rem Exits 0 when nothing regressed, 1 on a regression or failure, 3 when a run was incomplete.
rem Needs the bench stack and server running (benchmarks\README.md). Makes no paid calls.
rem
rem   benchmarks\ci-gate.bat            gate
rem   benchmarks\ci-gate.bat --update   write new baselines (a reviewed change; say why in the commit message)
setlocal EnableDelayedExpansion
set "HERE=%~dp0"
set "ROOT=%HERE%.."
set UPDATE=0
if "%~1"=="--update" set UPDATE=1
set "OUT=%HERE%.run\ci"
if not exist "%OUT%" mkdir "%OUT%"
if not exist "%HERE%baselines" mkdir "%HERE%baselines"
del /q "%OUT%\*.json" "%OUT%\*.md" 2>nul

dotnet build "%ROOT%\src\Test.Benchmark\Test.Benchmark.csproj" -c Release -nologo -v quiet || exit /b 1
set "BENCH=dotnet %ROOT%\src\Test.Benchmark\bin\Release\net10.0\MyMorningNews.Benchmark.dll"
set STATUS=0

python "%HERE%datasets\synthetic\build.py" --check || set STATUS=1
%BENCH% validate --dataset benchmarks\datasets\mymorningnews-synthetic.json,benchmarks\datasets\mymorningnews-live.json || set STATUS=1

call :gate quality mymorningnews-synthetic --dataset benchmarks\datasets\mymorningnews-synthetic.json --reingest
call :gate quality mymorningnews-live --dataset benchmarks\datasets\mymorningnews-live.json --reingest
call :gate resilience mymorningnews-synthetic
call :gate e2e mymorningnews-synthetic --repeat 1

echo.
if "%STATUS%"=="0" (echo GATE: pass) else (echo GATE: fail ^(%STATUS%^))
exit /b %STATUS%

:gate
set "KIND=%~1"
set "NAME=%~2"
set "ARGS="
shift
shift
:args
if "%~1"=="" goto run
set "ARGS=!ARGS! %1"
shift
goto args
:run
echo.
echo == gate %KIND% %NAME%
%BENCH% %KIND% --label ci --output-dir "%OUT%" --scrub !ARGS!
set CODE=%ERRORLEVEL%
if "%CODE%"=="3" (set STATUS=3& exit /b 0)
if not "%CODE%"=="0" (if "%STATUS%"=="0" set STATUS=1& exit /b 0)
set "CANDIDATE="
for /f "delims=" %%f in ('dir /b /o-d "%OUT%\*-%KIND%-%NAME%-ci.json" 2^>nul ^| findstr /v /c:"-compare-"') do if not defined CANDIDATE set "CANDIDATE=%OUT%\%%f"
set "BASELINE=%HERE%baselines\%KIND%-%NAME%.json"
if not defined CANDIDATE (echo no report written for %KIND% %NAME%& set STATUS=1& exit /b 0)
if "%UPDATE%"=="1" goto write
if not exist "%BASELINE%" goto write
%BENCH% compare --baseline "%BASELINE%" --candidate "%CANDIDATE%" --deterministic --output-dir "%OUT%" --name %KIND%-%NAME%-ci
if errorlevel 1 if "%STATUS%"=="0" set STATUS=1
exit /b 0
:write
copy /Y "%CANDIDATE%" "%BASELINE%" >nul
echo baseline written: %BASELINE%
exit /b 0
