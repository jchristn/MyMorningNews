#!/usr/bin/env bash
# The standard suite: ingest and quality on every committed dataset, e2e on a sample, resilience, and load, all
# stub-backed. The bench stack and server must be running (benchmarks/README.md).
#
#   benchmarks/run-baseline.sh <label> [extra options forwarded to every command]
#
# Opt-in switches (never on by default):
#   REINGEST=1                         research every workspace again (pass after changing code on the research path)
#   PAID=1 MAX_SPEND_USD=1.00          also run quality and e2e against a real model; needs MYMORNINGNEWS_BENCH_PAID_API_KEY
#                                      (or --api-key-env) and stops at the spend cap. This spends money.
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
LABEL="${1:-}"
if [ -z "$LABEL" ]; then
  echo "usage: benchmarks/run-baseline.sh <label> [options]"
  exit 2
fi
shift

dotnet build "$ROOT/src/Test.Benchmark/Test.Benchmark.csproj" -c Release -nologo -v quiet || exit 1
BENCH="$ROOT/src/Test.Benchmark/bin/Release/net10.0/MyMorningNews.Benchmark"
[ -f "$BENCH" ] || BENCH="dotnet $ROOT/src/Test.Benchmark/bin/Release/net10.0/MyMorningNews.Benchmark.dll"
REINGEST_FLAG=""
[ "${REINGEST:-0}" = "1" ] && REINGEST_FLAG="--reingest"
STATUS=0

run() {
  echo
  echo "== $*"
  $BENCH "$@"
  CODE=$?
  if [ "$CODE" -ne 0 ]; then
    echo "exit $CODE: $*"
    STATUS=1
  fi
}

for DATASET in benchmarks/datasets/mymorningnews-synthetic.json benchmarks/datasets/mymorningnews-live.json; do
  run validate --dataset "$DATASET"
  run ingest --dataset "$DATASET" --label "$LABEL" $REINGEST_FLAG ${1+"$@"}
  run quality --dataset "$DATASET" --label "$LABEL" ${1+"$@"}
done
run e2e --label "$LABEL" --repeat 3 ${1+"$@"}
run resilience --label "$LABEL" ${1+"$@"}
run load --label "$LABEL" --open-loop ${1+"$@"}

if [ "${PAID:-0}" = "1" ]; then
  if [ -z "${MAX_SPEND_USD:-}" ]; then
    echo "PAID=1 needs MAX_SPEND_USD (the spend cap for each paid command)."
    exit 2
  fi
  run quality --dataset benchmarks/datasets/mymorningnews-synthetic.json --label "$LABEL-paid" --paid --max-spend-usd "$MAX_SPEND_USD" --arms default ${1+"$@"}
  run e2e --label "$LABEL-paid" --repeat 1 --paid --max-spend-usd "$MAX_SPEND_USD" ${1+"$@"}
fi

exit $STATUS
