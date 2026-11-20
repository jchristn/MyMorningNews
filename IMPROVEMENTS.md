# Improvements found by the benchmarks

Candidate changes the benchmark suite surfaced, ranked by value (measured or estimated effect on a named metric) and
simplicity (size and risk). Each names the evidence that motivated it and, once built, the measured result. Change one
thing per round and compare against the previous round. Numbers are from `benchmarks/RESULTS.md`; definitions are in
`BENCHMARKING.md`.

(c) 2026 Lattice AI LLC

## Done

| # | Change | Evidence | Result |
|---|---|---|---|
| D1 | Retry transient search responses (408, 429, 5xx) with Retry-After and backoff (`search.maxAttempts`, default 3). | Round 0 `search-429-once`: one 429 per query failed the run. | Round 1: the run completes with all 11 findings. Resilience 0.727 to 1.000 (with D2). |
| D2 | Count a page as seen only when a finished, non-dry run fetched it. | Round 0 `model-down`: the recovery run showed 1 of 11 findings; `dry-run-first`: the real run after a dry run showed 0. | Round 1: both show 11. |

## Candidates

| Rank | Change | Value | Simplicity | Evidence |
|---|---|---|---|---|
| 1 | **Do not let neutral ranking components lift a weak model judgment over the display threshold.** For example, show a finding only when both the composite and the model judgment clear the threshold, or score feedback and source as "no information" (excluded and the weights renormalized) instead of a neutral 50 when there is no feedback or reputation yet. | High: synthetic negative probes 0.700 (default) against 0.850 (llm-only); the empty briefing shows a finding the model scored 28/100; a solar story scored 24/100 is shown as Fyi. With no feedback, the components other than the model add about 31 points regardless of relevance, so any judgment above about 19 is shown. | Small change in `Ranker`/`ScoreStage`, but it changes ranking semantics in plan section 17: an owner decision. Must not lose the live-corpus gain, where the composite surfaced 3 relevant sections the model under-scored (live surfaced 1.000 against 0.500 for llm-only). | `default` vs `llm-only` arms, `suppressed`, `empty`, `surfaced` probes |
| 2 | **Look for updates beyond the near-duplicate band.** Updates are judged only when cosine similarity is 0.85 to 0.92, so a reworded story with a new date or amount is a new, unlinked finding. Ask the judge about the nearest prior finding whenever it shares the dimension and clears a lower band (for example 0.6), or when the candidate's title shares most entities with a prior one. | Medium: update probes 0.333 (1 of 3; the two misses have cosine 0.60 and 0.39 under the stub embedder). Readers see "new" stories that are follow-ups, with no link to what they already read. | Medium: more judge calls per run (cost), threshold tuning; needs the real-embedding arm (rank 4) before tuning. | `update` probes |
| 3 | **Claim the next queued run as soon as a slot frees**, instead of on the next poll. | Medium for scheduled mornings: six runs queued at once take p95 6.5 s to complete while each run's pipeline takes p95 0.57 s; throughput is 54 runs per minute with `maxConcurrentRuns` 2 and a 2 s poll. | Small: signal the run worker when a run finishes. | `load` `runs-6`: `queueWaitP95Ms` 6,004 |
| 4 | **Benchmark with real embeddings** (Ollama `all-minilm`, free and local, as shipped) in an extra arm. | Needed to judge ranks 1 and 2 under the shipped thresholds; the stub's feature hashing is not the shipped model. | Small in the harness (an embedding endpoint option); needs Ollama on the bench machine. | Suite weakness |
| 5 | **Run a paid round** with a spend cap to measure a real model's relevance judgment, injection resistance, and quote accuracy. | The quality numbers today measure the pipeline under a lexical model. | Small: `PAID=1 MAX_SPEND_USD=... benchmarks/run-baseline.sh r2`. Needs the owner's approval. | Suite weakness |
| 6 | **Make audio start sooner after a run**, or report it as queued in the email. | e2e: submit to audio ready p50 10.2 s, mostly worker polling. Small value while audio is not in the run email. | Small. | e2e `timing-audio` |
| 7 | **Bound the seen-page and dedupe history reads.** `AssembleContextStage` reads at most 1,000 fetched results and `DedupeStage` at most 1,000 recent findings per topic; after that, older pages are fetched again and older findings are not checked. | Low today (no topic is near 1,000), but a silent cap. Not yet measured: a large-history load arm would show it. | Medium: query by hash instead of loading the list. | Code reading during round 0 |
