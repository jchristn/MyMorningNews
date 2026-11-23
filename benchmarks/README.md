# My Morning News benchmarks

The operator's guide to the benchmark suite: how to stand up the isolated stack, run each command, and read the
results. The plan, metric definitions, and hypotheses are in [`../BENCHMARKING.md`](../BENCHMARKING.md). Curated
numbers are in [`RESULTS.md`](RESULTS.md), and ranked fixes in [`../archive/IMPROVEMENTS.md`](../archive/IMPROVEMENTS.md) (archived).

(c) 2026 Lattice AI LLC

The harness is `src/Test.Benchmark` (assembly and process name `MyMorningNews.Benchmark`). It talks to the server only
over REST, like any client, and never reads the database. Everything it needs that would cost money or reach the
internet (the language model, embeddings, search, the pages search returns, and text to speech) is replaced by a
deterministic stub that the harness hosts on `127.0.0.1:12503` and that speaks the same protocols the real services
do. **The default and CI paths make no paid calls.**

## Quick start

```bash
# 1. The isolated stack: its own compose project (mymorningnews-bench), Postgres on 127.0.0.1:12502.
docker compose -f benchmarks/docker/compose.yaml up -d --wait

# 2. The server under test, built from the working tree, on 127.0.0.1:12500 (leave it running).
benchmarks/start-bench-server.sh            # Windows: benchmarks\start-bench-server.bat

# 3. The standard suite with a round label.
benchmarks/run-baseline.sh round1           # Windows: benchmarks\run-baseline.bat round1

# 4. The deterministic gate CI runs.
benchmarks/ci-gate.sh                       # Windows: benchmarks\ci-gate.bat

# Full reset of the bench stack (never touches the development stack).
docker compose -f benchmarks/docker/compose.yaml down -v
```

`start-bench-server` copies `docker/config/mymorningnews.bench.json` into `.run/server/` on every start, so the
server never reuses settings it saved, and it clears every `MYMORNINGNEWS_*` variable (and the common provider key
variables) from the environment first, so a developer's own Anthropic key or database override can never reach the
bench server. Every workspace the harness creates assigns all model tasks to the stub endpoint, checks that the
assignments took, and after every run checks that every recorded model call used an allowed endpoint; anything else
stops the command (`STOPPED: ...`, exit 1).

## Ports

The suite uses the 125xx block, bound to `127.0.0.1` only. The development stack uses 12100 to 12120, so the two never
collide.

| Port | What |
|---|---|
| 12500 | Server under test (run from the working tree, not in a container) |
| 12502 | Postgres (`pgvector/pgvector:0.8.1-pg17`, the same pin as `docker/compose.yaml`) |
| 12503 | Harness stub: model, embeddings, search, pages, robots.txt, speech |
| 12509 | Server Prometheus exporter, scraped by the harness for stage attribution |
| 12511 | Prometheus (compose profile `observability`, optional; scrapes 12509 every second) |

## Commands

```
dotnet run --project src/Test.Benchmark -c Release -- <command> [--option value ...]
```

