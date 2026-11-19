#!/bin/sh
# My Morning News factory demo seeder. Runs once from the factory-seed container (curlimages/curl, no jq).
# Best-effort and idempotent: every call is guarded, and a route that a phase has not built yet is skipped.
#
# The server already seeds the default tenant, the administrator (admin@mymorningnews / password), built-in roles,
# model endpoints, and prompts on first boot. This script layers demo content on top. Each later phase adds its
# demo data here (topics, editions, briefings, feedback), driven by the JSON files in this folder.

set -u
BASE="${MYMORNINGNEWS_REST_BASE:-http://server:8000}"
EMAIL="${MYMORNINGNEWS_ADMIN_EMAIL:-admin@mymorningnews}"
PASSWORD="${MYMORNINGNEWS_ADMIN_PASSWORD:-password}"
TENANT="${MYMORNINGNEWS_ADMIN_TENANT:-default}"
DIR="$(cd "$(dirname "$0")" && pwd)"

json_field() {
  printf '%s' "$1" | grep -o "\"$2\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" | head -n1 | sed 's/.*:[[:space:]]*"\(.*\)"/\1/'
}

echo "[seed] My Morning News factory seeder against ${BASE}"
i=0
until curl -fsS "${BASE}/v1.0/api/health" >/dev/null 2>&1; do
  i=$((i + 1))
  if [ "$i" -ge 60 ]; then echo "[seed] server not healthy after 60 attempts; giving up"; exit 0; fi
  sleep 2
done
echo "[seed] server is healthy"

LOGIN=$(curl -fsS -X POST "${BASE}/v1.0/token" -H "x-email: ${EMAIL}" -H "x-password: ${PASSWORD}" -H "x-tenant-guid: ${TENANT}" 2>/dev/null || true)
TOKEN=$(json_field "${LOGIN}" token)
if [ -z "${TOKEN}" ]; then
  echo "[seed] could not sign in as ${EMAIL}; was the default password changed? Nothing seeded."
  exit 0
fi
AUTH="Authorization: Bearer ${TOKEN}"

# post_json <path> <file> <label>: POST a JSON file, treating 404 (route not built yet) and 409 (already exists) as skips.
post_json() {
  CODE=$(curl -s -o /tmp/seed-out.json -w '%{http_code}' -X POST "${BASE}$1" -H "${AUTH}" -H 'content-type: application/json' --data-binary "@$2")
  case "${CODE}" in
    200|201) echo "[seed] created $3" ;;
    404) echo "[seed] skipped $3 (not available on this server version yet)" ;;
    409) echo "[seed] $3 already exists" ;;
    *) echo "[seed] $3 returned ${CODE}: $(head -c 200 /tmp/seed-out.json)" ;;
  esac
}

# 1) A demo member account, to see the product as a non-administrator: demo@mymorningnews / password.
post_json "/v1.0/tenants/${TENANT}/users" "${DIR}/demo-user.json" "demo user demo@mymorningnews"

# 2) Demo topics (Phase 4 and later). Each file in topics/ is one topic.
if [ -d "${DIR}/topics" ]; then
  for f in "${DIR}"/topics/*.json; do
    [ -f "$f" ] || continue
    post_json "/v1.0/api/topics" "$f" "topic $(basename "$f" .json)"
  done
fi

echo "[seed] done. Sign in at http://127.0.0.1:12101 as ${EMAIL} / ${PASSWORD} (tenant ${TENANT}), or demo@mymorningnews / password."
