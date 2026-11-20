#!/usr/bin/env bash
# Run the My Morning News JavaScript SDK test harness against a live server.
# Arguments pass through, for example: ./run-tests.sh --base-url http://127.0.0.1:12100 --verbose
# (c) 2026 Lattice AI LLC
set -euo pipefail
cd "$(dirname "$0")"
node test-harness.js "$@"
