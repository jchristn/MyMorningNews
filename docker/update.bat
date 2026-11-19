@echo off
REM Pull the latest images and recreate the stack. Named volumes are preserved.
cd /d %~dp0
docker compose pull
docker compose down
docker compose up -d
docker ps -a
