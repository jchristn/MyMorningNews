# Benchmarking plan

What the My Morning News benchmark suite measures and why, how each number is computed, what we expected round 0 to
show, and where the suite stands. The operator's guide is [`benchmarks/README.md`](benchmarks/README.md), the curated
numbers are in [`benchmarks/RESULTS.md`](benchmarks/RESULTS.md), and the fixes the suite found are ranked in
[`archive/IMPROVEMENTS.md`](archive/IMPROVEMENTS.md) (archived). This plan follows the family-wide `BENCHMARKING.md` requirements (plan section 28).

(c) 2026 Lattice AI LLC

## What the suite measures, in the product's terms

The product promises that a topic is researched on a schedule by a staged pipeline, that findings are ranked for the
reader, backed by verified evidence, de-duplicated against what the reader already saw, and delivered as a briefing, an
email with detail and feedback links, and audio, and that feedback changes what comes next. The suite turns each
promise into probes with known answers:

| Promise | How it is measured |
|---|---|
| Relevant findings rank first; irrelevant ones are not shown | nDCG@10 of shown findings against graded relevance; surfaced and suppressed probes; an empty-briefing probe |
| Repeats are not shown again; updates are linked | duplicate, cluster, update, seen, and canonical probes; dedupe precision |
| Every finding is backed by verified quotes | evidence probes: scripted misquotes must be rejected and genuine quotes (including typography changes) accepted; verified quotes must be in the page |
| Blocked domains are never fetched | blocked probes |
| Fetched text is data, not instructions | an injected instruction; the scope guardrail on every model request |
| Feedback shapes ranking | order probes after rated findings |
| A run ends in a briefing, an email with working links, and audio | e2e checks, with stage timings |
| Transient failures are retried, optional stages degrade, hard failures are clean and recoverable | resilience scenarios |
| The API and the run queue hold up under load | load: latency percentiles and throughput, closed and open loop, and concurrent runs |

## Gap analysis against the sibling suites

The reference suites (Isis, Pneuma, AssistantHub) measure retrieval over a fixed corpus: a query goes in and a ranked
list comes out. This product's unit of work is a research run, not a query: it plans its own searches, fetches pages,
extracts findings with a model, and decides what is new relative to its own history. That forces four differences.

1. **The corpus is served, not loaded.** The harness hosts a stub search engine and the pages it returns, per round,
   so the product's own search, filter, fetch, and extraction run on known pages. Ingest is "research every round".
2. **History matters.** Duplicates, updates, seen pages, novelty, and feedback depend on earlier rounds, so corpora
   have rounds that run in order on one topic, with feedback given between them.
3. **The model is in the loop at every stage.** A deterministic stub model stands in for it. It answers from the
   prompt text alone and never reads labels; its relevance judgment is lexical. Under stubs the suite measures the
   pipeline given that model, not the quality of a real model, which only a paid run measures.
4. **Probes are outcomes of a run.** A probe asks what the run did with a page (shown, suppressed, flagged, linked,
   skipped, verified), so per-probe outcomes come from the API after the run, mapped to pages by URL.

Shared conventions kept: black box over REST, `validate`/`ingest`/`quality`/`e2e`/`load`/`resilience`/`compare`/
`history`/`stub`, exit codes 0/1/2/3/130, paired JSON and Markdown reports, bootstrap intervals, the `compare` gate, and
committed baselines with a CI job that actually runs the gate.

## Datasets

| Tier | Dataset | Size | Status |
|---|---|---|---|
| Live | `mymorningnews-live.json`: the product's own operator docs, one page per section, frozen 2026-11-20 | 24 pages, 23 probes | Built |
| Synthetic | `mymorningnews-synthetic.json`: fictional Harbor City news, built by script from committed sources | 3 corpora, 5 rounds, 47 pages, 65 probes | Built |
| Public | None fits (below) | | Not applicable |
| Local | Not applicable: the product does not ingest uploaded files | | Not applicable |

