#!/usr/bin/env bash
# Update the stack to the latest published images (jchristn77/mymorningnews-<type>:latest, built with the repository's
# build-*.sh scripts) and restart it. Named volumes (data, media, email, metrics) are preserved.
#
# Optional profiles: set COMPOSE_PROFILES first to update them too, for example COMPOSE_PROFILES=mcp ./update.sh
# (c) 2026 Lattice AI LLC
set -euo pipefail
cd "$(dirname "$0")"
docker compose pull && docker compose down && docker compose up -d && docker ps -a
