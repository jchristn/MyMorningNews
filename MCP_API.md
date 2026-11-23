# My Morning News MCP API

The My Morning News MCP server lets an MCP client (Claude Code, Claude Desktop, an IDE, or your own agent) list your topics, read today's briefing, dig into the evidence behind a finding, resolve predictions, act on suggestions, and teach the research what matters with feedback and a reason.

It is a separate process, `src/MyMorningNews.McpServer`, built on [Voltaic](https://github.com/jchristn/Voltaic). It has no privileges of its own. Every call goes to the REST API through the C# SDK with the caller's own credentials, so tenant isolation, roles, and ownership are enforced in one place, exactly as for the dashboard and the SDKs.

## Transports

| Transport | How | Default |
|---|---|---|
| Streamable HTTP | `POST` and `GET` on `/mcp`. Health check: `GET /` (no credentials). | `http://127.0.0.1:12105/mcp` |
| stdio | `--stdio`. Standard output carries only MCP messages; diagnostics go to standard error. | |

Run it:

```bash
# Docker (the server must be running): Streamable HTTP on 127.0.0.1:12105, from the published image
# jchristn77/mymorningnews-mcp:latest.
cd docker && docker compose --profile mcp pull && docker compose --profile mcp up -d

# From source: Streamable HTTP
dotnet run --project src/MyMorningNews.McpServer -f net10.0 -- --api-url http://127.0.0.1:12100

# From source: stdio, signing in with a token
MYMORNINGNEWS_TOKEN=<session token> dotnet run --project src/MyMorningNews.McpServer -f net10.0 -- --stdio

# Print every tool, resource, and prompt definition as JSON
dotnet run --project src/MyMorningNews.McpServer -f net10.0 -- --describe
```

| Argument | Environment variable | Default | Meaning |
|---|---|---|---|
| `--api-url` | `MYMORNINGNEWS_MCP_SERVER_URL` or `MYMORNINGNEWS_BASE_URL` | `http://127.0.0.1:12100` | REST API base URL. |
| `--host` | `MYMORNINGNEWS_MCP_HOSTNAME` | `127.0.0.1` | Listen address. Loopback serves local clients only; the container uses `*`. |
| `--port` | `MYMORNINGNEWS_MCP_PORT` | `12105` | Listen port (`8100` in the container, published as `12105`). |
| `--stdio` | | off | Serve over stdio instead of HTTP. |
| `--verbose` | | off | Log protocol messages to standard error. |
| `--describe` | | | Print the definitions as JSON and exit. |
| | `MYMORNINGNEWS_OTLP_ENDPOINT` | none | OTLP collector for the Voltaic meter and traces. |
| | `MYMORNINGNEWS_MCP_TELEMETRY` | `true` | Set `false` to turn telemetry off. |
| | `MYMORNINGNEWS_MCP_PROMETHEUS_PORT` | `0` (off) | Prometheus scrape port (`9465` in the container, scraped by the compose Prometheus). |
| | `MYMORNINGNEWS_MCP_PROMETHEUS_HOSTNAME` | `127.0.0.1` | Hostname the scrape endpoint binds and answers to. No wildcards; the container uses `mcp`. |

## Authentication

Use the same material as the REST API.

**Streamable HTTP.** Send one of these on every request:

- `Authorization: Bearer <session token>` (or `x-token`). Get a token with `POST /v1.0/token` and `x-email` and `x-password`.
- `x-access-key`, `x-secret-key`, and `x-tenant-guid` for a credential whose auth mode allows per-request secrets (`DirectHeader` or `Hybrid`).

Requests without valid credentials get `401` with a `WWW-Authenticate: Bearer` challenge. Each credential is checked against `GET /v1.0/token/details`, and a successful check is remembered for 60 seconds under a SHA-256 fingerprint. A revoked token stops working at once anyway, because every tool call is a REST call made with it.

**stdio.** Set one of these in the environment of the process:

- `MYMORNINGNEWS_TOKEN`
- `MYMORNINGNEWS_ACCESS_KEY`, `MYMORNINGNEWS_SECRET_KEY`, and `MYMORNINGNEWS_TENANT_ID`
- `MYMORNINGNEWS_EMAIL` and `MYMORNINGNEWS_PASSWORD` (with `MYMORNINGNEWS_TENANT_ID` when the email belongs to several tenants). The server signs in at startup and again when the session expires.

Prefer an access key created for the purpose (Credentials in the dashboard), so you can revoke it without changing your password.

## Errors

A tool that fails returns a result with `isError: true` and a sentence the model can act on, for example:

```
The My Morning News API returned 404 NotFound: Topic was not found. Request rqi_mwogv6ug_hHywOm0mDg0R0m1Er2d.
```

Arguments are validated against each tool's input schema before the tool runs. A resource that does not exist returns JSON-RPC error `-32002`.

## Safety

- Tools act as the signed-in user. `run_topic` and `create_topic` make model calls that count against that user's usage and plan.
- Findings, quotes, and briefings contain text from fetched web pages. The server's instructions tell clients to treat it as data, never as instructions.
- Results are compact projections, not full records, and briefing Markdown is cut at `maxCharacters` (default 20000), so a single result cannot flood a model's context.
- Feedback recorded through MCP carries the channel `Mcp`, so analytics can tell it apart.

## Connecting a client

**Claude Code** (Streamable HTTP, with the Docker stack running):

```bash
claude mcp add --transport http mymorningnews http://127.0.0.1:12105/mcp --header "Authorization: Bearer <session token>"
```

**Claude Code** (stdio from a source checkout):

```bash
claude mcp add mymorningnews --env MYMORNINGNEWS_ACCESS_KEY=<key> --env MYMORNINGNEWS_SECRET_KEY=<secret> --env MYMORNINGNEWS_TENANT_ID=default \
  -- dotnet run --project /path/to/MyMorningNews/src/MyMorningNews.McpServer -f net10.0 -- --stdio
```

**Claude Desktop and other clients** that read an `mcpServers` JSON file:

```json
{
  "mcpServers": {
    "mymorningnews": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/MyMorningNews/src/MyMorningNews.McpServer", "-f", "net10.0", "--", "--stdio"],
      "env": {
        "MYMORNINGNEWS_BASE_URL": "http://127.0.0.1:12100",
        "MYMORNINGNEWS_ACCESS_KEY": "<key>",
        "MYMORNINGNEWS_SECRET_KEY": "<secret>",
        "MYMORNINGNEWS_TENANT_ID": "default"
      }
    }
  }
}
```

Clients that speak Streamable HTTP with custom headers use `http://127.0.0.1:12105/mcp` and an `Authorization` header instead.

Try it: ask your client to "list my My Morning News topics", "read today's briefing", or run the `review_morning_briefing` prompt.

## Example

```json
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"submit_feedback","arguments":{"targetType":"Finding","targetId":"fnd_mwoh0c2l_...","rating":"not-relevant","reason":"Already covered elsewhere"}}}
```

```json
{"jsonrpc":"2.0","id":3,"result":{"structuredContent":{"id":"fbk_...","targetType":"Finding","targetId":"fnd_mwoh0c2l_...","relevance":"NotRelevant","accuracy":null,"usefulness":null,"reason":"Already covered elsewhere","channel":"Mcp","createdUtc":"2026-11-20T05:01:12Z"},"content":[{"type":"text","text":"{...same as JSON...}"}]}}
```

## Telemetry

The MCP server runs its own Radiant host and exports the `Voltaic` meter and activity source (tool calls, durations, errors, sessions) to the same Tempo and Prometheus as the REST server. The REST calls it makes appear in the REST server's request history and traces under the caller's identity.

## Registry listing

`src/MyMorningNews.McpServer/server.json` describes the hosted server for the MCP registry (name `ai.mymorningnews/mymorningnews`, Streamable HTTP at `https://app.mymorningnews.ai/mcp`). It is submitted once the hosted service is public: validate it with the registry's `mcp-publisher validate`, prove the `mymorningnews.ai` domain, and publish. Keep its `version` in step with the server.

## Keeping this document current

The reference below is generated from the code. After changing a tool, resource, or prompt, run:

```bash
python3 src/Tools/mcpdocs/generate_mcp_api.py
```

`--check` exits non-zero when the document is out of date; the `Mcp` test suite also checks that every tool is documented here.

<!-- generated:start -->

## Tools

33 tools. Every tool returns structured content that matches its output schema, and the same data as JSON text.

| Tool | Title | Read only | Destructive |
|---|---|---|---|
| [`list_topics`](#list_topics) | List topics | yes | no |
| [`get_topic`](#get_topic) | Read a topic | yes | no |
| [`create_topic`](#create_topic) | Create a topic from a description | no | no |
| [`run_topic`](#run_topic) | Research a topic now | no | no |
| [`get_run`](#get_run) | Read a run | yes | no |
| [`get_run_costs`](#get_run_costs) | What a run cost | yes | no |
| [`update_topic`](#update_topic) | Update a topic | no | no |
| [`set_topic_dimension`](#set_topic_dimension) | Add or change a dimension | no | no |
| [`remove_topic_dimension`](#remove_topic_dimension) | Remove a dimension | no | yes |
| [`add_topic_constraint`](#add_topic_constraint) | Add a constraint | no | no |
| [`remove_topic_constraint`](#remove_topic_constraint) | Remove a constraint | no | yes |
| [`list_schedules`](#list_schedules) | List schedules | yes | no |
| [`set_schedule`](#set_schedule) | Add or change a schedule | no | no |
| [`remove_schedule`](#remove_schedule) | Remove a schedule | no | yes |
| [`list_findings`](#list_findings) | List findings | yes | no |
| [`search_findings`](#search_findings) | Search findings | yes | no |
| [`get_finding`](#get_finding) | Read a finding with evidence | yes | no |
| [`update_finding`](#update_finding) | Pin or hide a finding | no | no |
| [`list_briefings`](#list_briefings) | List briefings | yes | no |
| [`get_latest_briefing`](#get_latest_briefing) | Read today's briefing | yes | no |
| [`get_briefing`](#get_briefing) | Read a briefing | yes | no |
| [`share_briefing`](#share_briefing) | Share a briefing with a public link | no | no |
| [`list_briefing_shares`](#list_briefing_shares) | List a briefing's public links | yes | no |
| [`revoke_briefing_share`](#revoke_briefing_share) | Stop sharing a briefing link | no | yes |
| [`list_predictions`](#list_predictions) | List predictions | yes | no |
| [`resolve_prediction`](#resolve_prediction) | Resolve a prediction | no | no |
| [`list_suggestions`](#list_suggestions) | List suggestions | yes | no |
| [`update_suggestion_status`](#update_suggestion_status) | Accept, snooze, dismiss, or complete a suggestion | no | no |
| [`submit_feedback`](#submit_feedback) | Rate an item, with a reason | no | no |
| [`list_learned_preferences`](#list_learned_preferences) | List learned preferences | yes | no |
| [`accept_learned_preference`](#accept_learned_preference) | Accept a proposed preference | no | no |
| [`reject_learned_preference`](#reject_learned_preference) | Reject a proposed preference | no | no |
| [`get_analytics_summary`](#get_analytics_summary) | Summarize research quality | yes | no |

### list_topics

**List topics.** List your research topics (newest first), with status and last run time. Use the topic ID with the other tools.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `status` | string (`Draft`, `Active`, `Paused`, `Archived`) | no | Only topics in this status. |
| `search` | string (up to 200) | no | Text in the name or description. |
| `pageSize` | integer (1 to 100) | no | Topics per page. Default 25. |
| `pageNumber` | integer (1 to 10000) | no | Page (1-based). Default 1. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `items` | array of object with `id`, `name`, `status`, `description`, `isOneTime`, `emailOnRun`, `audioEnabled`, `lastRunUtc`, `createdUtc` | yes | Items on this page. |
| `totalCount` | integer | yes | Items matching across all pages. |
| `pageNumber` | integer | no | Page number (1-based). |
| `totalPages` | integer | no | Pages available. |

### get_topic

**Read a topic.** Read a topic with its brief, dimensions (the angles researched), constraints, and email recipients.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | yes | Topic ID (top_...). |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `topic` | object with `id`, `name`, `status`, `description`, `isOneTime`, `emailOnRun`, `audioEnabled`, `lastRunUtc`, `createdUtc` | yes |  |
| `brief` | string or null | no | The research brief. |
| `dimensions` | array of object | no | Research dimensions. |
| `constraints` | array of object | no | Constraints. |
| `recipients` | array of string | no | Email recipients. |

### create_topic

**Create a topic from a description.** Create and activate a research topic from a plain description, using the refinement wizard's defaults without asking questions: it proposes dimensions and drafts the brief and constraints, then activates the topic. This makes model calls on your account. Use refine_topic (a prompt) afterwards to adjust it conversationally.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `description` | string (up to 2000) | yes | What to research, in your own words (10 to 2000 characters). |
| `goal` | string (up to 500) | no | What you want to get out of it, for example 'decide whether to buy'. |
| `depth` | string (`overview`, `balanced`, `deep`) | no | How deep to go. |
| `horizon` | string (up to 200) | no | Time horizon, for example 'next 6 months'. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `topic` | object with `id`, `name`, `status`, `description`, `isOneTime`, `emailOnRun`, `audioEnabled`, `lastRunUtc`, `createdUtc` | yes |  |
| `brief` | string or null | no | The drafted brief. |
| `dimensions` | array of string | no | Dimension names. |

### run_topic

**Research a topic now.** Queue a research run for a topic now. Set wait to have the tool wait for the run to finish (up to waitSeconds) and return its result. A run searches, reads sources, verifies quotes, ranks findings, and may email the topic's recipients. It makes model calls on your account.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | yes | Topic ID (top_...). |
| `dryRun` | boolean | no | Research without saving findings for delivery or sending email. Default false. |
| `wait` | boolean | no | Wait for the run to finish. Default false. |
| `waitSeconds` | integer (10 to 900) | no | How long to wait when wait is true. Default 300. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `run` | object with `id`, `topicId`, `status`, `trigger`, `isDryRun`, `queuedUtc`, `startedUtc`, `completedUtc`, `summary`, `newFindingCount`, `duplicateCount`, `sourceCount`, `estimatedCostUsd`, `llmCallCount`, `unpricedCallCount`, `inputTokens`, `outputTokens`, `errorMessage`, `skippedReason` | yes |  |
| `finished` | boolean | yes | True when the run reached a final status. |

### get_run

**Read a run.** Read a research run: status, counts, estimated cost, and summary, with each pipeline stage's status, model calls, and cost. Use get_run_costs for the full breakdown by stage, model, and task.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `runId` | string (up to 64) | yes | Run ID (run_...). |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `run` | object with `id`, `topicId`, `status`, `trigger`, `isDryRun`, `queuedUtc`, `startedUtc`, `completedUtc`, `summary`, `newFindingCount`, `duplicateCount`, `sourceCount`, `estimatedCostUsd`, `llmCallCount`, `unpricedCallCount`, `inputTokens`, `outputTokens`, `errorMessage`, `skippedReason` | yes |  |
| `stages` | array of object with `id`, `stage`, `status`, `itemsIn`, `itemsOut`, `durationMs`, `llmCallCount`, `unpricedCallCount`, `estimatedCostUsd`, `error` | no | Pipeline stages in order. |

### get_run_costs

**What a run cost.** What a research run cost: totals, each pipeline stage (with its share of the cost), each model, and each task, with tokens and call counts. Amounts are estimates in US dollars from each model endpoint's prices; calls with no price (an endpoint without prices, or an agent CLI on a subscription) are counted in unpricedCallCount and left out of the amounts. Current while the run is going.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `runId` | string (up to 64) | yes | Run ID (run_...). |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `runId` | string | yes | Run ID. |
| `topicId` | string | no | Topic. |
| `status` | string | no | One of: Queued, Running, Completed, PartiallySucceeded, Failed, Cancelled, Skipped. |
| `currency` | string | yes | Always USD. |
| `totals` | object with `estimatedCostUsd`, `inputCostUsd`, `outputCostUsd`, `llmCallCount`, `unpricedCallCount`, `failedCallCount`, `inputTokens`, `outputTokens`, `cachedInputTokens`, `cacheWriteTokens`, `reasoningTokens`, `modelDurationMs` | yes | Totals across the run. |
| `costPerNewFindingUsd` | number or null | no | Estimated cost divided by new findings. Null when there is no priced cost or no new finding. |
| `stages` | array of object with `runStageId`, `stage`, `status`, `durationMs`, `shareOfCost`, `estimatedCostUsd`, `inputCostUsd`, `outputCostUsd`, `llmCallCount`, `unpricedCallCount`, `failedCallCount`, `inputTokens`, `outputTokens`, `cachedInputTokens`, `cacheWriteTokens`, `reasoningTokens`, `modelDurationMs` | yes | One row per stage, in run order. |
| `unattributed` | object or null | no | Totals of calls with no stage (same fields as totals), or null when every call has one. |
| `byModel` | array of object with `modelEndpointId`, `endpointName`, `provider`, `model`, `estimatedCostUsd`, `inputCostUsd`, `outputCostUsd`, `llmCallCount`, `unpricedCallCount`, `failedCallCount`, `inputTokens`, `outputTokens`, `cachedInputTokens`, `cacheWriteTokens`, `reasoningTokens`, `modelDurationMs` | no | By endpoint and model, most expensive first. |
| `byTask` | array of object with `task`, `estimatedCostUsd`, `inputCostUsd`, `outputCostUsd`, `llmCallCount`, `unpricedCallCount`, `failedCallCount`, `inputTokens`, `outputTokens`, `cachedInputTokens`, `cacheWriteTokens`, `reasoningTokens`, `modelDurationMs` | no | By model task, most expensive first. |

### update_topic

**Update a topic.** Change a topic's name, description, brief, status (pause with Paused, resume with Active), whether it emails after every run, or whether every run briefing is turned into audio automatically.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | yes | Topic ID (top_...). |
| `name` | string (up to 200) | no | New name. |
| `description` | string (up to 2000) | no | New description. |
| `brief` | string (up to 8000) | no | New research brief: what to look for and why. |
| `status` | string (`Active`, `Paused`, `Archived`) | no | New status. |
| `emailOnRun` | boolean | no | Email after every run. |
| `audioEnabled` | boolean | no | Turn every briefing of the topic's research runs into audio automatically, read in the topic's voice. Uses the plan's audio minutes. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `id` | string | yes | Topic ID (top_...). |
| `name` | string | yes | Name. |
| `status` | string | yes | One of: Draft, Active, Paused, Archived. |
| `description` | string or null | no | Description. |
| `isOneTime` | boolean | no | One-time report rather than recurring. |
| `emailOnRun` | boolean | no | Emails after every run. |
| `audioEnabled` | boolean | no | Every run briefing is turned into audio automatically. |
| `lastRunUtc` | string or null | no | Last run (UTC). |
| `createdUtc` | string | no | Created (UTC). |

### set_topic_dimension

**Add or change a dimension.** Add a research dimension (an angle the research covers) to a topic, or change one when dimensionId is given. Weight High gets more attention; Exclude rules an angle out.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | yes | Topic ID (top_...). |
| `dimensionId` | string (up to 64) | no | Dimension to change; omit to add one. |
| `name` | string (up to 120) | no | Name, for example 'Pricing changes'. |
| `description` | string (up to 1000) | no | What it covers. |
| `weight` | string (`Exclude`, `Low`, `Normal`, `High`) | no | How much it matters. Default Normal. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `id` | string | yes | Dimension ID. |
| `name` | string | yes | Name. |
| `description` | string or null | no | Description. |
| `weight` | string | no | One of: Exclude, Low, Normal, High. |

### remove_topic_dimension

**Remove a dimension.** Remove a research dimension from a topic.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | yes | Topic ID (top_...). |
| `dimensionId` | string (up to 64) | yes | Dimension ID. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `removed` | boolean | yes | True when removed. |

### add_topic_constraint

**Add a constraint.** Add a constraint to a topic: keywords or entities to include or exclude, preferred or blocked domains, a question to track, or a feed or seed URL.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | yes | Topic ID (top_...). |
| `kind` | string (`IncludeKeyword`, `ExcludeKeyword`, `IncludeEntity`, `ExcludeEntity`, `PreferredDomain`, `BlockedDomain`, `TrackedQuestion`, `FeedUrl`, `SeedUrl`) | yes | Kind of constraint. |
| `value` | string (up to 500) | yes | The keyword, entity, domain, question, or URL. |
| `note` | string (up to 500) | no | Why, briefly. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `id` | string | yes | Constraint ID. |
| `kind` | string | yes | One of: IncludeKeyword, ExcludeKeyword, IncludeEntity, ExcludeEntity, PreferredDomain, BlockedDomain, TrackedQuestion, FeedUrl, SeedUrl. |
| `value` | string | yes | Value. |
| `note` | string or null | no | Note. |

### remove_topic_constraint

**Remove a constraint.** Remove a constraint from a topic.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | yes | Topic ID (top_...). |
| `constraintId` | string (up to 64) | yes | Constraint ID. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `removed` | boolean | yes | True when removed. |

### list_schedules

**List schedules.** List your schedules, or only those of one topic or one edition, with what each does and when it runs next. A topic schedule starts research runs; an edition schedule composes the edition's briefing.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | no | Only this topic's schedules (top_...). |
| `editionId` | string (up to 64) | no | Only this edition's schedules (edn_...). |
| `pageSize` | integer (1 to 100) | no | Schedules per page. Default 25. |
| `pageNumber` | integer (1 to 10000) | no | Page (1-based). Default 1. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `items` | array of object with `id`, `ownerType`, `ownerId`, `name`, `intervalType`, `intervalValue`, `minuteOffset`, `daysOfWeek`, `daysOfMonth`, `timesOfDay`, `cronExpression`, `timeZone`, `startUtc`, `endUtc`, `runOnceUtc`, `enabled`, `missedRunPolicy`, `nextRunUtc`, `lastRunUtc` | yes | Items on this page. |
| `totalCount` | integer | yes | Items matching across all pages. |
| `pageNumber` | integer | no | Page number (1-based). |
| `totalPages` | integer | no | Pages available. |

### set_schedule

**Add or change a schedule.** Add a schedule to a topic (topicId) or an edition (editionId), or change one (scheduleId; only the fields sent change). Pick intervalType and send the fields it uses: DaysOfWeek (daysOfWeek, timesOfDay), EveryNDays (intervalValue, timesOfDay), EveryNWeeks (intervalValue, daysOfWeek, timesOfDay), DaysOfMonth (daysOfMonth, timesOfDay), EveryNHours (intervalValue, minuteOffset), EveryNMinutes (intervalValue), Once (runOnceUtc), Cron (cronExpression), or Manual (never fires by itself). Times of day are local to timeZone. The result's nextRunUtc shows when it fires next. The plan may limit how often a topic runs.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `scheduleId` | string (up to 64) | no | Schedule to change (sch_...); omit to add one. |
| `topicId` | string (up to 64) | no | Topic to schedule (top_...), when adding. |
| `editionId` | string (up to 64) | no | Edition to schedule (edn_...), when adding. |
| `name` | string (up to 200) | no | Label, for example 'Weekday mornings'. |
| `intervalType` | string (`EveryNMinutes`, `EveryNHours`, `EveryNDays`, `EveryNWeeks`, `DaysOfWeek`, `DaysOfMonth`, `Once`, `Cron`, `Manual`) | no | How it repeats. Required when adding. |
| `intervalValue` | integer (1 to 1440) | no | Every how many minutes, hours, days, or weeks. |
| `minuteOffset` | integer (0 to 59) | no | Minute past the hour, for EveryNHours. |
| `daysOfWeek` | array of string (`Sunday`, `Monday`, `Tuesday`, `Wednesday`, `Thursday`, `Friday`, `Saturday`) | no | Days it runs on, for DaysOfWeek and EveryNWeeks. |
| `daysOfMonth` | array of integer (-1 to 31) | no | Days of the month, for DaysOfMonth: 1 to 31, or -1 for the last day. |
| `timesOfDay` | array of string (up to 5) | no | Local times of day as 24-hour HH:mm, for example 07:00. |
| `cronExpression` | string (up to 200) | no | Five-field cron expression, for Cron. |
| `timeZone` | string (up to 64) | no | IANA time zone the times are in, for example America/New_York. Default UTC. |
| `runOnceUtc` | string (up to 40) | no | When to run, for Once (ISO 8601, UTC). |
| `startUtc` | string (up to 40) | no | Do not run before this time (ISO 8601, UTC). |
| `endUtc` | string (up to 40) | no | Do not run after this time (ISO 8601, UTC). |
| `enabled` | boolean | no | False pauses the schedule without removing it. Default true. |
| `missedRunPolicy` | string (`Skip`, `RunOnceOnRecovery`) | no | What to do with a run missed while the server was down. Default RunOnceOnRecovery. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `id` | string | yes | Schedule ID (sch_...). |
| `ownerType` | string | yes | Topic or Edition. |
| `ownerId` | string | yes | The topic or edition it runs. |
| `name` | string or null | no | Label. |
| `intervalType` | string | yes | One of: EveryNMinutes, EveryNHours, EveryNDays, EveryNWeeks, DaysOfWeek, DaysOfMonth, Once, Cron, Manual. |
| `intervalValue` | integer (0 to 100000) | no | Interval size for the EveryN types. |
| `minuteOffset` | integer (0 to 59) | no | Minute past the hour for EveryNHours. |
| `daysOfWeek` | array of string | no | Days of the week it runs on. |
| `daysOfMonth` | array of integer (-1 to 31) | no | Days of the month (-1 is the last day). |
| `timesOfDay` | array of string | no | Local times of day (HH:mm). |
| `cronExpression` | string or null | no | Cron expression for the Cron type. |
| `timeZone` | string | no | IANA time zone the times are in. |
| `startUtc` | string or null | no | Not before (UTC). |
| `endUtc` | string or null | no | Not after (UTC). |
| `runOnceUtc` | string or null | no | Run time for the Once type (UTC). |
| `enabled` | boolean | yes | Whether it fires. |
| `missedRunPolicy` | string | no | One of: Skip, RunOnceOnRecovery. |
| `nextRunUtc` | string or null | no | Next run (UTC); null when nothing is due. |
| `lastRunUtc` | string or null | no | Last time it fired (UTC). |

### remove_schedule

**Remove a schedule.** Remove a schedule. The topic or edition stays and can still be run by hand. To pause instead, use set_schedule with enabled false.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `scheduleId` | string (up to 64) | yes | Schedule ID (sch_...). |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `removed` | boolean | yes | True when removed. |

### list_findings

**List findings.** List research findings, highest relevance first. Duplicates, dry runs, and below-threshold findings are left out unless tier is BelowThreshold.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | no | Only this topic (top_...). |
| `runId` | string (up to 64) | no | Only this run (run_...). |
| `tier` | string (`MustRead`, `WorthALook`, `Fyi`, `BelowThreshold`) | no | Only this tier. |
| `minScore` | integer (0 to 100) | no | Minimum relevance score (0 to 100). |
| `since` | string (up to 40) | no | Only findings found at or after this time (ISO 8601, for example 2026-11-18). |
| `pinned` | boolean | no | Only pinned (true) or unpinned (false) findings. |
| `includeHidden` | boolean | no | Include findings you hid. Default false. |
| `pageSize` | integer (1 to 100) | no | Findings per page. Default 20. |
| `pageNumber` | integer (1 to 10000) | no | Page (1-based). Default 1. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `items` | array of object with `id`, `topicId`, `runId`, `title`, `summary`, `whyItMatters`, `category`, `tier`, `relevanceScore`, `confidence`, `isDuplicate`, `pinned`, `hidden`, `publishedUtc`, `createdUtc` | yes | Items on this page. |
| `totalCount` | integer | yes | Items matching across all pages. |
| `pageNumber` | integer | no | Page number (1-based). |
| `totalPages` | integer | no | Pages available. |

### search_findings

**Search findings.** Search findings by text in their titles and summaries, with the same filters as list_findings.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | no | Only this topic (top_...). |
| `runId` | string (up to 64) | no | Only this run (run_...). |
| `tier` | string (`MustRead`, `WorthALook`, `Fyi`, `BelowThreshold`) | no | Only this tier. |
| `minScore` | integer (0 to 100) | no | Minimum relevance score (0 to 100). |
| `since` | string (up to 40) | no | Only findings found at or after this time (ISO 8601, for example 2026-11-18). |
| `pinned` | boolean | no | Only pinned (true) or unpinned (false) findings. |
| `includeHidden` | boolean | no | Include findings you hid. Default false. |
| `pageSize` | integer (1 to 100) | no | Findings per page. Default 20. |
| `pageNumber` | integer (1 to 10000) | no | Page (1-based). Default 1. |
| `query` | string (up to 200) | yes | Words to look for in titles and summaries. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `items` | array of object with `id`, `topicId`, `runId`, `title`, `summary`, `whyItMatters`, `category`, `tier`, `relevanceScore`, `confidence`, `isDuplicate`, `pinned`, `hidden`, `publishedUtc`, `createdUtc` | yes | Items on this page. |
| `totalCount` | integer | yes | Items matching across all pages. |
| `pageNumber` | integer | no | Page number (1-based). |
| `totalPages` | integer | no | Pages available. |

### get_finding

**Read a finding with evidence.** Read one finding with its verified evidence: the exact quotes, where they came from, and the score breakdown.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `findingId` | string (up to 64) | yes | Finding ID (fnd_...). |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `finding` | object with `id`, `topicId`, `runId`, `title`, `summary`, `whyItMatters`, `category`, `tier`, `relevanceScore`, `confidence`, `isDuplicate`, `pinned`, `hidden`, `publishedUtc`, `createdUtc` | yes |  |
| `topicName` | string or null | no | Topic name. |
| `dimension` | string or null | no | The dimension it answers. |
| `rationale` | string or null | no | Why the model scored it this way. |
| `scores` | object | no | Score components (0 to 100): llm, feedback, novelty, source, recency. |
| `evidence` | array of object | yes | Verified quotes. |

### update_finding

**Pin or hide a finding.** Pin a finding to keep it at hand, or hide one you do not want to see again. Hidden findings stay out of lists, briefings, and emails.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `findingId` | string (up to 64) | yes | Finding ID (fnd_...). |
| `pinned` | boolean | no | Pin (true) or unpin (false). |
| `hidden` | boolean | no | Hide (true) or show (false). |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `id` | string | yes | Finding ID (fnd_...). |
| `topicId` | string | no | Topic. |
| `runId` | string | no | Run. |
| `title` | string | yes | Title. |
| `summary` | string or null | no | Summary. |
| `whyItMatters` | string or null | no | Why it matters for the topic. |
| `category` | string | no | Category. |
| `tier` | string | no | One of: MustRead, WorthALook, Fyi, BelowThreshold. |
| `relevanceScore` | number | yes | Composite relevance, 0 to 100. |
| `confidence` | number | no | Model confidence, 0 to 1. |
| `isDuplicate` | boolean | no | Repeats an earlier finding. |
| `pinned` | boolean | no | Pinned by the owner. |
| `hidden` | boolean | no | Hidden by the owner. |
| `publishedUtc` | string or null | no | Source publication time (UTC). |
| `createdUtc` | string | no | Found (UTC). |

### list_briefings

**List briefings.** List your briefings, newest first: morning editions, per-run briefings, and one-time reports.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `kind` | string (`Edition`, `Run`, `OneTime`) | no | Only this kind. |
| `topicId` | string (up to 64) | no | Only this topic's briefings. |
| `editionId` | string (up to 64) | no | Only this edition's briefings. |
| `pageSize` | integer (1 to 50) | no | Briefings per page. Default 10. |
| `pageNumber` | integer (1 to 10000) | no | Page (1-based). Default 1. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `items` | array of object with `id`, `title`, `kind`, `status`, `topicId`, `editionId`, `runId`, `itemCount`, `wordCount`, `readMinutes`, `hasAudio`, `pinned`, `composedUtc`, `createdUtc` | yes | Items on this page. |
| `totalCount` | integer | yes | Items matching across all pages. |
| `pageNumber` | integer | no | Page number (1-based). |
| `totalPages` | integer | no | Pages available. |

### get_latest_briefing

**Read today's briefing.** Read your newest ready briefing as Markdown. Pass topicId or editionId to narrow it, for example to one topic's latest run briefing.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `kind` | string (`Edition`, `Run`, `OneTime`) | no | Only this kind. |
| `topicId` | string (up to 64) | no | Only this topic. |
| `editionId` | string (up to 64) | no | Only this edition. |
| `maxCharacters` | integer (1000 to 200000) | no | Most Markdown characters to return. Default 20000. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `found` | boolean | yes | False when there is no ready briefing yet. |
| `briefing` | object or null | no | The briefing detail, as get_briefing returns it, or null. |

### get_briefing

**Read a briefing.** Read one briefing as Markdown, with its sections and the items it cites (use their IDs with get_finding or submit_feedback).

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `briefingId` | string (up to 64) | yes | Briefing ID (brf_...). |
| `maxCharacters` | integer (1000 to 200000) | no | Most Markdown characters to return. Default 20000. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `briefing` | object with `id`, `title`, `kind`, `status`, `topicId`, `editionId`, `runId`, `itemCount`, `wordCount`, `readMinutes`, `hasAudio`, `pinned`, `composedUtc`, `createdUtc` | yes |  |
| `topicName` | string or null | no | Topic name, for run briefings. |
| `editionName` | string or null | no | Edition name, for edition briefings. |
| `markdown` | string or null | no | The briefing as Markdown (may be truncated; see maxCharacters). |
| `sections` | array of string | no | Section headings in order. |
| `items` | array of object | no | Items cited in the briefing, in order. |

### share_briefing

**Share a briefing with a public link.** Create public links for one of your ready briefings: a reading page and a listening page that anyone with the link can open with no sign-in. The links are returned once and cannot be read back later, so give them to the user now. Every share expires: expiresInDays is 1 to 14 (default 7), and a value outside that range is refused. Only share when the user asks to.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `briefingId` | string (up to 64) | yes | Briefing ID (brf_...). |
| `expiresInDays` | integer (1 to 14) | no | Days until the links stop working. 1 to 14. Default 7. |
| `showOwnerName` | boolean | no | Show your display name on the public pages. Default false. Your email address is never shown. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `share` | object with `id`, `briefingId`, `status`, `expiresUtc`, `revokedUtc`, `showOwnerName`, `viewCount`, `lastViewedUtc`, `createdUtc` | yes |  |
| `readUrl` | string | yes | Public reading page. Anyone with the link can open it with no sign-in. |
| `listenUrl` | string | yes | Public listening page. Anyone with the link can open it with no sign-in. |
| `audioReady` | boolean | no | Whether the briefing has finished audio now. When false the listening page says the audio is not ready. |

### list_briefing_shares

**List a briefing's public links.** List the public share links of one briefing, newest first: when each expires (1 to 14 days after it was made), whether it was revoked, and how often it was opened. The links themselves are shown only when a share is created.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `briefingId` | string (up to 64) | yes | Briefing ID (brf_...). |
| `pageSize` | integer (1 to 100) | no | Shares per page. Default 20. |
| `pageNumber` | integer (1 to 10000) | no | Page (1-based). Default 1. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `items` | array of object with `id`, `briefingId`, `status`, `expiresUtc`, `revokedUtc`, `showOwnerName`, `viewCount`, `lastViewedUtc`, `createdUtc` | yes | Items on this page. |
| `totalCount` | integer | yes | Items matching across all pages. |
| `pageNumber` | integer | no | Page number (1-based). |
| `totalPages` | integer | no | Pages available. |

### revoke_briefing_share

**Stop sharing a briefing link.** Revoke one public share so its reading and listening links stop working at once. This cannot be undone; make a new share to share again.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `briefingId` | string (up to 64) | yes | Briefing ID (brf_...). |
| `shareId` | string (up to 64) | yes | Share ID (shr_...), from list_briefing_shares or share_briefing. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `revoked` | boolean | yes | True when the share is revoked. |
| `shareId` | string | yes | Share ID. |

### list_predictions

**List predictions.** List predictions the research made, with their confidence and when they should be judged. Open predictions past their horizon are ready to resolve.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | no | Only this topic. |
| `status` | string (`Open`, `ProposedResolved`, `Accurate`, `PartiallyAccurate`, `Inaccurate`, `Unresolvable`, `Withdrawn`) | no | Only this status, for example Open. |
| `pageSize` | integer (1 to 100) | no | Predictions per page. Default 20. |
| `pageNumber` | integer (1 to 10000) | no | Page (1-based). Default 1. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `items` | array of object with `id`, `topicId`, `statement`, `rationale`, `confidence`, `horizonUtc`, `status`, `resolvedUtc`, `resolutionNote`, `brierScore` | yes | Items on this page. |
| `totalCount` | integer | yes | Items matching across all pages. |
| `pageNumber` | integer | no | Page number (1-based). |
| `totalPages` | integer | no | Pages available. |

### resolve_prediction

**Resolve a prediction.** Record how a prediction turned out. This feeds the accuracy and calibration reports.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `predictionId` | string (up to 64) | yes | Prediction ID (prd_...). |
| `outcome` | string (`Accurate`, `PartiallyAccurate`, `Inaccurate`, `Unresolvable`) | yes | How it turned out. |
| `note` | string (up to 1000) | no | What happened, briefly. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `id` | string | yes | Prediction ID (prd_...). |
| `topicId` | string | no | Topic. |
| `statement` | string | yes | What is predicted. |
| `rationale` | string or null | no | Why. |
| `confidence` | number | no | Stated probability, 0 to 1. |
| `horizonUtc` | string | no | When it should be judged (UTC). |
| `status` | string | yes | One of: Open, ProposedResolved, Accurate, PartiallyAccurate, Inaccurate, Unresolvable, Withdrawn. |
| `resolvedUtc` | string or null | no | Resolved (UTC). |
| `resolutionNote` | string or null | no | Resolution note. |
| `brierScore` | number or null | no | Brier score once graded (lower is better). |

### list_suggestions

**List suggestions.** List suggested next steps from your research, highest priority first.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | no | Only this topic. |
| `status` | string (`New`, `Accepted`, `Snoozed`, `Dismissed`, `Done`) | no | Only this status, for example New. |
| `pageSize` | integer (1 to 100) | no | Suggestions per page. Default 20. |
| `pageNumber` | integer (1 to 10000) | no | Page (1-based). Default 1. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `items` | array of object with `id`, `topicId`, `title`, `description`, `rationale`, `priority`, `effort`, `status`, `snoozedUntilUtc`, `createdUtc` | yes | Items on this page. |
| `totalCount` | integer | yes | Items matching across all pages. |
| `pageNumber` | integer | no | Page number (1-based). |
| `totalPages` | integer | no | Pages available. |

### update_suggestion_status

**Accept, snooze, dismiss, or complete a suggestion.** Change a suggestion's status. Snoozing needs snoozeDays (or snoozedUntil).

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `suggestionId` | string (up to 64) | yes | Suggestion ID (sug_...). |
| `status` | string (`Accepted`, `Snoozed`, `Dismissed`, `Done`) | yes | New status. |
| `snoozeDays` | integer (1 to 365) | no | Snooze for this many days. |
| `snoozedUntil` | string (up to 40) | no | Snooze until this time (ISO 8601). |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `id` | string | yes | Suggestion ID (sug_...). |
| `topicId` | string | no | Topic. |
| `title` | string | yes | What to do. |
| `description` | string or null | no | Detail. |
| `rationale` | string or null | no | Why. |
| `priority` | string | no | Priority. |
| `effort` | string | no | Effort. |
| `status` | string | yes | One of: New, Accepted, Snoozed, Dismissed, Done. |
| `snoozedUntilUtc` | string or null | no | Snoozed until (UTC). |
| `createdUtc` | string | no | Created (UTC). |

### submit_feedback

**Rate an item, with a reason.** Rate a finding, prediction, suggestion, briefing, briefing section, source, or domain, ideally with a short reason in your own words. Ratings change ranking on the next run, and reasons are distilled into preferences you can accept. A new rating replaces your earlier one for the same item. Findings, briefings, sections, sources, and domains take relevant, somewhat, or not-relevant. Predictions take accurate, partially, inaccurate, or too-early. Suggestions take useful or not-useful.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `targetType` | string (`Finding`, `Prediction`, `Suggestion`, `Briefing`, `BriefingSection`, `SourceItem`, `Domain`) | yes | What is being rated. |
| `targetId` | string (up to 253) | yes | The item's ID (for Domain, the domain name). |
| `rating` | string (`relevant`, `somewhat`, `not-relevant`, `accurate`, `partially`, `inaccurate`, `too-early`, `useful`, `not-useful`) | no | The rating. |
| `reason` | string (up to 2000) | no | Why, in a sentence or two. Optional but it is what the system learns from. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `id` | string | yes | Feedback ID (fbk_...). |
| `targetType` | string | yes | What was rated. |
| `targetId` | string | yes | Rated item. |
| `relevance` | string or null | no | Relevance rating. |
| `accuracy` | string or null | no | Accuracy rating. |
| `usefulness` | string or null | no | Usefulness rating. |
| `reason` | string or null | no | The reason given. |
| `channel` | string | no | Where it came from (Mcp for this server). |
| `createdUtc` | string | no | Recorded (UTC). |

### list_learned_preferences

**List learned preferences.** List the preferences that steer your research: ones you wrote, and ones proposed from your ratings (status Proposed) that wait for your decision.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | no | Only this topic's preferences. |
| `status` | string (`Proposed`, `Active`, `Rejected`, `Disabled`) | no | Only this status, for example Proposed. |
| `pageSize` | integer (1 to 100) | no | Preferences per page. Default 25. |
| `pageNumber` | integer (1 to 10000) | no | Page (1-based). Default 1. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `items` | array of object with `id`, `topicId`, `kind`, `statement`, `status`, `origin`, `supportCount`, `confidence` | yes | Items on this page. |
| `totalCount` | integer | yes | Items matching across all pages. |
| `pageNumber` | integer | no | Page number (1-based). |
| `totalPages` | integer | no | Pages available. |

### accept_learned_preference

**Accept a proposed preference.** Accept a proposed preference so future runs follow it.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `preferenceId` | string (up to 64) | yes | Preference ID (lpr_...). |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `id` | string | yes | Preference ID (lpr_...). |
| `topicId` | string or null | no | Topic, or null for all topics. |
| `kind` | string | no | One of: Prefer, Avoid, PreferSource, AvoidSource, StyleNote. |
| `statement` | string | yes | The preference in plain words. |
| `status` | string | yes | One of: Proposed, Active, Rejected, Disabled. |
| `origin` | string | no | One of: Distilled, User. |
| `supportCount` | integer | no | Ratings behind it. |
| `confidence` | number | no | Confidence, 0 to 1. |

### reject_learned_preference

**Reject a proposed preference.** Reject a proposed preference. It will not be proposed again from the same ratings.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `preferenceId` | string (up to 64) | yes | Preference ID (lpr_...). |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `id` | string | yes | Preference ID (lpr_...). |
| `topicId` | string or null | no | Topic, or null for all topics. |
| `kind` | string | no | One of: Prefer, Avoid, PreferSource, AvoidSource, StyleNote. |
| `statement` | string | yes | The preference in plain words. |
| `status` | string | yes | One of: Proposed, Active, Rejected, Disabled. |
| `origin` | string | no | One of: Distilled, User. |
| `supportCount` | integer | no | Ratings behind it. |
| `confidence` | number | no | Confidence, 0 to 1. |

### get_analytics_summary

**Summarize research quality.** Headline numbers for a window (default the last 30 days): runs, findings shown and rated, relevance precision, prediction accuracy and Brier score, feedback, suggestions, quote verification, briefings, emails, and model cost.

Input:

| Name | Type | Required | Description |
|---|---|---|---|
| `topicId` | string (up to 64) | no | Only this topic. |
| `days` | integer (1 to 365) | no | Window length in days, ending now. Default 30. |

Output:

| Name | Type | Always | Description |
|---|---|---|---|
| `fromUtc` | string | yes | Window start (UTC). |
| `toUtc` | string | yes | Window end (UTC). |
| `activeTopics` | integer | no | Active topics. |
| `runs` | object | no | Runs: total, completed, partiallySucceeded, failed. |
| `findings` | object | no | Findings: shown, rated, relevancePrecision. |
| `predictions` | object | no | Predictions: open, resolved, accuracy, meanBrier. |
| `feedback` | object | no | Feedback: total, withReason. |
| `suggestions` | object | no | Suggestions: new, accepted. |
| `quoteFailureRate` | number or null | no | Share of quotes that failed verification. |
| `briefingsReady` | integer | no | Briefings composed. |
| `emails` | object | no | Emails: sent, failed. |
| `model` | object | no | Model use: calls, costUsd. |

## Resources

| URI | Name | Description |
|---|---|---|
| `mmn://briefings/latest` | Latest briefing | Your newest ready briefing as Markdown. |
| `mmn://briefings/{id}` | Briefing | A briefing as Markdown. |
| `mmn://topics/{id}/brief` | Topic brief | A topic's brief, dimensions, and constraints as Markdown. |
| `mmn://runs/{id}/summary` | Run summary | A run's status, estimated cost, stages with their cost, summary, and top findings as Markdown. |

All resources are `text/markdown`. A missing ID returns JSON-RPC error -32002.

## Prompts

### review_morning_briefing

**Review the morning briefing.** Walk through today's briefing with me, item by item, and record my ratings and reasons as feedback.

| Argument | Required | Description |
|---|---|---|
| `briefingId` | no | Briefing to review (brf_...). Default: the newest ready briefing. |

### refine_topic

**Refine a topic.** Refine one of my research topics in conversation: review its brief, dimensions, and constraints, and change them with me.

| Argument | Required | Description |
|---|---|---|
| `topicId` | yes | Topic to refine (top_...). |

<!-- generated:end -->

---

(c) 2026 Lattice AI LLC
