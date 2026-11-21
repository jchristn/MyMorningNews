@echo off
rem Build every My Morning News image on the Docker cloud builder for amd64 and arm64, push each to Docker Hub as
rem jchristn77/mymorningnews-^<type^>:latest and :^<tag^>, and pull them back. (c) 2026 Lattice AI LLC.
setlocal

if "%~1"=="" (
    echo Usage: build-all.bat ^<tag^>
    echo Example: build-all.bat v0.1.0
    endlocal
    exit /b 1
)

set "TAG=%~1"

echo === Building My Morning News server (REST API, pipeline, workers) image ===
call "%~dp0build-server.bat" %TAG% || goto :failed

echo === Building My Morning News dashboard image ===
call "%~dp0build-dashboard.bat" %TAG% || goto :failed

echo === Building My Morning News MCP server image ===
call "%~dp0build-mcp.bat" %TAG% || goto :failed

echo === Building My Morning News SearXNG image ===
call "%~dp0build-searxng.bat" %TAG% || goto :failed

echo === Building My Morning News Kokoro text-to-speech image ===
call "%~dp0build-tts.bat" %TAG% || goto :failed

echo === Building My Morning News Ollama image ===
call "%~dp0build-ollama.bat" %TAG% || goto :failed

echo === Building My Morning News Prometheus image ===
call "%~dp0build-prometheus.bat" %TAG% || goto :failed

echo === Building My Morning News Tempo image ===
call "%~dp0build-tempo.bat" %TAG% || goto :failed

echo === Building My Morning News Loki image ===
call "%~dp0build-loki.bat" %TAG% || goto :failed

echo === Building My Morning News Grafana image ===
call "%~dp0build-grafana.bat" %TAG% || goto :failed

echo === All images built for %TAG% and latest, pushed to Docker Hub, and pulled locally ===
endlocal
exit /b 0

:failed
echo Build failed.
endlocal
exit /b 1
