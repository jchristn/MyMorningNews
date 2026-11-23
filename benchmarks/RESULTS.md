# Benchmark results

The curated record of the benchmark suite, round by round. Plan and definitions: [`../BENCHMARKING.md`](../BENCHMARKING.md).
How to run and read: [`README.md`](README.md). Ranked fixes: [`../archive/IMPROVEMENTS.md`](../archive/IMPROVEMENTS.md) (archived).

(c) 2026 Lattice AI LLC

## Where the product stands

Round 0 was a bug hunt, as expected, and it found three defects, all in how a run recovers from trouble. A single rate
limit from the search provider failed the whole run, because search requests were never retried. And the product
remembered every page a run fetched as "seen", even when that run then failed or was only a dry run, so the next
real run silently skipped those pages and their findings were lost for good: after a model outage, the recovery run
showed 1 finding where a healthy run shows 11. All three are fixed, each with a regression test, and round 1 passes
every resilience scenario.

On quality, under the deterministic stub model, the pipeline does its mechanical work correctly: blocked domains are
never fetched, tracking-parameter URLs are fetched once, seen pages are skipped, duplicates and syndicated copies are
caught, every scripted misquote is rejected and every genuine quote accepted, the scope guardrail leads every model
request, and every end-to-end check passes (briefing, email with open, feedback, and unsubscribe links, audio, and
feedback recorded from an email link). The product beats the no-product floor by a wide margin everywhere (synthetic
overall 0.855 against 0.537). Two design-level weaknesses show up, and both are candidates in `archive/IMPROVEMENTS.md`: the
composite score's neutral components lift weak model judgments over the display threshold (the empty-briefing probe
fails, and an item the model scored 24 out of 100 is shown), and a reworded update with a new date is shown as a new,
unlinked finding because updates are only looked for among near duplicates.

These quality numbers measure the product's pipeline under a simple lexical stand-in for the model, not a real model's
judgment. No paid round has been run.

## Hardware and environment

| | |
|---|---|
| Machine | Apple M5 Max, 18 cores, 128 GB, macOS 26.6 |
| Runtime | .NET 10.0.403 (harness and server, net10.0, Release) |
| Docker | 29.8.2; bench Postgres `pgvector/pgvector:0.8.1-pg17` (digest `sha256:3e8b3adfd27b5707128f60956f62a793c3c9326ea8cfaf0eab7adccb5d700b21`) |
| Server | built from the working tree by `start-bench-server.sh`, settings `docker/config/mymorningnews.bench.json` |
| Model, embeddings, search, pages, speech | the harness stub (`stub-2`): `bench-stub-chat`, `bench-hash-384`, in process on 127.0.0.1:12503 |
| Shared during runs | The development stack (`mymorningnews-*`, idle) was running on the same machine. Load numbers are therefore indicative. |

## Round 0 (`round0`), 2026-11-20

The product as of commit `c6c0ffa` (the server binary was built before any product change; reports say `-dirty`
because the harness was not yet committed). Command: `REINGEST=1 benchmarks/run-baseline.sh round0`.

Reports: `20261120-214352-ingest-mymorningnews-synthetic-round0`, `20261120-214417-quality-mymorningnews-synthetic-round0`,
`20261120-214420-ingest-mymorningnews-live-round0`, `20261120-214428-quality-mymorningnews-live-round0`,
`20261120-214431-e2e-mymorningnews-synthetic-round0`, `20261120-214506-resilience-mymorningnews-synthetic-round0`,
`20261120-214710-load-mymorningnews-synthetic-round0`.

### Quality, synthetic (65 probes, 20 negative)

| Arm | Overall | Negative | Ranking nDCG@10 |
|---|---|---|---|
| default | 0.855 [0.762, 0.934] | 0.700 [0.500, 0.900] | 0.715 (n=5, indicative) |
| llm-only | 0.916 [0.844, 0.976] | 0.850 [0.700, 1.000] | 0.910 (n=5, indicative) |
| floor-search-order | 0.537 [0.399, 0.670] | 0.316 [0.105, 0.526] | 0.662 (n=5, indicative) |
| reference-lexical | 0.859 [0.762, 0.938] | 0.737 [0.526, 0.895] | 0.705 (n=5, indicative) |

### Quality, live (23 probes, 14 negative)

| Arm | Overall | Negative | Ranking nDCG@10 | Surfaced |
|---|---|---|---|---|
| default | 0.995 [0.984, 1.000] | 1.000 | 0.876 (n=1) | 1.000 (n=6) |
| llm-only | 0.854 [0.696, 0.984] | 1.000 | 0.641 (n=1) | 0.500 (n=6) |
| floor-search-order | 0.696 [0.506, 0.869] | 0.571 | 0.620 (n=1) | 1.000 (n=6) |
| reference-lexical | 0.840 [0.680, 0.966] | 1.000 | 0.641 (n=1) | 0.500 (n=6) |

On the live docs the composite helps: three relevant sections get model scores under 40, and recency, novelty, and
source lift them into view. On the synthetic corpus the same lift shows distractors. Same mechanism, both directions.

### Resilience (11 scenarios): 0.727

| Scenario | Round 0 | What happened |
|---|---|---|
| baseline, model-429-once, model-slow, embeddings-down, search-down, pages-down, tts-down, tts-429-once | pass | Retries on model and speech rate limits work; optional embedding degrades to a partial success; hard failures end with a message. |
| model-down | **fail** | The run failed cleanly, but the recovery run showed 1 finding against 11 for a healthy run: the failed run's pages were "seen". |
| dry-run-first | **fail** | The real run after a dry run showed 0 findings: the dry run had used up every page. |
| search-429-once | **fail** | One 429 per query failed the run ("Every search provider failed"); search had no retry. |

