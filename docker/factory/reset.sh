#!/usr/bin/env bash
# DESTRUCTIVE: stops the stack and deletes all data volumes, then starts fresh.
set -e
cd "$(dirname "$0")/.."
read -r -p "This deletes all My Morning News data. Type RESET to continue: " CONFIRM
if [ "$CONFIRM" != "RESET" ]; then
  echo "Aborted."
  exit 1
fi
docker compose down -v
cp factory/mymorningnews.json server/mymorningnews.json
docker compose up -d --build
docker ps -a
