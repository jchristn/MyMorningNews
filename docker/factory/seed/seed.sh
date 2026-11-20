#!/bin/sh
# My Morning News factory demo seeder. (c) 2026 Lattice AI LLC
# Runs from the curlimages/curl containers in factory/compose.factory.yaml (no jq). Best-effort and idempotent: every
# call is guarded, nothing here is fatal, and running it again changes nothing.
#
# The server seeds the default tenant, the administrator (admin@mymorningnews / password), built-in roles, model
# endpoints, templates, and prompts on first boot. The demo is layered on top in three steps, each a one-shot container:
#   1. factory-seed        seed.sh accounts  creates the demo member demo@mymorningnews / password through the API
#                                            (the server also gives the new user its Morning News edition).
#   2. factory-seed-data   psql demo.sql     inserts topics, runs, findings, evidence, predictions, suggestions,
#                                            briefings, and feedback for the demo user. No model calls, no API key.
#   3. factory-seed-files  seed.sh files     asks the server to store each demo briefing's Markdown and HTML files.

set -u
PHASE="${1:-accounts}"
BASE="${MYMORNINGNEWS_REST_BASE:-http://server:8000}"
EMAIL="${MYMORNINGNEWS_ADMIN_EMAIL:-admin@mymorningnews}"
PASSWORD="${MYMORNINGNEWS_ADMIN_PASSWORD:-password}"
TENANT="${MYMORNINGNEWS_ADMIN_TENANT:-default}"
DEMO_EMAIL="${MYMORNINGNEWS_DEMO_EMAIL:-demo@mymorningnews}"
DEMO_PASSWORD="${MYMORNINGNEWS_DEMO_PASSWORD:-password}"
DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="/tmp/seed-out.json"

json_field() {
  printf '%s' "$1" | grep -o "\"$2\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" | head -n1 | sed 's/.*:[[:space:]]*"\(.*\)"/\1/'
}

# login <email> <password>: print a bearer token, or nothing.
login() {
  RESP=$(curl -fsS -X POST "${BASE}/v1.0/token" -H "x-email: $1" -H "x-password: $2" -H "x-tenant-guid: ${TENANT}" 2>/dev/null || true)
  json_field "${RESP}" token
}

echo "[seed] My Morning News factory seeder (${PHASE}) against ${BASE}"
i=0
until curl -fsS "${BASE}/v1.0/api/health" >/dev/null 2>&1; do
  i=$((i + 1))
  if [ "$i" -ge 60 ]; then echo "[seed] server not healthy after 60 attempts; giving up"; exit 0; fi
  sleep 2
done
echo "[seed] server is healthy"

case "${PHASE}" in
  accounts)
    TOKEN=$(login "${EMAIL}" "${PASSWORD}")
    if [ -z "${TOKEN}" ]; then
      echo "[seed] could not sign in as ${EMAIL}; was the default password changed? Nothing seeded."
      exit 0
    fi

    # The demo member account, to see the product as a non-administrator. 409 means it already exists.
    CODE=$(curl -s -o "${OUT}" -w '%{http_code}' -X POST "${BASE}/v1.0/tenants/${TENANT}/users" -H "Authorization: Bearer ${TOKEN}" \
      -H 'content-type: application/json' --data-binary "@${DIR}/demo-user.json")
    case "${CODE}" in
      200|201) echo "[seed] created demo user ${DEMO_EMAIL}" ;;
      409) echo "[seed] demo user ${DEMO_EMAIL} already exists" ;;
      *) echo "[seed] demo user returned ${CODE}: $(head -c 200 "${OUT}")" ;;
    esac
    ;;

  files)
    TOKEN=$(login "${DEMO_EMAIL}" "${DEMO_PASSWORD}")
    if [ -z "${TOKEN}" ]; then
      echo "[seed] could not sign in as ${DEMO_EMAIL}; skipping briefing files."
      exit 0
    fi

    # The demo briefings have fixed IDs (see demo.sql). Store their files only when they have none yet.
    for n in 1 2 3 4; do
      ID="brf_mmndemo0_000000000000000000${n}"
      CODE=$(curl -s -o "${OUT}" -w '%{http_code}' "${BASE}/v1.0/api/briefings/${ID}" -H "Authorization: Bearer ${TOKEN}")
      if [ "${CODE}" != "200" ]; then echo "[seed] briefing ${ID} not found (${CODE}); skipped"; continue; fi
      if ! grep -q '"filesExpired"[[:space:]]*:[[:space:]]*true' "${OUT}"; then echo "[seed] briefing ${ID} already has files"; continue; fi
      CODE=$(curl -s -o "${OUT}" -w '%{http_code}' -X POST "${BASE}/v1.0/api/briefings/${ID}/regenerate" -H "Authorization: Bearer ${TOKEN}")
      case "${CODE}" in
        200|201) echo "[seed] stored Markdown and HTML for briefing ${ID}" ;;
        *) echo "[seed] briefing ${ID} files returned ${CODE}: $(head -c 200 "${OUT}")" ;;
      esac
    done

    echo "[seed] done. Sign in at http://127.0.0.1:12101 as ${DEMO_EMAIL} / ${DEMO_PASSWORD} (tenant ${TENANT}), or ${EMAIL} / ${PASSWORD}."
    ;;

  *)
    echo "[seed] unknown phase ${PHASE}; use accounts or files"
    ;;
esac
exit 0