### End to end, ingest, load

- e2e (3 repeats): every check passed (run, briefing, email with links, audio, feedback link recorded).
- ingest: every round completed with no fetch failures and reachability 1.0.
- load (indicative): API reads p95 3.1 ms at 1 worker (492 req/s), 6.0 ms at 4 (1,090 req/s), 11.8 ms at 16
  (2,311 req/s), no errors; open loop at 100 req/s p95 5.1 ms. Six runs queued at once: 54 runs per minute, p95 queue
  to completion 6.5 s while the pipeline itself took p95 0.57 s, so queue wait dominates.

### Defects found by the benchmarks

| # | Defect | Fix | Regression test |
|---|---|---|---|
| 1 | Search requests were never retried, so one 429 (or 502, 503, 504) from SearXNG failed the query, and a run whose queries all hit one failed. | `HttpSearchSourceBase.GetTextAsync` retries 408, 429, 500, 502, 503, and 504 up to `search.maxAttempts` (default 3), waiting for `Retry-After` (at most 5 s) or an exponential backoff from 500 ms. | `Search.TransientRetry` |
| 2 | Pages fetched by a run that then failed were remembered as seen, so the next run skipped them and their findings were lost. | `AssembleContextStage` counts a page as seen only when a non-dry run that finished (`Completed` or `PartiallySucceeded`) fetched it. | `Pipeline.SeenOnlyAfterFinishedRuns` |
| 3 | A dry run (a preview, "not counted in history") used up its pages the same way, so the real run after it found nothing new. | The same change. | `Pipeline.SeenOnlyAfterFinishedRuns` |

## Round 1 (`round1`), 2026-11-20

The three fixes. Command: `REINGEST=1 benchmarks/run-baseline.sh round1`. Reports:
`20261120-215824-ingest-mymorningnews-synthetic-round1`, `20261120-215849-quality-mymorningnews-synthetic-round1`,
`20261120-215852-ingest-mymorningnews-live-round1`, `20261120-215859-quality-mymorningnews-live-round1`,
`20261120-215902-e2e-mymorningnews-synthetic-round1`, `20261120-215938-resilience-mymorningnews-synthetic-round1`,
`20261120-220142-load-mymorningnews-synthetic-round1`.

Resilience rose from 0.727 to 1.000 (every scenario passes; the recovery runs show the full 11 findings). Every quality,
e2e, and ingest score is unchanged, probe for probe, as expected: the fixes touch recovery paths the quality corpora do
not exercise. Load is unchanged within noise (reads p95 3.1, 5.5, and 11.0 ms at 1, 4, and 16 workers; 54 runs per
minute).

## History

Generated with `history --rounds R0=round0,R1=round1` (`20261120-220340-history.md`). Product arms, by probe type;
types under 20 probes are indicative.

| Dataset / arm | Group | n | R0 | R1 | Net |
|---|---|---|---|---|---|
| synthetic / default | overall | 65 | 0.855 | 0.855 | +0.000 |
| synthetic / default | negative | 20 | 0.700 | 0.700 | +0.000 |
| synthetic / default | ranking (nDCG@10) | 5 | 0.715 | 0.715 | +0.000 |
| synthetic / default | surfaced | 15 | 1.000 | 1.000 | +0.000 |
| synthetic / default | suppressed | 13 | 0.615 | 0.615 | +0.000 |
| synthetic / default | duplicate, cluster, seen, canonical, blocked, dedupe precision | 3, 2, 2, 1, 2, 5 | 1.000 | 1.000 | +0.000 |
| synthetic / default | update | 3 | 0.333 | 0.333 | +0.000 |
| synthetic / default | evidence | 7 | 1.000 | 1.000 | +0.000 |
| synthetic / default | injection, guardrail, order | 1, 3, 2 | 1.000 | 1.000 | +0.000 |
| synthetic / default | empty | 1 | 0.000 | 0.000 | +0.000 |
| synthetic / llm-only | overall | 65 | 0.916 | 0.916 | +0.000 |
| live / default | overall | 23 | 0.995 | 0.995 | +0.000 |
| live / llm-only | overall | 23 | 0.854 | 0.854 | +0.000 |
| resilience | overall | 11 | 0.727 | 1.000 | +0.273 |
| e2e | overall | 15 | 1.000 | 1.000 | +0.000 |

Published baselines: none exist for either dataset (each was written for this product); the floor and reference arms
are the anchors, shown above.

## Environment notes

- The development stack was up (idle) during every run. No other benchmark or build ran at the same time as a load
  run. Treat load numbers as indicative.
- End-to-end timings include worker poll intervals: a run waits up to `scheduler.runPollSeconds` (2 s) to be claimed,
  email delivery runs every 2 s in the bench settings, and audio is made by the media worker on its own cycle, so
  "submit to audio ready" (p50 10.2 s) is mostly waiting.

## Known weaknesses of the suite

- **The stub model is lexical.** It cannot tell a basketball team called the Heat from heat pumps, so suppression and
  empty-briefing probes partly measure the stub. Ranking, suppression, injection, and evidence precision against a real
  model need a paid round, which has not been run.
- **Feature-hashed embeddings**, not the shipped `all-minilm`, decide which pairs reach the dedupe judge. The dedupe
  thresholds were tuned for a real embedding model; a free local Ollama arm is the next step.
- **Small probe groups.** Most types have fewer than 20 probes, so their intervals are wide and marked indicative.
- **One host for every page** (`127.0.0.1`), so source reputation from feedback applies to all pages at once.
- **Labels are the author's.** The synthetic corpus and the live labels were written by the same person (one pass for
  pages, a separate pass for probes, checked by `validate`), so they are good but not gold.
- **history** shows one ingest report per round (the last dataset run), not one per dataset.
