#!/usr/bin/env bash
# Build the server from the working tree in Release and run it against the isolated bench stack
# (benchmarks/docker/compose.yaml). The committed bench settings are copied into benchmarks/.run/server on every start,
# so the server never reuses settings it saved earlier. Every MYMORNINGNEWS_* variable from the caller's shell is
# cleared first, so a developer's own keys and overrides (an Anthropic key, a database host) can never reach the bench
# server. Runs in the foreground; stop it with Ctrl+C.
#
#   docker compose -f benchmarks/docker/compose.yaml up -d --wait
#   benchmarks/start-bench-server.sh
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
RUN="$HERE/.run/server"
FRAMEWORK="${FRAMEWORK:-net10.0}"

mkdir -p "$RUN"
dotnet build "$ROOT/src/MyMorningNews.Server/MyMorningNews.Server.csproj" -c Release -f "$FRAMEWORK" -nologo -v quiet
cp "$HERE/docker/config/mymorningnews.bench.json" "$RUN/mymorningnews.json"

for name in $(env | sed -n 's/^\(MYMORNINGNEWS_[A-Za-z0-9_]*\)=.*/\1/p'); do
  unset "$name"
done
unset ANTHROPIC_API_KEY OPENAI_API_KEY GEMINI_API_KEY || true

export MYMORNINGNEWS_SETTINGS_FILE="$RUN/mymorningnews.json"
cd "$RUN"
exec dotnet "$ROOT/src/MyMorningNews.Server/bin/Release/$FRAMEWORK/MyMorningNews.Server.dll"
