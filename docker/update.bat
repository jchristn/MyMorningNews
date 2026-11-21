@echo off
rem Update the stack to the latest published images (jchristn77/mymorningnews-^<type^>:latest, built with the
rem repository build-*.bat scripts) and restart it. Named volumes (data, media, email, metrics) are preserved.
rem Optional profiles: set COMPOSE_PROFILES first, for example: set COMPOSE_PROFILES=mcp
rem (c) 2026 Lattice AI LLC
setlocal
pushd "%~dp0"
docker compose pull || goto :failed
docker compose down || goto :failed
docker compose up -d || goto :failed
docker ps -a
popd
endlocal
exit /b 0
:failed
echo Update failed.
popd
endlocal
exit /b 1
