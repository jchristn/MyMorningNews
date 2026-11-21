#!/usr/bin/env bash
# Update the deployment to the latest published images and restart it. (c) 2026 Lattice AI LLC.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
docker compose pull && docker compose down && docker compose up -d && docker ps -a