**Why no public dataset fits.** The task is "given a reader's topic and history, research the web, decide what is new
and relevant, back it with quotes, and leave out what the reader already saw". Passage-ranking sets (BEIR, MS MARCO)
measure ranking a fixed list for a query, not planning searches, fetching, extracting, and de-duplicating across runs.
News de-duplication sets have no reader topic, history, or evidence spans. MultiHop-RAG has news articles but its
oracle is an answer to a question, not a relevance grade per article for a standing topic. Forcing one in would
measure a neighboring task, so the suite leans on the live and synthetic tiers, and `benchmarks/baselines.json` says
so for each dataset.

## Metric definitions

- **Shown**: a finding that is not a duplicate and whose tier is not `BelowThreshold`, which is what the reader sees.
- **nDCG@10**: linear gain (the item's graded relevance 0 to 3), discount `log2(rank + 1)`, over shown findings in the
  product's rank order. The ideal ranking uses every story in the round. A story counts once: an alias and its
  original, and the members of a same-round duplicate cluster, share a story; a later member scores no gain. A repeat
  of an earlier round (a duplicate item, or a page seen before) has gain 0. When no story has positive gain, nDCG is 1
  if nothing is shown and 0 otherwise.
- **p5**: the share of the first five shown findings with gain 2 or more. **shownPrecision**: the same over all shown
  findings. **recall**: shown stories with gain 2 or more over all such stories in the round.
- **Evidence accuracy**: the share of quotes the stub returned for a page that the product classified correctly
  (verified when the stub marked them genuine, absent when not). **Evidence precision**: the share of verified
  quotes whose normalized text (lowercase, straight quotes, single spaces, no emphasis) is a substring of the page.
- **Dedupe precision**: the share of flagged duplicates whose page is a known duplicate (cluster member after the
  first, an alias, or a duplicate probe item), 1 when none are flagged.
- **Reachability**: the share of the round's stories with gain 2 or more whose page was fetched.
- **Binary probes** (surfaced, suppressed, duplicate, cluster, update, blocked, seen, canonical, injection, order,
  empty, every e2e check, every resilience scenario) score 0 or 1 as defined in `benchmarks/datasets/SCHEMA.md`.
- **Intervals**: 95% percentile bootstrap over probes, 2000 resamples, seed 20261120. Groups under 20 probes are
  marked indicative. A probe whose round failed scores 0 and is kept in the denominator.
- **Latency**: nearest-rank percentiles (p50, p90, p95, p99, max) and the mean over successful operations, client
  side, in milliseconds. Throughput is successful operations over the measured time. Open-loop latency is measured
  from the scheduled send time.
- **Stage attribution**: the server's `mymorningnews.run.stage.duration` and `mymorningnews.llm.call.duration`
  histograms, scraped from the Prometheus exporter before and after the measured phase, 2.5 s after the last run.

## Stubs, floor, and reference

- **Stub model**: every default prompt is recognized by its wording. Relevance is the cosine of 512-dimension
  feature-hashed word vectors (unigrams, stopwords removed, plural s stripped) between the text and the topic name,
  brief, and dimension names. Extraction returns nothing below relevance 0.08; otherwise one finding per page (title,
  the first sentences of the first two paragraphs, the best dimension) with quotes per the page's script. The score
  is `100 * relevance / 0.45`, capped at 100. The dedupe judge answers Update when the new finding has a number the
  old one lacks, Duplicate at word Jaccard 0.5 or more, else Different. Query planning is the topic name plus one query
  per dimension (name and description). Embeddings are 384-dimension feature hashes.
- **Stub search and pages**: SearXNG JSON; a round's pages that share a word with the query, by overlap. Pages are
  plain HTML with a nav and footer around the article.
- **Floor** (`floor-search-order`): every page the same queries return (10 per query, like the product), in search
  order, shown as-is.
- **Reference** (`reference-lexical`): the same retrieval with blocked domains dropped, query strings stripped, pages
  from earlier rounds skipped, the same stub judgment with a fixed threshold of 40, and word-overlap dedupe.
- **Arms**: `default` (shipped weights) and `llm-only` (an ablation with the model judgment as the only component).

## Hypotheses for round 0

Drafted while the probes were designed, before the first full round 0 run, so the results cannot be rationalized
after it. Outcomes are in `benchmarks/RESULTS.md`.

1. The product beats the floor on every negative probe type (blocked, seen, canonical) and on dedupe. *Confirmed.*
2. Search has no retry, so one 429 from SearXNG fails a query, and when every query hits it the run fails.
   *Confirmed (defect 1).*
3. Pages fetched by a run that later fails are treated as seen, so the run after a failure silently loses them.
   *Confirmed (defect 2), and the same holds for dry runs (defect 3).*
4. The composite's neutral components (feedback 50, source 50, novelty, recency) lift low model judgments over the
   display threshold, so the default arm shows more irrelevant findings than the llm-only arm. *Confirmed on the
   synthetic corpus; the opposite on the live corpus, where recency and novelty help ranking.*
5. Updates are only detected inside the near-duplicate band (cosine 0.85 or more), so a reworded update with a new date
   is shown as a new, unlinked finding. *Confirmed (two of three update probes miss).*
6. Evidence verification rejects invented, altered, and one-word quotes and accepts typography changes. *Confirmed.*
7. Every model request leads with the scope guardrail. *Confirmed.*
8. Run throughput is bounded by the run worker's poll interval, not by pipeline work. *Confirmed: queue wait dominates.*

## Product hooks

The suite needed no new endpoint. It uses: `POST /v1.0/api/topics/{id}/run?dryRun=true` (the dry run; the plan's
`/topics/{id}/dry-run` is this query parameter), `GET /v1.0/api/runs/{id}/email-preview`, `POST /v1.0/api/prompts/lab`
(not used yet), `GET /v1.0/api/runs/{id}/stages`, `/results`, `/llm-calls`, findings with `includeDuplicates` and
`includeBelowThreshold`, and the Prometheus exporter. All are in `REST_API.md`.

## Phases and status

- [x] Phase 0: hooks (none needed beyond existing routes and the exporter).
- [x] Harness: `src/Test.Benchmark`, assembly `MyMorningNews.Benchmark`, in the solution, no product references.
- [x] Isolated stack: `benchmarks/docker/compose.yaml`, project `mymorningnews-bench`, ports 125xx, same pins.
- [x] Datasets: live and synthetic, with negative probes, passing `validate`.
- [x] Commands: validate, ingest, quality, e2e, load (closed and open loop), resilience, compare, history, stub.
- [x] Reports with provenance (dirty tree including untracked files, harness commit, image digests, dataset hash).
- [x] Reference and floor arms; bootstrap intervals; incomplete runs exit 3.
- [x] CI gate with committed baselines, job "Benchmark gate" in `.github/workflows/tests.yml`.
- [x] Round 0, defects fixed with regression tests, round 1 recorded.
- [ ] A paid round (needs the owner's approval and a spend cap).
- [ ] Real embeddings arm (Ollama `all-minilm`, free and local) to measure dedupe thresholds as shipped.

## Where the implementation departs from this plan

- **CI job location.** Plan section 27.2 names a separate `benchmarks.yml`; the gate is the "Benchmark gate" job in
  `tests.yml` instead, beside the other suites, as the Phase 13 task asked. It runs on every push and pull request.
- **The dry-run hook** is `POST /topics/{id}/run?dryRun=true`, not a separate `/topics/{id}/dry-run` route.
- **No `prepare` command**, because no public dataset fits; no `agent` command, because there is no agent workflow.
- **The stub model is lexical.** Suppression, empty-briefing, and ranking probes under stubs partly measure the stub
  (a basketball team called the Heat looks relevant to a heat pump topic). They are kept, because they still show what
  the composite does with a given judgment, and the paid run is the measure of a real model.
- **All pages share one host** (`127.0.0.1`), so source reputation from feedback applies to every page at once. Blocked
  pages use outside `.test` hosts that are never fetched.
- **Learning is off in the bench settings** (`learning.enabled false`), because the learning worker runs on a timer and
  would make ranking depend on timing. Feedback still reaches ranking through the feedback component.
