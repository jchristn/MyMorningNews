# Changelog

All notable changes to this project are documented here. The project follows [Semantic Versioning](https://semver.org). Versions below 1.0.0 are alpha and may include breaking changes.

## [0.1.0-alpha] - Unreleased

### Fixed

- Deleting a user removed their topic rows through a database cascade but left the topics' runs, findings, briefings, and editions behind. User deletion now removes everything the user owns first (`AccountDataService`), which Phase 14 account deletion will reuse.
- Credential auth modes are enforced: `SignedRequest` credentials refuse `x-secret-key` and session exchange, and `SessionExchange` credentials work only through `POST /v1.0/token`.
- Abandoning a refinement of an existing Draft topic no longer deletes that topic.
- Saving a briefing template keeps the name given at creation unless the front matter name changes.
- Credential updates and deletions are audited under their own event and mutation names.
- A member could read another member's fetched source by ID; sources are now visible only to members whose topics found them.
- Deleting a topic now also removes its feedback, reason tags, learned preferences, learning checkpoints, email action tokens and suppressions, prompt overrides, and model assignments. Sent emails stay in the outbox with the topic cleared.
- Feedback ratings are checked against the item type even without an action, and an action on a learned preference gets a clear message.
- Validation messages no longer end with the parameter name. Empty source and template lists report zero pages and their duration. The prompts tenantOnly filter is applied before paging, so counts are right. OpenAPI: the model test body is declared and `search` is no longer listed twice.
- `/openapi.json` sent `Access-Control-Allow-Origin` twice, which browsers reject.

### Added

- Implementation plan (`MY_MORNING_NEWS_PLAN.md`).
- Repository scaffolding: solution, backend projects, test projects, React dashboard, Docker Compose stack, CI workflows.
- Backend foundation: Watson 7 host with OpenAPI, CORS, request IDs, and typed error responses.
- PostgreSQL data layer with tracked, idempotent migrations and first-boot seeding (default tenant, administrator, built-in roles).
- Authentication: session tokens (AES-256-GCM, revocable sessions, refresh, logout), access key and secret credentials, system administration key, login rate limiting.
- Authorization: RBAC with built-in roles, tenant roles and permissions, role assignments, credential scopes with owner ceiling, explicit deny, effective-permission inspection, and audited denials and bypasses.
- Control-plane routes for tenants, users, credentials, roles, permissions, and assignments; profile and password routes.
- Request history capture with secret redaction, truncation, gap-filled summaries, and retention pruning.
- Runtime settings API with masked secrets, live and restart-required fields, environment overrides, and restart.
- Telemetry: application meter and spans, Radiant collector, Prometheus, Tempo, Loki, and Grafana (Overview and HTTP dashboards) in Docker Compose.
- Touchstone test suites for security primitives, settings, database, authentication, authorization, tenant isolation, resources, and observability.
- Dashboard foundation: light and dark themes, sign-in with brand assets, sidebar shell, command palette, ICU translations with pseudo-locales and a verifier, data tables with server paging and filters, hand-rolled SVG charts, and pages for Home, Request History, API Explorer, Settings, Server Info, Health, Audit, Tenants, Users, Credentials, Roles, and Profile.
- OpenAPI document now includes request body schemas and query parameters for every route, and request history can be filtered by status range or class.
- Credential vault: encrypted secrets with previews, rotation, where-used, and an audited, no-store reveal action for tenant administrators.
- Model endpoints through PolyPrompt (Anthropic, OpenAI, OpenAI-compatible, Ollama, Gemini, and more) with validation (tool support and embedding dimensions), model listing, and test calls; per-task model assignments with topic overrides.
- Model gateway: streaming calls, tool-based structured output with `tool_choice` auto and repair turns, JSON fallback for models without tools, retry with backoff, typed refusals, embeddings with model and dimensions.
- Model call accounting: every call records attributed user and topic, input, cached, cache-write, output, reasoning, and total tokens, time to first token, streaming duration, chunks, tokens per second, tool calls, attempts, and split input and output cost.
- Usage analytics API: cost and token reports grouped by user, model, task, provider, topic, prompt, or day, gap-filled time series, the caller's own usage, and platform-wide reports by tenant for global administrators. Grafana "Models and cost" dashboard.
- Mobile backend: rotating refresh tokens for the mobile app with reuse detection, sign-in without a tenant ID (one matching tenant signs in, several return a list), push devices with test notifications through Expo, and a public client configuration route.
- Settings for artifact storage (local or S3, configurable in the server JSON file or with `MYMORNINGNEWS_STORAGE_*` variables, 365-day retention) and push notifications.
- Docker: SearXNG web search in the default stack; TTS, MCP, and Ollama profiles; `.env.example`; a factory demo overlay with a seeder and demo user; reset scripts with `--demo`, `--keep-ollama`, and `--yes`; and a hosted overlay with Caddy for HTTPS.
- Topics with dimensions, constraints, and email recipients, private to their owner (tenant administrators see all).
- Topic refinement wizard: proposes dimensions (locked items survive regeneration), asks clarifying questions in rounds, drafts the research brief and constraints, suggests feeds and seed pages checked with a live fetch, and applies the topic. Sessions resume, and any topic can be refined again. Requests that are tasks rather than topics are declined with a reason.
- One-time requests: "build me a podcast on X" is planned into a brief with at most three clarifying questions, confirmed to run once now or at a set time, and can become a recurring topic.
- Schedules: every n minutes, hours, days, or weeks; days of the week or month; once; cron; or manual, each in its own time zone with daylight-saving handled, plus a preview of the next runs. A scheduler and run workers queue and execute runs (one active run per topic, missed-run policies, run now, dry run, cancel, time limit, concurrency limit), and confirmed one-time requests run when due.
- Dashboard pages for stored credentials (with audited reveal), models (endpoints, assignments, call log), prompts (editor, version diff, lab), usage and cost insights, and platform usage for administrators.
- Prompts a tenant never customized now follow updated defaults; the prompt lab reports cost; the call log filters by user and topic.
- Web search through SearXNG (bundled, no key), Tavily, You.com, RSS and Atom feeds, Hacker News, and arXiv, with per-topic provider choices and a test action. Page fetching respects robots.txt, spaces requests per domain, extracts the main content, and converts HTML and PDF to Markdown.
- Mobile app foundation for iPhone, iPad, and Android phones and tablets (`mobile/`): sign-in with secure token storage and refresh, account screens with usage, a podcast player with background playback, lock-screen controls, downloads, and a sleep timer, push notifications, deep links, tablet split views, and translations scaffolding. `APP_STORE_SUBMISSION.md` lists every store field to fill in.
- Unknown API endpoints return `RouteNotFound`, distinct from `NotFound` for missing records.
- Research pipeline: each run plans queries, searches, filters, fetches, extracts findings backed by quotes verified word for word against the source, de-duplicates against earlier findings, scores and ranks with model judgment plus novelty, source, and recency, summarizes, reviews open predictions, makes new predictions, and suggests what to dig into. Runs record every stage, query, and result, and total their tokens and cost.
- Briefings: Markdown templates with sections you control, a composer that cites its sources, editions across topics on their own schedules (Morning News is set up for you), a briefing after every run, and one-time reports. Each briefing is stored as Markdown and a self-contained HTML report for up to 365 days, or longer when pinned, on local disk or S3.
- Audio: every edition (and any briefing you ask for) can become an episode with chapters, spoken by the bundled Kokoro voices or any OpenAI-compatible speech service, with a private podcast feed you can subscribe to in any podcast app.
- Email: a digest after every run and an email for each edition, with links to every item and one-tap feedback (Relevant, Not relevant, Tell us why) that opens a short form, plus one-click unsubscribe per topic. Works out of the box by writing emails to files; set Mailgun keys to send for real.
- Learning: your ratings and reasons shape what the next run ranks highly and what it searches for, recurring reasons turn into preferences you can apply with one click, and Insights reports show relevance, prediction accuracy and calibration, feedback, suggestions, and evidence quality.
- Promotional codes: administrators can create codes that give a free subscription for a set number of days, cap how many people can use each one, generate batches, see who used them, and revoke them.
- `OWNER_ACTIONS.md` with everything the owner needs to do, grouped by phase.
- Analytics overview (`GET /v1.0/api/analytics/overview`): active topics, runs by outcome, findings shown and rated, relevance precision, briefings, emails, predictions and accuracy, feedback, suggestions, quote failure rate, and model calls and cost in one call.
- Email analytics (`GET /v1.0/api/analytics/email`): sent, failed, pending, and skipped counts, delivery rate, mean time to send, breakdowns by template, day, and status, feedback link use, and unsubscribes. The feedback report adds a split by day and channel.
- Findings can be pinned and hidden (`PUT /v1.0/api/findings/{id}`) and deleted; hidden findings stay out of lists, briefings, and emails. `GET /v1.0/api/evidence/{id}` and `GET /v1.0/api/runs/{id}/llm-calls`.
- SDKs for C# (`sdk/csharp`, NuGet-ready `MyMorningNews.Sdk` for net8 and net10), JavaScript (`sdk/js`, ES module with type declarations), and Python (`sdk/python`), each with signed-request support and a harness that runs against a live server.
- MCP server (`src/MyMorningNews.McpServer`, Voltaic): Streamable HTTP on `/mcp` (port 12105, compose profile `mcp`) and stdio, 26 tools, four Markdown resources, and two prompts, acting with the caller's own credentials.
- The retention worker also prunes expired refresh tokens and email action tokens, and audit records when `Diagnostics.AuditRetentionDays` is set (0, the default, keeps them).
- Run progress as server-sent events (`GET /v1.0/api/runs/{id}/events`) and retry from a stage (`POST /v1.0/api/runs/{id}/retry?fromStage=`), in the API and all three SDKs.
- The server image includes Node.js 22 with npm, Claude Code, Codex, Gemini CLI, git, ripgrep, and Mux (versions set by build arguments). Ollama remains available in compose with `--profile ollama`.
- Client config reports the MCP endpoint (`Rest.McpPublicUrl`), and a credential's auth mode can be changed after creation.
- Dashboard: a live stage timeline for running runs, retry from a stage, and a Sources page with full text. Also pin, hide, and delete findings; evidence in context; a run's model calls; Insights overview and email delivery pages; credential auth modes; an MCP client page. Mobile: pin, hide, and delete findings, and an overview on Insights and Today.
- Signed requests (`MMN1-HMAC-SHA256`): machine clients sign each request with the credential secret instead of sending it, with a clock skew window (`Auth.SignatureMaxSkewSeconds`, default 300) and nonce replay protection.
- Scope guardrail on every model call and in every default prompt, so models stay on research and briefing tasks and treat fetched content as data.
- Prompt templates: 19 defaults, renderer with list expansion, save-time variable validation, versions, rollback, reset, topic overrides, and a prompt lab.
- New tenants are seeded with an Anthropic `claude-opus-5-5` endpoint, an Ollama `all-minilm` embedding endpoint, assignments for every task, and the default prompts.
