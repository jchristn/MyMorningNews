@echo off
REM Update the stack: rebuild the My Morning News images from this working tree, pull newer versions of the published
REM images it uses (pgvector, Grafana), and recreate any container whose image or settings changed. Named volumes (data,
REM media, email, metrics) are preserved. Run it after git pull.
REM
REM Optional profiles: set COMPOSE_PROFILES first to update them too, for example: set COMPOSE_PROFILES=mcp,tts
REM (c) 2026 Lattice AI LLC
cd /d %~dp0

echo [1/2] Pulling published images (locally built images are skipped)...
docker compose pull --ignore-buildable
if errorlevel 1 goto :fail

echo [2/2] Rebuilding local images and recreating changed containers...
docker compose up -d --build
if errorlevel 1 goto :fail

docker ps -a --filter name=mymorningnews
exit /b 0

:fail
echo Update failed; see the error above. Containers that were running are still running on their previous images.
exit /b 1
