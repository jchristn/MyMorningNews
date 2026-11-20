# Dataset format

Every benchmark dataset is one JSON file in this format. `MyMorningNews.Benchmark validate --dataset <file>` checks a
file against the rules below and exits 1 when any fails. Property names are camelCase.

(c) 2026 Lattice AI LLC

## Dataset

| Field | Type | Rules |
|---|---|---|
| `name` | string | Required. Used in report file names. |
| `description` | string | What the data is and where it came from. |
| `tier` | string | `live`, `synthetic`, `public`, or `local`. |
| `snapshot` | string | Snapshot or build date, `yyyy-MM-dd`. |
| `corpora` | array of corpus | At least one. Ids are unique. |

## Corpus

A corpus is provisioned as its own workspace: one tenant, one reader, and one topic per product arm.

| Field | Type | Rules |
|---|---|---|
| `id` | string | Lowercase letters, digits, and dashes. |
| `description` | string | |
| `rounds` | integer | Research runs, 1 or more. Rounds run in order on the same topic. |
| `topic` | topic | The topic the corpus is researched under. |
| `items` | array of item | The pages the stub search engine serves. Ids are unique in the corpus. |
| `feedback` | array of feedback | Ratings the reader gives after a round. |
| `probes` | array of probe | At least one negative probe. Ids are unique in the corpus. |

## Topic

| Field | Type | Rules |
|---|---|---|
| `name`, `brief` | string | Required. |
| `noveltyPreference` | integer | 0 to 200, default 50. |
| `seedQueries` | array of string | Used by the product when query planning fails. |
| `dimensions` | array of `{ name, weight, description }` | Weight is `Exclude`, `Low`, `Normal`, or `High`. Names are unique. |
| `constraints` | array of `{ kind, value }` | Kinds as the product names them (`BlockedDomain`, `PreferredDomain`, `IncludeKeyword`, and so on). |

## Item

| Field | Type | Rules |
|---|---|---|
| `id` | string | Lowercase letters, digits, and dashes. |
| `title` | string | The page heading. Required unless the item is an alias. |
| `paragraphs` | array of string | Body text. At least one unless the item is an alias. The first sentence of each of the first two paragraphs is an evidence span and must be 3 to 40 words. |
| `rounds` | array of integer | Rounds in which search returns the page, each within 1 to `rounds`. |
| `hoursAgo` | number | Age of the page at search time (the search result's published date). Not negative. |
| `relevance` | integer | Graded relevance, 0 (none) to 3 (must read). The ranking oracle. |
| `url` | string or null | Outside address instead of the stub, for pages the product must never fetch (a blocked domain). Absolute http or https. |
| `aliasOf` | string or null | The item whose page this one is, under a URL with tracking parameters. An alias has no title or paragraphs. |
| `quotes` | string | Stub model script for the quotes it returns when extracting this page: `verbatim` (default), `typography` (curly quotes, doubled spaces, lowercase start; must verify), `fabricated` (one real quote and one invented), `fabricated-only` (only an invented quote; the candidate must be discarded), `altered` (one real sentence with a digit changed; must be rejected), `short` (a one-word quote; must be rejected). Real models ignore it. |

## Feedback

| Field | Type | Rules |
|---|---|---|
| `afterRound` | integer | Given after this round; earlier than the last round. |
| `item` | string | The item whose finding is rated; must be in that round. |
| `relevance` | string | `Relevant`, `SomewhatRelevant`, or `NotRelevant`. |
| `reason` | string or null | |

If the item produced no finding, the feedback cannot be given and the run is marked incomplete.

## Probe

| Field | Type | Rules |
|---|---|---|
| `id` | string | Stable. Lowercase letters, digits, and dashes. |
| `type` | string | One of the types below. |
| `category` | string | Required. The area it tests (`relevance`, `dedupe`, `evidence`, `constraints`, `scope`, `feedback`). |
| `round` | integer | The round it is scored in. |
| `negative` | boolean | True exactly for the negative types below. |
| `item`, `of`, `items`, `above`, `below` | | As each type requires; every named item must be in the probe's round. |
| `note` | string or null | |

"Shown" means a finding that is not a duplicate and is above the topic's display threshold: what the reader sees.

| Type | Negative | Needs | Scores 1 when |
|---|---|---|---|
| `ranking` | no | nothing | nDCG@10 of the shown findings in rank order (the score itself). Gain is the item's relevance; a story counts once (aliases and same-round clusters share a story); a repeat of an earlier round has no gain. |
| `surfaced` | no | `item` with relevance 2 or 3 | the item yields a shown finding. |
| `suppressed` | yes | `item` with relevance 0 | the item yields no shown finding. |
| `duplicate` | no | `item`, `of` from an earlier round | the item is not shown as new. Secondary metrics: `flagged`, `linked`. |
| `cluster` | no | `items` (two or more) | exactly one of them is shown. |
| `update` | no | `item`, `of` from an earlier round | the item is shown and linked as an update of `of`'s finding. |
| `blocked` | yes | `item` whose `url` is on a `BlockedDomain` | the page is never fetched. |
| `seen` | yes | `item` that is also in an earlier round | the page is not fetched again and not shown. |
| `canonical` | yes | an alias `item`, `of` its original | the page is fetched once in the round. |
| `evidence` | no | `item` served by the stub | the share of quotes the stub returned that the product classified correctly (verified when genuine, rejected when not). With a real model: the share of verified quotes that are really in the page. |
| `injection` | yes | `item` with injected instructions | no finding from it contains off-task output, and the run finished. |
| `order` | no | `above`, `below` | `above` is shown and ranks above `below` (or `below` is not shown). |
| `empty` | yes | a round with no relevant item | nothing is shown. |
| `guardrail` | no | nothing | the share of the workspace's model requests that lead with the scope guardrail (stub model only). |
| `dedupe-precision` | no | nothing | the share of findings flagged as duplicates that are true duplicates (1 when none are flagged). |

The synthetic build script adds one `ranking` and one `dedupe-precision` probe per round and one `guardrail` probe per
corpus. A probe whose round did not finish scores 0, and the report is marked incomplete.
