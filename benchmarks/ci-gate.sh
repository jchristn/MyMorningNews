#!/usr/bin/env bash
# Deterministic regression gate. Runs only stub-backed checks whose results contain no model judgment from a real
# model, no judge, and no wall-clock latency: the dataset checks, quality on both committed datasets in every arm
# (fresh workspaces), the resilience outcomes, and the end-to-end checks. Each report is compared with its committed
# baseline in benchmarks/baselines/ in deterministic mode, where any single probe that gets worse is a regression.
# Exits 0 when nothing regressed, 1 on a regression or failure, 3 when a run was incomplete.
# Needs the bench stack and server running (benchmarks/README.md). Makes no paid calls.
#
#   benchmarks/ci-gate.sh            gate
#   benchmarks/ci-gate.sh --update   write new baselines (a reviewed change; say why in the commit message)
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
UPDATE=0
[ "${1:-}" = "--update" ] && UPDATE=1
OUT="$HERE/.run/ci"
mkdir -p "$OUT" "$HERE/baselines"
rm -f "$OUT"/*.json "$OUT"/*.md

dotnet build "$ROOT/src/Test.Benchmark/Test.Benchmark.csproj" -c Release -nologo -v quiet || exit 1
BENCH="dotnet $ROOT/src/Test.Benchmark/bin/Release/net10.0/MyMorningNews.Benchmark.dll"
STATUS=0

python3 "$HERE/datasets/synthetic/build.py" --check || STATUS=1
$BENCH validate --dataset benchmarks/datasets/mymorningnews-synthetic.json,benchmarks/datasets/mymorningnews-live.json || STATUS=1

gate() {
  KIND="$1"
  NAME="$2"
  shift 2
  echo
  echo "== gate $KIND $NAME"
  $BENCH "$KIND" --label ci --output-dir "$OUT" --scrub "$@"
  CODE=$?
  if [ "$CODE" -eq 3 ]; then STATUS=3; return; fi
  if [ "$CODE" -ne 0 ]; then [ "$STATUS" -eq 0 ] && STATUS=1; return; fi
  CANDIDATE="$(ls -t "$OUT"/*-"$KIND"-"$NAME"-ci.json 2>/dev/null | grep -v -- '-compare-' | head -1)"
  BASELINE="$HERE/baselines/$KIND-$NAME.json"
  if [ -z "$CANDIDATE" ]; then echo "no report written for $KIND $NAME"; STATUS=1; return; fi
  if [ "$UPDATE" = "1" ] || [ ! -f "$BASELINE" ]; then
    cp "$CANDIDATE" "$BASELINE"
    echo "baseline written: $BASELINE"
    return
  fi
  $BENCH compare --baseline "$BASELINE" --candidate "$CANDIDATE" --deterministic --output-dir "$OUT" --name "$KIND-$NAME-ci" || { [ "$STATUS" -eq 0 ] && STATUS=1; }
}

gate quality mymorningnews-synthetic --dataset benchmarks/datasets/mymorningnews-synthetic.json --reingest
gate quality mymorningnews-live --dataset benchmarks/datasets/mymorningnews-live.json --reingest
gate resilience mymorningnews-synthetic
gate e2e mymorningnews-synthetic --repeat 1

echo
if [ "$STATUS" -eq 0 ]; then echo "GATE: pass"; else echo "GATE: fail ($STATUS)"; fi
exit $STATUS
