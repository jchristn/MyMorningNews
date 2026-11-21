@echo off
rem Build the My Morning News dashboard image on the Docker cloud builder for amd64 and arm64, push it to Docker Hub
rem as jchristn77/mymorningnews-dashboard:latest and jchristn77/mymorningnews-dashboard:^<tag^>, and pull both back. (c) 2026 Lattice AI LLC.
setlocal

if "%~1"=="" (
    echo Usage: build-dashboard.bat ^<tag^>
    echo Example: build-dashboard.bat v0.1.0
    endlocal
    exit /b 1
)

set "TAG=%~1"
set "IMAGE=jchristn77/mymorningnews-dashboard"

pushd "%~dp0"

rem Build on the cloud builder for both architectures and push the multi-arch manifest to Docker Hub.
echo Building %IMAGE%:latest and %IMAGE%:%TAG%...
docker buildx build ^
    --builder cloud-jchristn77-jchristn77 ^
    --platform linux/amd64,linux/arm64/v8 ^
    -t %IMAGE%:latest ^
    -t %IMAGE%:%TAG% ^
    -f dashboard\Dockerfile ^
    --push ^
    dashboard
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" goto :done

rem Pull the pushed tags back from Docker Hub to update the local copy.
echo Pulling %IMAGE%:latest...
docker pull %IMAGE%:latest
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" goto :done

echo Pulling %IMAGE%:%TAG%...
docker pull %IMAGE%:%TAG%
set "EXIT_CODE=%ERRORLEVEL%"

:done
echo Done.
popd
endlocal & exit /b %EXIT_CODE%
