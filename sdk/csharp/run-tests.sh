#!/usr/bin/env bash
# Run the My Morning News C# SDK harness against a live server.
# Usage: ./run-tests.sh [--base-url URL] [--email EMAIL] [--password PASSWORD] [--tenant ID] [--with-run] [--verbose]
# Environment: MYMORNINGNEWS_BASE_URL, MYMORNINGNEWS_EMAIL, MYMORNINGNEWS_PASSWORD, MYMORNINGNEWS_FRAMEWORK (default net10.0).
# (c) 2026 Lattice AI LLC
set -euo pipefail
cd "$(dirname "$0")"
FRAMEWORK="${MYMORNINGNEWS_FRAMEWORK:-net10.0}"
dotnet build MyMorningNews.Sdk.sln --nologo -v q
dotnet run --no-build --project MyMorningNews.Sdk.TestHarness -f "$FRAMEWORK" -- "$@"
