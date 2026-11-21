#!/usr/bin/env bash
# reset.sh: reset My Morning News to factory defaults.
#
# DESTRUCTIVE. Stops the stack and deletes every Docker volume of its compose project (PostgreSQL, media with reports and
# audio, Prometheus, Tempo, Loki, Grafana), restores docker/server/mymorningnews.json from factory/mymorningnews.json, and
# starts fresh. The project is "mymorningnews" unless COMPOSE_PROJECT_NAME is set; other projects are never touched.
#
# Usage: factory/reset.sh [--demo] [--keep-ollama] [--yes]
#   --demo         start with the factory demo overlay (factory/compose.factory.yaml): a demo user (demo@mymorningnews /
#                  password) with topics, completed runs, findings, evidence, predictions, suggestions, briefings, and
#                  feedback, seeded without model calls or an API key
#   --keep-ollama  keep the Ollama model volume (only relevant with --profile ollama) so models are not downloaded again
#   --yes          skip the confirmation prompt (for scripted resets)
set -euo pipefail

DEMO=0; KEEP_OLLAMA=0; YES=0
for arg in "$@"; do
  case "$arg" in
    --demo) DEMO=1 ;;
    --keep-ollama) KEEP_OLLAMA=1 ;;
    --yes) YES=1 ;;
    *) echo "Unknown option: $arg"; exit 2 ;;
  esac
done

DOCKER_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$DOCKER_DIR"

FILES=(-f compose.yaml -f factory/compose.factory.yaml)
# The compose project this reset applies to: "mymorningnews" from compose.yaml, or COMPOSE_PROJECT_NAME when set.
# Only that project's containers and volumes are touched.
PROJECT="$(docker compose "${FILES[@]}" config 2>/dev/null | sed -n 's/^name: *//p' | head -n 1 | tr -d "\"'")"
if [ -z "$PROJECT" ]; then
  echo "Could not read the compose project name (docker compose config failed). Nothing was changed."
  exit 1
fi

echo
echo "=========================================================="
echo "  My Morning News: reset to factory defaults"
echo "=========================================================="
echo "Compose project: $PROJECT"
echo "WARNING: this deletes all data in this project: users, topics, briefings, audio, settings changes, and metrics."
if [ "$YES" != "1" ]; then
  read -r -p "Type RESET to continue: " CONFIRM
  if [ "$CONFIRM" != "RESET" ]; then echo "Aborted. Nothing was changed."; exit 1; fi
fi

echo "[1/3] Stopping containers and removing volumes..."
if [ "$KEEP_OLLAMA" = "1" ]; then
  docker compose "${FILES[@]}" --profile tts --profile mcp --profile ollama down --remove-orphans
  # Select volumes by the labels compose puts on them, never by name, so another project's volumes are never touched.
  OLLAMA_VOLUMES="$(docker volume ls -q --filter "label=com.docker.compose.project=$PROJECT" --filter "label=com.docker.compose.volume=ollama")"
  for v in $(docker volume ls -q --filter "label=com.docker.compose.project=$PROJECT"); do
    if printf '%s\n' "$OLLAMA_VOLUMES" | grep -qx "$v"; then continue; fi
    docker volume rm "$v" >/dev/null
  done
else
  docker compose "${FILES[@]}" --profile tts --profile mcp --profile ollama down -v --remove-orphans
fi

echo "[2/3] Restoring factory settings..."
cp factory/mymorningnews.json server/mymorningnews.json

echo "[3/3] Starting..."
if [ "$DEMO" = "1" ]; then
  docker compose "${FILES[@]}" pull --ignore-pull-failures && docker compose "${FILES[@]}" up -d
else
  docker compose pull && docker compose up -d
fi
docker ps -a --filter name=mymorningnews
echo
echo "Done. Open http://127.0.0.1:12101 and sign in as admin@mymorningnews / password (tenant default)."
if [ "$DEMO" = "1" ]; then
  echo "Demo data: sign in as demo@mymorningnews / password. Seeder logs: docker logs mymorningnews-factory-seed-data"
fi