| Command | What it does | Deterministic | In CI |
|---|---|---|---|
| `validate` | Checks dataset files against [`datasets/SCHEMA.md`](datasets/SCHEMA.md) and their own consistency; prints counts and the SHA-256. | yes | yes |
| `ingest` | Provisions a workspace per corpus and arm (tenant, reader, stub model, stub search provider, topic with dimensions and constraints), runs every round in order with feedback between rounds, and reports the load path: results, fetches, fetch failures, findings, duplicates, verified evidence, **reachability** (the share of relevant stories whose page was even fetched), run time, and queue wait. | outcomes yes, timings no | no |
| `quality` | Researches every corpus in every product arm (reusing intact workspaces) and scores every probe: ranking nDCG@10, surfaced and suppressed findings, duplicates, clusters, updates, blocked domains, seen pages, URL canonicalization, evidence verification, prompt injection, feedback order, empty briefings, scope guardrail, dedupe precision. Adds the two reference arms. | yes | yes |
| `e2e` | The whole flow, repeated in fresh workspaces: topic, run, briefing ready, run email sent with open, feedback, and unsubscribe links, audio made, a feedback link followed and recorded. Each check is a probe; each step's time is a metric, and the server's stage and model-call histograms are diffed. | checks yes, timings no | checks |
| `load` | API reads (topics, findings, runs, stages, briefings) closed loop at each `--concurrency`, open loop at each `--rate` with `--open-loop`, then `--runs` research runs queued at once (runs per minute, queue wait, submit to finish). Warmup first, in-flight requests drained at the deadline, throughput over successful requests and measured time only. | no | no |
| `resilience` | Injects one fault per scenario through the stub (model, embeddings, search, pages, speech; slow, down, rate limited once) plus a dry run before a real run, and checks the product retries, degrades, fails cleanly, and recovers. | outcomes yes | yes |
| `compare` | Paired diff of two reports of any kind, with a significance test; saves `<utc>-compare-<name>.md/.json`; exit 1 on regression and a final `RESULT:` line. | | yes |
| `history` | One table per dataset and arm: every probe type's score per round, the net change, probe counts, and published baselines; plus e2e, resilience, and ingest per round. Saves `<utc>-history.md`. | | no |
| `stub` | Runs the stub on its own for manual testing, with fault control at `POST /_stub/faults?target=Model&mode=Down`. | | no |

`prepare` (public datasets) and `agent` are not implemented: no public dataset fits this product's task (see
`BENCHMARKING.md`), and there is no agent-in-the-loop workflow to measure.

### What only makes sense in a paid run

The stub model judges relevance lexically (cosine of feature-hashed word vectors) and embeds with feature hashing, so
under stubs the suite measures the product's pipeline (filtering, canonicalization, seen-page memory, evidence
verification, dedupe thresholds and judge wiring, composite ranking, tiers, retries, degradation, delivery) given a
fixed, simple model. These numbers need a real model to mean anything, and are measured only by a paid run:

