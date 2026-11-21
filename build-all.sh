#!/usr/bin/env bash
# Build every My Morning News image on the Docker cloud builder for amd64 and arm64, push each to Docker Hub as
# jchristn77/mymorningnews-<type>:latest and :<tag>, and pull them back. (c) 2026 Lattice AI LLC.
set -euo pipefail

if [ -z "${1:-}" ]; then
    echo "Usage: build-all.sh <tag>"
    echo "Example: build-all.sh v0.1.0"
    exit 1
fi

TAG="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== Building My Morning News server (REST API, pipeline, workers) image ==="
"${SCRIPT_DIR}/build-server.sh" "$TAG"

echo "=== Building My Morning News dashboard image ==="
"${SCRIPT_DIR}/build-dashboard.sh" "$TAG"

echo "=== Building My Morning News MCP server image ==="
"${SCRIPT_DIR}/build-mcp.sh" "$TAG"

echo "=== Building My Morning News SearXNG image ==="
"${SCRIPT_DIR}/build-searxng.sh" "$TAG"

echo "=== Building My Morning News Kokoro text-to-speech image ==="
"${SCRIPT_DIR}/build-tts.sh" "$TAG"

echo "=== Building My Morning News Ollama image ==="
"${SCRIPT_DIR}/build-ollama.sh" "$TAG"

echo "=== Building My Morning News Prometheus image ==="
"${SCRIPT_DIR}/build-prometheus.sh" "$TAG"

echo "=== Building My Morning News Tempo image ==="
"${SCRIPT_DIR}/build-tempo.sh" "$TAG"

echo "=== Building My Morning News Loki image ==="
"${SCRIPT_DIR}/build-loki.sh" "$TAG"

echo "=== Building My Morning News Grafana image ==="
"${SCRIPT_DIR}/build-grafana.sh" "$TAG"

echo "=== All images built for ${TAG} and latest, pushed to Docker Hub, and pulled locally ==="
