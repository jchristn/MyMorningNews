@echo off
rem The standard suite: ingest and quality on every committed dataset, e2e on a sample, resilience, and load, all
rem stub-backed. The bench stack and server must be running (benchmarks\README.md).
rem
rem   benchmarks\run-baseline.bat <label> [extra options forwarded to every command]
rem
rem Opt-in switches (never on by default):
rem   set REINGEST=1                       research every workspace again (after changing code on the research path)
rem   set PAID=1 and set MAX_SPEND_USD=1.00  also run quality and e2e against a real model; needs
rem                                        MYMORNINGNEWS_BENCH_PAID_API_KEY and stops at the spend cap. This spends money.
setlocal EnableDelayedExpansion
set "HERE=%~dp0"
set "ROOT=%HERE%.."
set "LABEL=%~1"
if "%LABEL%"=="" (
  echo usage: benchmarks\run-baseline.bat ^<label^> [options]
  exit /b 2
)
shift
set "EXTRA="
:collect
if "%~1"=="" goto collected
set "EXTRA=!EXTRA! %1"
shift
goto collect
:collected

dotnet build "%ROOT%\src\Test.Benchmark\Test.Benchmark.csproj" -c Release -nologo -v quiet || exit /b 1
set "BENCH=dotnet %ROOT%\src\Test.Benchmark\bin\Release\net10.0\MyMorningNews.Benchmark.dll"
set "REINGEST_FLAG="
if "%REINGEST%"=="1" set "REINGEST_FLAG=--reingest"
set STATUS=0

for %%D in (benchmarks\datasets\mymorningnews-synthetic.json benchmarks\datasets\mymorningnews-live.json) do (
  call :run validate --dataset %%D
  call :run ingest --dataset %%D --label %LABEL% %REINGEST_FLAG% !EXTRA!
  call :run quality --dataset %%D --label %LABEL% !EXTRA!
)
call :run e2e --label %LABEL% --repeat 3 !EXTRA!
call :run resilience --label %LABEL% !EXTRA!
call :run load --label %LABEL% --open-loop !EXTRA!

if "%PAID%"=="1" (
  if "%MAX_SPEND_USD%"=="" (
    echo PAID=1 needs MAX_SPEND_USD ^(the spend cap for each paid command^).
    exit /b 2
  )
  call :run quality --dataset benchmarks\datasets\mymorningnews-synthetic.json --label %LABEL%-paid --paid --max-spend-usd %MAX_SPEND_USD% --arms default !EXTRA!
  call :run e2e --label %LABEL%-paid --repeat 1 --paid --max-spend-usd %MAX_SPEND_USD% !EXTRA!
)
exit /b %STATUS%

:run
echo.
echo == %*
%BENCH% %*
if errorlevel 1 (
  echo exit %ERRORLEVEL%: %*
  set STATUS=1
)
exit /b 0
