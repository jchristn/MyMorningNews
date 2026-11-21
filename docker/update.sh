#!/usr/bin/env bash
# Update the stack: rebuild the My Morning News images from this working tree, pull newer versions of the published
# images it uses (pgvector, Grafana), and recreate any container whose image or settings changed. Named volumes (data,
# media, email, metrics) are preserved. Run it after `git pull`.
#
# Optional profiles: set COMPOSE_PROFILES first to update them too, for example COMPOSE_PROFILES=mcp,tts ./update.sh
# (c) 2026 Lattice AI LLC
set -euo pipefail
cd "$(dirname "$0")"

echo "[1/2] Pulling published images (locally built images are skipped)..."
docker compose pull --ignore-buildable

echo "[2/2] Rebuilding local images and recreating changed containers..."
docker compose up -d --build

docker ps -a --filter name=mymorningnews
