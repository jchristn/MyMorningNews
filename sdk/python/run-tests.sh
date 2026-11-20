#!/usr/bin/env bash
# Runs the My Morning News Python SDK test harness against a live server.
# Creates sdk/python/.venv on first use, installs requirements, and passes
# every argument through to test_harness.py (for example --with-run or --verbose).
# (c) 2026 Lattice AI LLC
set -euo pipefail

cd "$(dirname "$0")"

PYTHON="${PYTHON:-python3}"
if [ ! -x ".venv/bin/python" ]; then
  echo "Creating virtual environment in .venv"
  "$PYTHON" -m venv .venv
fi

.venv/bin/python -m pip install --quiet --disable-pip-version-check -r requirements.txt
exec .venv/bin/python test_harness.py "$@"
