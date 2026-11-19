#!/usr/bin/env bash
# Pull the latest images and recreate the stack. Named volumes are preserved.
set -e
cd "$(dirname "$0")"
docker compose pull
docker compose down
docker compose up -d
docker ps -a