- how well a real model's relevance judgment ranks and suppresses (ranking, surfaced, suppressed, empty);
- whether a real model obeys injected instructions (injection), and how often it misquotes (the evidence precision of
  a real model's quotes; the stub scripts misquotes instead);
- duplicate and update detection with real embeddings (the dedupe thresholds were set for a real embedding model);
- token use and cost per run.

Paid runs never happen by default. They need `--paid`, a spend cap (`--max-spend-usd`, at most 50), and an API key in
the environment variable named by `--api-key-env` (default `MYMORNINGNEWS_BENCH_PAID_API_KEY`). The cap is enforced
twice: the harness assigns the reader a plan whose monthly cost cap is the remaining budget (the server then refuses
calls with `402 CapReached`), and the harness stops starting runs once the recorded cost reaches the cap (the report
is marked incomplete). Search, pages, embeddings, and speech stay on the stub. `load` and `resilience` refuse `--paid`.

```bash
MYMORNINGNEWS_BENCH_PAID_API_KEY=... dotnet run --project src/Test.Benchmark -c Release -- quality --paid --max-spend-usd 1.00 --arms default --label r2-paid
PAID=1 MAX_SPEND_USD=1.00 benchmarks/run-baseline.sh r2       # the same through the script
```

## Options

| Option | Default | Commands | Meaning |
|---|---|---|---|
| `--server` | `http://127.0.0.1:12500` | all that call the server | Server under test. |
| `--admin-key` | `MYMORNINGNEWS_BENCH_ADMIN_KEY`, else the bench settings key | same | Administration key (never written to reports). |
| `--metrics-url` | `http://127.0.0.1:12509/metrics` | ingest, quality, e2e, load | Prometheus exporter, or `none`. |
| `--stub-port` | 12503 | all that host the stub | Stub port (must match the bench settings' TTS and Ollama URLs). |
| `--stub-bind`, `--stub-host` | `127.0.0.1`, the bind address | stub | Listening address (`*` for every interface) and the host name in the URLs the stub serves (`host.docker.internal` for a Docker stack). |
| `--dataset` | `benchmarks/datasets/mymorningnews-synthetic.json` | validate (comma list), ingest, quality, e2e, load, resilience, stub | Dataset file. |
| `--arms` | `default,llm-only` | ingest, quality | Product arms. `--no-reference` drops the reference arms. |
| `--corpora` | all | ingest, quality | Only these corpus ids. |
| `--reingest` | off | ingest, quality | Ignore saved workspaces and research again. Pass it after changing any code on the research path: the workspace hash covers data and settings, not code. |
| `--label` | empty | all reports | Round or variant label, part of the file name. |
| `--output-dir` | `benchmarks/results` | all reports | Report directory (relative paths resolve from the repository root). |
| `--seed`, `--resamples` | 20261120, 2000 | all | Bootstrap and load seeds; bootstrap resamples. |
| `--run-timeout-seconds` | 300 | all that run topics | Per run. |
| `--max-connections`, `--http-timeout-seconds` | 256, 120 | all | The one pooled HTTP client (recorded in every report). |
| `--scrub` | off | all reports | Replace the machine name and repository path (used for committed baselines). |
| `--repeat`, `--corpus`, `--no-audio`, `--step-timeout-seconds` | 3, first corpus, off, 120 | e2e | Repeats, corpus, skip audio, per-step wait. |
| `--concurrency`, `--open-loop`, `--rate`, `--warmup-seconds`, `--duration-seconds`, `--runs`, `--model-latency-ms` | `1,4,16`, off, `25,100`, 5, 15, 6, 0 | load | Closed-loop levels, open-loop rates, timing, concurrent runs, added stub latency per model call. |
| `--scenarios`, `--corpus` | all, first corpus | resilience | Only these scenarios. |
| `--baseline`, `--candidate`, `--tolerance`, `--alpha`, `--latency-tolerance`, `--deterministic`, `--allow-partial`, `--name` | required, required, 0.01, 0.05, off, off, off, from the candidate | compare | See "Comparing". |
| `--rounds`, `--dir`, `--arms` | required, `benchmarks/results`, all | history | `R0=round0,R1=round1`; labels or UTC timestamp prefixes. |
| `--paid`, `--max-spend-usd`, `--api-key-env`, `--paid-provider`, `--paid-model`, `--paid-endpoint`, `--paid-reasoning-effort`, `--paid-input-cost-per-mtok`, `--paid-output-cost-per-mtok` | off, required with `--paid`, `MYMORNINGNEWS_BENCH_PAID_API_KEY`, Anthropic, claude-opus-5-5, provider default, Low, 5, 25 | quality, ingest, e2e | Paid runs; see above. |

## Examples

```bash
B="dotnet run --project src/Test.Benchmark -c Release --"
$B validate --dataset benchmarks/datasets/mymorningnews-synthetic.json,benchmarks/datasets/mymorningnews-live.json
$B ingest --label r2 --reingest
$B quality --label r2                                   # reuses the workspaces ingest made
$B quality --dataset benchmarks/datasets/mymorningnews-live.json --label r2
$B quality --arms llm-only --corpora heat-pumps --label r2-llm-only   # ablation: model judgment only
$B e2e --repeat 5 --label r2
$B resilience --scenarios search-429-once,model-down --label r2
$B load --concurrency 1,8,32 --open-loop --rate 50,200 --duration-seconds 30 --label r2
$B load --runs 12 --model-latency-ms 800 --label r2-slow-model   # run throughput with a model that takes time
$B compare --baseline benchmarks/results/<r1>-quality-mymorningnews-synthetic-r1.json --candidate benchmarks/results/<r2>-quality-mymorningnews-synthetic-r2.json
$B compare --baseline <a>-load-...json --candidate <b>-load-...json --latency-tolerance 0.2
$B history --rounds R0=round0,R1=round1,R2=r2
$B stub    # then point a server at the stub by hand (see "Using the stub by hand" below)
```

### Using the stub by hand

`$B stub` serves every stand-in on one port and prints the exact URLs for the dataset it loaded:

| Stand-in | URL (default port 12503) | Configure it in the dashboard as |
|---|---|---|
| Chat | `http://127.0.0.1:12503/v1`, model `bench-stub-chat` | Models: an OpenAI-compatible endpoint with that base URL and model, no key, then assign every task to it |
| Embeddings | same `/v1`, model `bench-hash-384` (384 dimensions) | the same endpoint with embeddings on, or a second one |
| Search | `http://127.0.0.1:12503/search/<corpus>/r<round>`, for example `/search/heat-pumps/r1` | Search providers: a SearXNG provider with that endpoint |
| Pages | `http://127.0.0.1:12503/corpus/<corpus>/<item>.html` | nothing: search results link to them |
| Speech | `http://127.0.0.1:12503/v1/audio/speech` | Voices: an OpenAI-compatible speech voice with base URL `.../v1` |
| Faults and state | `POST /_stub/faults?target=Model&mode=Down`, `GET /_stub/state` | |

A server started from source on the same machine reaches those `127.0.0.1` URLs as they are. A server in Docker (the
documented stack) cannot: inside the container `127.0.0.1` is the container itself, and by default the stub answers
only requests addressed to `127.0.0.1`. Start it with two options instead:

```bash
$B stub --stub-bind '*' --stub-host host.docker.internal
```

`--stub-bind` is the address to listen on (default `127.0.0.1`; `*` is every interface and any host name), and
`--stub-host` is the host name the stub writes into the URLs it serves, so search results link to pages the container
can fetch. Then use `http://host.docker.internal:12503/v1` and `http://host.docker.internal:12503/search/<corpus>/r<round>`
in the dashboard. The stack's server already maps `host.docker.internal` to the host (`extra_hosts` in
`docker/compose.yaml`). Listening on every interface lets anything that can reach this machine use the stub, so do it
only on a network you trust, and stop the stub when you are done.

Sweeps: arms are per-topic ranking weights, so a new weighting is a new `ProductArm` in `Runners/ProductArm.cs`; each
arm gets its own workspace automatically (the arm's settings are part of the workspace hash).

## Datasets

| File | Tier | Size | Notes |
|---|---|---|---|
| `datasets/mymorningnews-synthetic.json` | synthetic | 3 corpora, 5 rounds, 47 pages, 65 probes (20 negative) | Fictional Harbor City news. Built by `datasets/synthetic/build.py` from the `.md` and `.json` sources beside it (standard library only; `--check` verifies the committed file is current, which the gate does). |
| `datasets/mymorningnews-live.json` | live | 1 corpus, 24 pages, 23 probes (14 negative) | A frozen 2026-11-20 snapshot of `docs/HOSTING.md`, `docs/I18N.md`, and `docs/MOBILE_DEVICE_TESTING.md`, one page per section, built by `datasets/live/build.py` from `datasets/live/manifest.json`. Labels are hand-assigned by section: good but not gold. Rebuild only to take a new snapshot. |

No public dataset is used and none is downloaded; `BENCHMARKING.md` explains why none fits. Local corpora (files kept
outside the repository) do not apply: the product researches web pages, it does not ingest uploads, so
`datasets/local/` holds only a note. There is no LLM judge, so `labels/` holds only a note.

The synthetic corpus covers: relevant stories at three grades; look-alike distractors that share words with the topic
(a basketball team named the Heat, a heat wave, a pumpkin festival, a Quantum Cafe); a blocked domain; a URL with
tracking parameters; a syndicated copy in the same round; a reworded repeat and a verbatim copy in the next round;
updates with a new amount or date; a page seen in an earlier round; scripted misquotes (typography changes that must
verify; invented, altered, and one-word quotes that must not); an injected instruction; feedback between rounds; and a
corpus whose correct briefing is empty.

## Reading the results

Reports go to `benchmarks/results/<yyyyMMdd-HHmmss>-<kind>-<dataset>[-<label>].json` and `.md` (git-ignored). The
Markdown opens with provenance (product commit with `-dirty` for any change including untracked files, harness
commit, machine, image digests, the models and services used, the dataset hash, the full command line), then the
summary, published baselines, the stage breakdown, latency, and the misses with what happened. The JSON has every
per-probe outcome.

- **Which numbers matter.** For quality: `type:ranking ndcg10`, the `negative` group (the suite's honesty check: a
  system that always shows something fails it), and the dedupe and evidence types. For resilience and e2e: every probe
  should be 1. For load: p95 and p99 against throughput, and where they part.
- **Arms.** `default` is the product as shipped, `llm-only` the same with the model judgment as the only ranking
  component, `floor-search-order` everything search returned with no product, and `reference-lexical` a plain pipeline
  with the same constraints and the same stub judgment. The product should beat the floor everywhere; where it does not
  beat the reference, the product's extra machinery is not paying for itself under this model.
- **Indicative.** A group with fewer than 20 probes is marked indicative: the interval is wide, so read it as a
  direction, not a measurement.
- **Low reachability** means relevant pages were never fetched (search did not return them, the fetch limit cut them,
  or they were skipped as seen), so no ranking change can find them.
- **A high error rate** in load means the throughput number is about failures; fix those first.
- **Incomplete runs** start with a bold banner and exit 3. Their numbers are not comparable.
- **Same machine only.** Latency and throughput are properties of the hardware; compare them only between runs on the
  same machine, and never while another benchmark or a heavy build is running.

## Comparing

`compare` pairs probes by arm, corpus, and probe id. A metric regresses when its mean drops by more than `--tolerance`
(0.01) and a paired bootstrap says the drop is real (the 95% interval is below zero or p is below `--alpha`, 0.05).
`--deterministic` (the CI gate) instead counts any single probe that gets worse as a regression: under stubs the runs
are fully deterministic, so there is no noise to forgive, and on a small probe set the significance test would miss a
real break (round 0's resilience failures are p = 0.053 under the default rule). Latency (p95) and throughput are
compared only with `--latency-tolerance`. Mismatched probe sets, a changed dataset file, and incomplete reports fail
unless `--allow-partial`.

## CI gate

`ci-gate.sh` (and `.bat`) runs only deterministic, stub-backed checks: the synthetic build check, `validate`, `quality`
on both datasets in every arm with fresh workspaces, `resilience`, and `e2e --repeat 1`, each compared with its
committed baseline in `baselines/` in deterministic mode. It deletes the outputs of earlier gate runs, writes to
`.run/ci/`, and exits 0 (pass), 1 (regression or failure), or 3 (incomplete). `ci-gate.sh --update` writes new
baselines; a baseline update is a reviewed change with the reason in the commit message.

The gate runs in `.github/workflows/tests.yml`, job **Benchmark gate**, on every push to every branch and every pull
request. It needs Docker for the bench stack, so it runs on `ubuntu-latest` only. Nothing is excluded from CI except
what is not deterministic: `load`, the timings inside `e2e` and `ingest`, and paid runs. Those run by hand on the
maintainer's machine (`run-baseline.sh`), at least once per round.

## Layout

```
benchmarks/
  README.md, RESULTS.md, .gitignore
  run-baseline.sh/.bat, start-bench-server.sh/.bat, ci-gate.sh/.bat
  docker/compose.yaml, docker/config/      isolated stack, bench server settings, Prometheus scrape config
  datasets/SCHEMA.md                        dataset format
  datasets/mymorningnews-synthetic.json     built synthetic corpus (sources and build.py in datasets/synthetic/)
  datasets/mymorningnews-live.json          frozen live snapshot (manifest and build.py in datasets/live/)
  datasets/local/, labels/                  notes (not used by this product)
  baselines/                                committed gate baselines
  baselines.json                            published results per dataset (none exist; each entry says why)
  results/                                  git-ignored reports
  .run/                                     git-ignored server directory, logs, workspaces, gate output
```
