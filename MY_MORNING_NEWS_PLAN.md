# My Morning News: Implementation Plan

Status: Draft for approval. Nothing below is implemented yet. All open questions are decided (section 30). Implementation is under way; section 32 logs progress.

Repository: https://github.com/jchristn/MyMorningNews
Initial version: `0.1.0-alpha` (set once at creation; never changed afterward without explicit approval, per `VERSIONING.md`).

---

## Table of Contents

1. [What We Are Building](#1-what-we-are-building)
2. [Glossary](#2-glossary)
3. [Requirements Compliance Matrix](#3-requirements-compliance-matrix)
4. [Conflicts and How They Are Resolved](#4-conflicts-and-how-they-are-resolved)
5. [System Architecture](#5-system-architecture)
6. [Repository Layout](#6-repository-layout)
7. [Docker Stack and Ports](#7-docker-stack-and-ports)
8. [Backend Foundation](#8-backend-foundation)
9. [Authentication, Multi-Tenancy, and RBAC](#9-authentication-multi-tenancy-and-rbac)
10. [Domain Model and Database Schema](#10-domain-model-and-database-schema)
11. [Model Management with PolyPrompt](#11-model-management-with-polyprompt)
12. [Prompt Management](#12-prompt-management)
13. [Topic Intake and the Refinement Wizard](#13-topic-intake-and-the-refinement-wizard)
14. [Scheduling](#14-scheduling)
15. [Search and Fetch Providers](#15-search-and-fetch-providers)
16. [The Research Pipeline](#16-the-research-pipeline)
17. [Relevance Ranking](#17-relevance-ranking)
18. [History, Reinforcement, and Learned Preferences](#18-history-reinforcement-and-learned-preferences)
19. [Evidence Tracking](#19-evidence-tracking)
20. [Accuracy and Relevance Tracking](#20-accuracy-and-relevance-tracking)
21. [Briefings, Templates, and Editions](#21-briefings-templates-and-editions)
22. [Audio and Podcast Output](#22-audio-and-podcast-output)
23. [Email Delivery Component](#23-email-delivery-component)
24. [REST API, SDKs, and MCP Surface](#24-rest-api-sdks-and-mcp-surface)
25. [Dashboard](#25-dashboard)
26. [Telemetry and Observability](#26-telemetry-and-observability)
27. [Testing and CI](#27-testing-and-ci)
28. [Benchmarking and Simulated User Testing](#28-benchmarking-and-simulated-user-testing)
29. [Implementation Phases](#29-implementation-phases)
30. [Open Questions](#30-open-questions)
31. [Risks](#31-risks)
32. [Progress Log](#32-progress-log)

---

## 1. What We Are Building

My Morning News is a self-hosted, multi-tenant research assistant. You tell it what you care about ("I want to learn about AI developer tools"). A wizard uses your configured models to break that interest into dimensions, asks you clarifying questions, and records what to look for and what to avoid. Scheduled research agents then search, read, extract, rank, and synthesize. Each run produces findings with evidence, a view of where things might go next (predictions), and concrete suggestions for what you should dig into. Results arrive as a Markdown briefing, as audio or a podcast episode, and as an email after every run. The email has deep links to the details and one-click feedback links.

What makes it more than a news reader is the loop. Every item examined, every suggestion, every prediction, and every piece of feedback is persisted. The system uses that history to rank tomorrow's results, stops repeating what you already saw, learns what you find irrelevant and why, and keeps a running scorecard of which predictions came true. Dozens of charts make that history visible.

Hard constraints from the request:

- Backend in C# on Watson 7, with PrettyId identifiers and Touchstone tests, built to `~/Code/Agents/requirements`.
- React dashboard, styled after Xeno, Armada, Pneuma, Mux, and the other reference dashboards.
- PostgreSQL is the only database. No SQLite, MySQL, or SQL Server.
- Docker Compose is the default and primary deployment. Host ports are in the 12xxx range.
- Multi-tenancy and RBAC throughout.
- Model selection (local and API) through PolyPrompt.
- User-controlled prompts and briefing formats, defined in Markdown and editable.
- Flexible intervals: every n minutes, hours, days, or weeks; days of the week; and similar.
- Relevance ranking on every result.
- Evidence tracking, feedback with a free-text "why" box, and feedback review over time.
- An email summary on every iteration, with links to details and feedback links in the email, following the SendWithMailgun and HtmlPageBuilder patterns from Xeno and BjjWithJoel.
- Default login: `admin@mymorningnews` / `password` / tenant `default`.
- `assets/logo.png` and `assets/glyph.png` used on the login page, as the favicon, in the README, and elsewhere.

---

## 2. Glossary

| Term | Meaning |
|------|---------|
| Topic | Something you want tracked, for example "AI developer tools". Owned by a user within a tenant. |
| Topic Context | Structured refinement of a topic: brief, dimensions, include/exclude constraints, preferred and blocked sources, audience level, horizon. |
| Dimension | One facet of a topic ("agent frameworks", "pricing and licensing", "evaluation tooling") with a weight. |
| Refinement Session | A wizard conversation in which the model proposes dimensions and asks questions, and you answer. |
| Schedule | When research runs for a topic, or when an edition is composed. |
| Run | One execution of the research pipeline for one topic. |
| Run Stage | One step of a run (plan, search, fetch, extract, score, synthesize, ...), with timing and status. |
| Source Item | A fetched document (URL, title, publisher, extracted text, hash). Deduplicated per tenant. |
| Evidence | A specific excerpt of a source item that supports a claim. |
| Finding | A ranked, summarized result of a run, backed by evidence. |
| Prediction | A forward-looking statement ("where it might go next") with confidence and a horizon date, later resolved as accurate or inaccurate. |
| Suggestion | A concrete recommendation to dig into something, with priority and rationale. |
| Briefing | A composed Markdown document (and optional audio) for a period, built from findings, predictions, and suggestions using a template. |
| Edition | A recurring briefing product, for example "Morning News": a schedule, a set of topics, a template, and delivery options. |
| Briefing Template | A Markdown file that defines the structure and instructions for a briefing and its audio script. |
| Prompt Template | A versioned, editable prompt used by one pipeline task. |
| Feedback | Your rating of a finding, prediction, suggestion, source, or briefing, with an optional "why". |
| Learned Preference | A rule distilled from feedback ("prefer hands-on benchmarks; avoid funding announcements"), which you can review and edit. |

---

## 3. Requirements Compliance Matrix

Every document in `~/Code/Agents/requirements` was read for this plan. The table records how each is satisfied. Section 4 covers the places where the documents disagree with each other or with the request.

| Document | How this plan complies |
|----------|------------------------|
| `CODE_STYLE.md` | Enforced through `CLAUDE.md` and review: namespace-first files with `using` inside the namespace, no `var`, no tuples, one class or enum per file, XML docs on every public member, `_PascalCase` privates, `ConfigureAwait(false)`, a `CancellationToken` on every async method, specific exception types, guard clauses, `<Nullable>enable</Nullable>`, full Dispose pattern, async variants for `IEnumerable` methods, no `Console.WriteLine` in library code, no em-dashes anywhere. Builds must be free of warnings. |
| `BACKEND_ARCHITECTURE.md` | Watson 7 only, using `server.Routes.{PreAuthentication,PostAuthentication}.{Static,Parameter}.Add(...)` registrars (not `server.Get(...)`, which the requirement forbids even though the library exposes it). Thin `Program.cs` plus `Bootstrapper` plus `MyMorningNewsServer`. `RequestContext` in `ctx.Metadata`. PrettyId K-sortable IDs from a central `IdGenerator`. `DatabaseDriverBase`, `DatabaseDriverFactory`, `DatabaseSettings`, `DatabaseTypeEnum`, `SchemaMigration`, domain `I*Methods` interfaces, and a `Postgresql/` provider folder with `Implementations/` and `Queries/`. Structured schema with no BLOB-store tables. Request history at `/v1.0/api/request-history` captured in `PostRouting`. `Server.UseOpenApi()`. Preflight and PostRouting CORS. Runtime settings read, write, and restart API. Docker healthchecks and `restart: unless-stopped`. |
| `AUTHENTICATION.md` | AAA model. Tenants, users, credentials, authsessions, userroles, userroleassignments, credentialscopeassignments, permissions, rolepermissionmaps, audit. Built-in roles seeded and protected. Effective-permission resolution with explicit deny, owner ceiling for credentials, and a cache with a version counter. AES-256 opaque tokens with a random IV per token. Token endpoints at `/v1.0/token`. Constant-time comparisons. Rate-limited auth routes. Cascading deletes. Persisted denial and bypass audit. Effective-permission inspection endpoints. |
| `BACKEND_TEST_ARCHITECTURE.md` | `Test.Shared` (Touchstone.Core only, `MyMorningNewsSuites : ITestSuiteProvider`), `Test.Automated` (`ConsoleRunner.RunMainAsync`, with a `--provider postgresql` argument stripped before Touchstone sees it), `Test.Xunit3` (fact and theory hosts). `127.0.0.1` everywhere. A GitHub Actions workflow on every push and PR with no hidden exclusions. The AOT decision is recorded in the README (section 4, C3). |
| `FRONTEND_ARCHITECTURE.md` | React 19, Vite, React Router 7, Fetch-based `ApiClient`, `AuthContext`, `ThemeContext`, `AppContext`, `useApi`, the required directory tree, the `src/i18n/` module, hand-rolled SVG charts (no chart library), the Home, Request History, and API Explorer views, `RequestDetailsModal`, OpenAPI-driven explorer, nginx Dockerfile. |
| `DASHBOARD_STYLE_AND_USABILITY.md` | A route inventory written before coding (section 25.4). Grouped sidebar, a context-rich topbar, a server-side-first `DataTable` with above-table controls, Select Columns, portaled row menus, View, Edit, View JSON, and Duplicate, bulk actions, custom modals only, the copy-to-clipboard component, light and dark themes, setup wizard and tour, Settings page with restart, visual QA at 1280, 768, and 390 px, and a handoff report. |
| `I18N.md` | i18next with ICU (`i18next-icu`), the 12 baseline locales plus expansion and RTL pseudo-locales, locale-explicit formatters, `LanguageSelector` on login, the topbar, and settings, `lang`/`dir` sync, `Accept-Language` on requests, enum display labels, and CI checks for missing keys, orphaned keys, and hard-coded strings. |
| `TELEMETRY_REQUIREMENTS.md` | Watson 7 telemetry on. Radiant in-process collector subscribed to the `Watson`, `PolyPrompt`, `SendWithMailgun`, `CrawlSharp`, and `MyMorningNews` meters and sources, exporting to Prometheus and Tempo, with logs to Loki (justified by the background pipeline). Prometheus, Tempo, Loki, and Grafana in `compose.yaml`. Grafana folder `MyMorningNews` with per-domain dashboards. External Services card on Home. |
| `REPOSITORY_REQUIREMENTS.md` | `.gitignore`, `.dockerignore`, `README.md`, `DOCKERHUB_README.md` (absolute asset URLs), `CHANGELOG.md`, `LICENSE.md` (MIT), source only in `src/` and `dashboard/`, `.yaml` compose files, `docker/update.bat` (plus `update.sh`), `docker/factory/reset.bat` (plus `reset.sh`), cURL healthchecks with `interval: 5s`, `retries: 2`, and timeout no greater than interval against `127.0.0.1`, `depends_on` with `service_healthy`, `REST_API.md`, a Postman collection in `assets/postman/`, and a tests workflow. |
| `VERSIONING.md` | `0.1.0-alpha` at creation. No version changes after that without explicit approval. |
| `WRITING_DOCUMENTS.md` | No em-dashes anywhere (code, docs, Postman, commits). README and DOCKERHUB_README prose gets the humanizer pass and the Signs of AI writing review before release. |
| `EXAMPLE_APPLICATIONS.md` | Reference projects named per pattern in sections 8, 25, and 27. |
| `SIMULATED_USER_TESTING.md` | Phase 13 session with report under `user-testing/`. `user-testing/evidence/` git-ignored. Stubs for LLM, search, TTS, and Mailgun so no paid calls are made. |
| `BENCHMARKING.md` | In scope (it applies to every platform). Delivered in Phase 13 with a deterministic stub-backed CI gate. See section 28. |
| `INSTALLERS.md` | Applicability decision recorded in section 4, C4. |
| SDKs (`REPOSITORY_REQUIREMENTS.md` item 7) | `sdk/csharp`, `sdk/js`, `sdk/python`, each with a README and a thorough test harness run in CI, defaulting to `127.0.0.1` (section 24.1). |
| MCP (`REPOSITORY_REQUIREMENTS.md` item 14) | `MyMorningNews.McpServer` on Voltaic with `MCP_API.md` covering every tool, resource, and prompt (section 24.2). |

---

## 4. Conflicts and How They Are Resolved

The requirement documents say to satisfy the stricter rule or call out a conflict before implementation. These are the calls.

**C1. Postgres only vs four required database providers.** `BACKEND_ARCHITECTURE.md` requires `Sqlite`, `Mysql`, `Postgresql`, and `SqlServer` provider folders. You asked for Postgres only, and an explicit user instruction is the justified divergence the document allows. The full abstraction stays (`DatabaseTypeEnum`, `DatabaseDriverBase`, `DatabaseDriverFactory`, domain `I*Methods` interfaces, `Postgresql/Implementations` and `Postgresql/Queries`), so another provider could be added later without touching services. `DatabaseTypeEnum` contains only `Postgresql`, and the factory throws `ArgumentException` for anything else. The README records this under "Database support". This also lets the plan use Postgres-native features: `text[]` columns with GIN indexes for permission matching (the "database-native predicate" `AUTHENTICATION.md` asks for, instead of `LIKE` over JSON), `pgvector` for similarity, and `FOR UPDATE SKIP LOCKED` for the scheduler.

**C2. Route prefixes.** `BACKEND_ARCHITECTURE.md` says `/api/v1/...` in one place and `/v1.0/api/request-history` and `/v1.0/api/health` in others. `AUTHENTICATION.md` mandates `/v1.0/token` and `/v1.0/tenants/{tenantGuid}/users/{userGuid}/permissions`. `FRONTEND_ARCHITECTURE.md` uses `POST /v1.0/auth/token`. Resolution:
- `/v1.0/token...` for sessions, exactly as `AUTHENTICATION.md` specifies. The dashboard `ApiClient.login()` calls this route with the `x-email`, `x-password`, and `x-tenant-guid` headers. The stricter security document wins over the frontend sample.
- `/v1.0/tenants/...` for the control plane (tenants and their users, credentials, roles, permissions, assignments, effective permissions), matching the mandated paths.
- `/v1.0/api/...` for everything else (health, request history, settings, and every domain resource).
- `/v1.0/public/...` for the tokenized, unauthenticated email feedback pages and podcast feeds.
- `/openapi.json` as Watson serves it.

**C3. CI on three operating systems vs Postgres only.** `BACKEND_TEST_ARCHITECTURE.md` expects Windows and macOS jobs to run the full suite against SQLite, and GitHub service containers are Linux-only. Decided (Q3): declare `AOT support: Not supported` in the README. The reasons are CrawlSharp (Playwright), TextChunker, and other dependencies that are not marked AOT-compatible, plus a server that ships only as a Linux container. With that, the document allows `ubuntu-latest` only. The full suite runs there against a real `pgvector/pgvector` service container, with no exclusions. If you want three-OS coverage anyway, the alternative is installing Postgres natively on the Windows and macOS runners in the workflow.

**C4. INSTALLERS.md.** It covers apps "that ship to end users" and lists docker, helm, and apt/yum as the server channels. My Morning News is a server distributed as Docker images. Decided (Q4): the Docker channel applies. Images are built from `docker/server/Dockerfile` and `dashboard/Dockerfile` and optionally published to Docker Hub (hence `DOCKERHUB_README.md`). Native installers, the Publisher app, apt/yum, and systemd units are out of scope for `0.x`, and the README says so with the reason.

**C5. Telemetry collector.** `TELEMETRY_REQUIREMENTS.md` uses an in-process collector (Radiant) and lists no OpenTelemetry Collector container. This plan adds no `otel-collector` container. Radiant exports straight to Tempo (OTLP) and Loki, and Prometheus scrapes the server's `/metrics` on its main port.

**C6. External Services card URLs.** The telemetry document shows `http://localhost:3000`. The `127.0.0.1` rule is stricter, so the card shows `http://127.0.0.1:12110` and so on.

**C7. Dashboard route paths.** The style document uses `/dashboard/request-history` and `/dashboard/api-explorer`, while the frontend document codes `/dashboard/requests` and `/dashboard/explorer`. The frontend paths are primary, and the style-document paths are registered as redirect aliases.

**C8. Navigation order.** The frontend document puts Home, Request History, and API Explorer first. The style document says product workflow comes first. Home stays first, the product groups follow, and Request History and API Explorer sit in a visible Observability group before Administration. This deviation is recorded in the dashboard handoff.

**C9. Request History page sizes.** The style document lists 10/25/50/100, while the frontend document lists `[10, 25, 50, 100, 250, 500, 1000]` for Request History. Request History uses the larger set and other tables use 10/25/50/100. The default is 25 everywhere.

**C10. CSS tokens.** The union of both documents is defined. The `--color-*` names from the frontend document stay, and `--color-surface-alt`, `--color-text-muted`, `--color-accent`, `--color-info`, `--color-focus`, the `-soft` tints (from Xeno), and `--chart-series-1..8` (from Conductor's `--qos-series-*`) are added.

**C11. i18n approach.** Armada and Hydra key the catalog by English text. `I18N.md` requires stable keys that are not English sentences, so this plan follows Xeno's i18next layout (`index.js`, `localeRegistry.js`, `resources.js`, `formatters.js`, `LanguageSelector.jsx`, `scripts/verify-i18n.mjs`).

**C12. License file name.** `REPOSITORY_REQUIREMENTS.md` says `LICENSE.md` and `BACKEND_ARCHITECTURE.md` says `LICENSE`. `LICENSE.md` is used, and no NuGet packages are produced, so `PackageLicenseFile` does not apply.

**C13. Touchstone version.** `BACKEND_TEST_ARCHITECTURE.md` was written against Touchstone 1.0.0, and the current packages are 1.1.1 with the same API. Pin `1.1.1` unless you prefer the documented `1.0.0`.

**C14. Password hashing.** `AUTHENTICATION.md` specifies SHA-256 (hex) for passwords, and this plan follows it. Salted or slow hashing would be a deviation, so it is not proposed here.

**C15. Default tenant identifier.** You asked for tenant ID `default`, which is not a `ten_` PrettyId. The seeded tenant uses the literal ID `default` as a documented exception, and every other tenant gets a PrettyId.

---

## 5. System Architecture

```text
                        +-------------------------------+
   Browser  ----------> |  dashboard (nginx, React SPA) |  :12101
     |                  +-------------------------------+
     |  REST + SSE (Bearer token)
     v
+---------------------------------------------------------------+
| server (MyMorningNews.Server, Watson 7)                :12100 |
|  Routes: token, tenants, api/*, public/*, openapi, metrics    |
|  Services: auth, authz, audit, settings, topics, refinement,  |
|            scheduler, pipeline, ranking, briefing, media,     |
|            email, feedback, analytics, prompts, models        |
|  Workers:  SchedulerWorker, RunWorker (bounded), EmailWorker, |
|            MediaWorker, RetentionWorker, LearningWorker       |
+-----+--------------+-------------+-------------+--------------+
      |              |             |             |
      v              v             v             v
 +-----------+  +----------+  +-----------+  +-----------------------+
 | postgres  |  | searxng  |  | tts       |  | external (optional)   |
 | +pgvector |  | :12103   |  | (Kokoro)  |  | Ollama, OpenAI,       |
 | :12102    |  +----------+  | :12104    |  | Anthropic, Gemini,    |
 +-----------+                +-----------+  | Tavily, Mailgun       |
                                             +-----------------------+
 Observability: prometheus :12111, tempo :12112/:12113/:12114,
                loki :12115, grafana :12110
```

Design principles:

1. **The pipeline is a chain of explicit, recorded stages.** Each stage writes a `runstages` row, emits a span and a duration histogram, and stores its inputs and outputs in typed tables. A run can be inspected, re-run from a stage, or replayed with a different prompt or model.
2. **Every model call goes through one gateway** (`ModelGateway`). It resolves the model assigned to a task, renders the prompt template, runs the call through PolyPrompt, enforces structured output, logs the call to `llmcalls` (tokens, latency, cost, prompt version), and emits telemetry.
3. **Everything the user controls is data.** Prompts, briefing templates, model assignments, ranking weights, schedules, and email preferences live in tenant-scoped tables with version history and reset-to-default.
4. **History is first-class.** Dedupe, ranking, synthesis, and prediction review all read from prior runs and feedback, and the context assembled for each prompt is logged so you can see why the agent did what it did.
5. **Background work is durable.** Runs, emails, and audio jobs are database-backed queues claimed with `FOR UPDATE SKIP LOCKED`, so a restart never loses work and the server could later scale to more than one process.

---

## 6. Repository Layout

```text
MyMorningNews/
|-- .github/workflows/
|   |-- tests.yml                 backend suite on postgres service (every push, every PR)
|   |-- dashboard.yml             lint, unit tests, i18n checks, build
|   `-- benchmarks.yml            ci-gate (Phase 13)
|-- assets/
|   |-- logo.png, glyph.png       (provided)
|   |-- icon.png, favicon.ico     generated from glyph.png
|   |-- postman/MyMorningNews.postman_collection.json
|   `-- grafana/                  dashboard JSON (mirrored into docker/grafana)
|-- benchmarks/                   (Phase 13; layout per BENCHMARKING.md)
|-- dashboard/                    React app (section 25)
|-- docker/
|   |-- compose.yaml
|   |-- update.bat, update.sh
|   |-- server/Dockerfile, server/mymorningnews.json
|   |-- searxng/Dockerfile, searxng/settings.yml
|   |-- tts/Dockerfile
|   |-- prometheus.yaml, tempo.yaml, loki.yaml
|   |-- grafana/provisioning/{datasources,dashboards}/, grafana/dashboards/mymorningnews/
|   `-- factory/reset.bat, reset.sh, compose.yaml (seeded demo profile), seed/
|-- docs/
|   |-- BRIEFING_TEMPLATES.md     template format reference
|   |-- PROMPTS.md                prompt keys, variables, defaults
|   |-- EMAIL.md                  email setup (Mailgun, file provider), link and token model
|   |-- I18N.md                   locale governance (glossary, keys, adding a locale)
|   `-- ARCHITECTURE.md
|-- src/
|   |-- MyMorningNews.sln
|   |-- MyMorningNews.Core/       models, enums, requests, responses, database, services, pipeline
|   |   `-- Defaults/             embedded default prompts and briefing templates (*.md)
|   |-- MyMorningNews.Server/     Program, Bootstrapper, MyMorningNewsServer, Routes, Workers, Settings
|   |-- MyMorningNews.McpServer/  Voltaic MCP server (Streamable HTTP and stdio), calls REST via the C# SDK
|   |-- Test.Shared/
|   |-- Test.Automated/
|   |-- Test.Xunit3/
|   `-- Test.Benchmark/           assembly MyMorningNews.Benchmark (Phase 13)
|-- sdk/
|   |-- README.md
|   |-- csharp/                   MyMorningNews.Sdk, MyMorningNews.Sdk.TestHarness, run-tests.sh/.bat
|   |-- js/                       mymorningnews-sdk.js, test-harness.js, run-tests.sh/.bat
|   `-- python/                   mymorningnews_sdk.py, test_harness.py, run-tests.sh/.bat
|-- user-testing/                 reports (evidence/ git-ignored)
|-- .gitignore, .dockerignore
|-- BENCHMARKING.md, IMPROVEMENTS.md
|-- CHANGELOG.md, CLAUDE.md, LICENSE.md
|-- DOCKERHUB_README.md, README.md, REST_API.md, MCP_API.md
`-- mymorningnews.json            default settings for local `dotnet run`
```

The default prompts and briefing templates are Markdown files embedded in `MyMorningNews.Core/Defaults/`. They seed each new tenant, and "reset to default" restores them. You edit your own copies in the dashboard, or import and export them as `.md` files. An optional `Templates.ImportDirectory` setting (mounted at `docker/templates/`) syncs `.md` files from disk into your tenant on startup and on demand, for people who prefer editing files in their own editor.

---

## 7. Docker Stack and Ports

Every host port is bound to `127.0.0.1` and sits in the 12xxx range. The survey of every `compose.yaml` under `~/Code` found only `12345` in use (Grafana Alloy in RecallDB, NotDory, and LiteGraph), so this plan avoids it.

| Service | Image (pinned, never `latest`) | Host port | Container port | Healthcheck |
|---------|-------------------------------|-----------|----------------|-------------|
| `postgres` | `pgvector/pgvector:pg17` (pinned minor) | 12102 | 5432 | `pg_isready` |
| `server` | built from `docker/server/Dockerfile` | 12100 | 8000 | `curl -f http://127.0.0.1:8000/v1.0/api/health` |
| `dashboard` | built from `dashboard/Dockerfile` | 12101 | 80 | `curl -f http://127.0.0.1/health` |
| `searxng` | built `FROM searxng/searxng:<pin>` plus `curl` | 12103 | 8080 | `curl -f http://127.0.0.1:8080/healthz` |
| `tts` | built `FROM ghcr.io/remsky/kokoro-fastapi-cpu:<pin>` plus `curl` | 12104 | 8880 | `curl -f http://127.0.0.1:8880/health` |
| `grafana` | `grafana/grafana-oss:<pin>` | 12110 | 3000 | `curl -f http://127.0.0.1:3000/api/health` |
| `prometheus` | `prom/prometheus:<pin>` plus `curl` if absent | 12111 | 9090 | `curl -f http://127.0.0.1:9090/-/healthy` |
| `tempo` | `grafana/tempo:2.6.1` plus `curl` | 12112 (http), 12113 (OTLP gRPC), 12114 (OTLP HTTP) | 3200, 4317, 4318 | `curl -f http://127.0.0.1:3200/ready` |
| `loki` | `grafana/loki:<pin>` plus `curl` if absent | 12115 | 3100 | `curl -f http://127.0.0.1:3100/ready` |
| `mcp` | built from `src/MyMorningNews.McpServer/Dockerfile` | 12105 | 8100 | `curl -f http://127.0.0.1:8100/health` |
| `ollama` (optional profile `ollama`) | `ollama/ollama:<pin>` plus `curl` | 12120 | 11434 | `curl -f http://127.0.0.1:11434/api/tags` |

The Vite dev server uses port 12190 (not 3000, which collides with Grafana defaults across `~/Code`). The isolated benchmark stack uses 125xx and the user-test stack uses 127xx (section 28).

Compose rules applied:

- `compose.yaml` (not `.yml`), build contexts inside the repo, and pinned image tags.
- Every HTTP service has `healthcheck: { test: ["CMD", "curl", "-f", ...], interval: 5s, timeout: 5s, retries: 2 }`. Third-party images that lack `curl` get a thin Dockerfile under `docker/<service>/` that adds it.
- `depends_on` long form: `server` waits on `postgres: service_healthy`, `tempo: service_healthy`, `loki: service_healthy`, `searxng: service_healthy`, and `tts: service_healthy`. `dashboard` waits on `server: service_healthy`. `grafana` waits on `prometheus`, `tempo`, and `loki`.
- `server` has `restart: unless-stopped` and mounts `docker/server/mymorningnews.json` as a volume so dashboard saves and restarts persist.
- Named volumes: `pgdata`, `media` (audio files), `emailout` (file-provider email output), `grafana`, `prometheus`, `tempo`, `loki`.
- On macOS, Ollama usually runs on the host for GPU access. The seeded embedding endpoint points at `http://host.docker.internal:11434` (Ollama `all-minilm`), with `extra_hosts: ["host.docker.internal:host-gateway"]` on `server`. The `ollama` profile is for Linux hosts.
- `docker/update.bat` and `docker/update.sh`: `docker compose pull`, `docker compose down`, `docker compose up -d`, `docker ps -a`. Named volumes are preserved.
- `docker/factory/reset.bat` and `reset.sh`: destructive factory reset (down with `-v`, re-seed from `docker/factory/seed/`).

Startup from a clean checkout is `cd docker && docker compose up -d --build`, then open `http://127.0.0.1:12101` and sign in with the default credentials.

---

## 8. Backend Foundation

### 8.1 Projects and packages

| Project | Purpose | Key packages |
|---------|---------|--------------|
| `MyMorningNews.Core` | Models, enums, requests and responses, database layer, services, pipeline, integrations | `PrettyId` 2.0.3, `PolyPrompt` 5.0.1, `Npgsql`, `Pgvector`, `SyslogLogging` 2.4.2, `CrawlSharp` 1.2.3, `DocConverter` 0.1.3, `TextChunker` 0.3.3, `SendWithMailgun` 1.2.2, `HtmlPageBuilder` 1.2.1, `Cronos` 0.13.0 (Cron interval type only) |
| `MyMorningNews.Server` | Watson host, routes, workers, settings, telemetry host | `Watson` 7.3.3, `Radiant` 0.1.4 |
| `MyMorningNews.McpServer` | MCP server (Streamable HTTP and stdio) | `Voltaic` 2.4.x, `Radiant`, the C# SDK |
| `Test.Shared` | Touchstone suites | `Touchstone.Core` |
| `Test.Automated` | Console runner | `Touchstone.Cli` |
| `Test.Xunit3` | xUnit v3 host | `Touchstone.Xunit3Adapter`, `xunit.v3.mtp-off`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector` |

All projects target `net10.0` (tests target `net8.0;net10.0` per the test architecture) with `<Nullable>enable</Nullable>`, `<ImplicitUsings>disable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in Release, and XML documentation output enabled.

### 8.2 Composition

- `Program.Main` calls `Bootstrapper.Run(args)`.
- `Bootstrapper` loads settings (file, then environment overrides), validates and clamps, creates `LoggingModule` (with `DetectConsole = false` before `EnableConsole = true`, the container gotcha found in SyslogLogging), starts the Radiant telemetry host (best effort), calls `DatabaseDriverFactory.CreateAndInitializeAsync` (migrations plus seeding), constructs the services and workers, constructs `MyMorningNewsServer`, starts it, and handles SIGTERM and Ctrl+C with a drain.
- `MyMorningNewsServer` owns `Webserver`. It wires `Routes.AuthenticateRequest`, `Routes.Preflight`, `Routes.PostRouting` (CORS, request capture, timing log), `Routes.Exception` (typed error responses, never a stack trace), and `UseOpenApi()`, then registers each feature registrar: `HealthRoutes`, `TokenRoutes`, `TenantRoutes`, `UserRoutes`, `CredentialRoutes`, `RoleRoutes`, `PermissionRoutes`, `AssignmentRoutes`, `AuditRoutes`, `RequestHistoryRoutes`, `SettingsRoutes`, `ModelRoutes`, `PromptRoutes`, `TopicRoutes`, `RefinementRoutes`, `ScheduleRoutes`, `RunRoutes`, `FindingRoutes`, `SourceRoutes`, `PredictionRoutes`, `SuggestionRoutes`, `FeedbackRoutes`, `PreferenceRoutes`, `BriefingRoutes`, `EditionRoutes`, `TemplateRoutes`, `MediaRoutes`, `EmailRoutes`, `PublicFeedbackRoutes`, `PodcastFeedRoutes`, `SearchProviderRoutes`, `VoiceRoutes`, `AnalyticsRoutes`.
- Handlers read `RequestContext` from `ctx.Metadata`, deserialize `ctx.Request.DataAsString` into typed DTOs, pass `ctx.Token` everywhere, set `ctx.Response.StatusCode` explicitly, and send JSON through one serializer helper (`Serialization/Serializer.cs` with a strict enum converter, modeled on Constellation's `StrictEnumConverterFactory`).

Reference patterns: `Armada.Server.ArmadaServer` and `LiteGraph.Server` (host wiring), Lattice `RequestContext` and `RequestHistoryService`, Conductor `RequestHistoryCleanupService`, Pneuma `AuditAndRequestHistoryRoutes`.

### 8.3 Settings

The settings file is `mymorningnews.json`. Environment variables use the `MYMORNINGNEWS_` prefix (for example `MYMORNINGNEWS_DB_PASSWORD`, `MYMORNINGNEWS_AUTH_SIGNING_KEY`, `MYMORNINGNEWS_MAILGUN_API_KEY`, `MYMORNINGNEWS_SETTINGS_FILE`). Each group is a typed class with clamped setters and documented defaults, minimums, and maximums.

| Group | Key fields (default) | Live or restart |
|-------|----------------------|-----------------|
| `Rest` | `Hostname` (`127.0.0.1`; `0.0.0.0` in Docker), `Port` (8000), `Ssl` (false), `CorsAllowedOrigins` (`*`) | restart |
| `Database` | `Type` (`Postgresql`), `Server`, `Port`, `DatabaseName`, `Username`, `Password`, `MaxPoolSize` (50), `CommandTimeoutSeconds` (60) | restart |
| `Logging` | `ConsoleLogging`, `FileLogging`, `LogDirectory`, `LogFilename`, `MinimumSeverity` | live |
| `Auth` | `SigningKey` (env), `SessionLifetimeMinutes` (480 for the dashboard; clamp 15 to 10080), `AdminApiKey` (`mymorningnewsadmin`, env-overridable), `LoginRateLimitPerMinute` (10) | live (except key) |
| `RequestHistory` | `Enabled` (true), `MaxRequestBodyBytes` (65536), `MaxResponseBodyBytes` (65536), `RetentionDays` (30) | live |
| `Telemetry` | `Enabled`, `ServiceName` (`mymorningnews-server`), `OtlpEndpoint` (`http://tempo:4317`), `LokiEndpoint`, `PrometheusEnabled` (true, `/metrics`) | restart |
| `Scheduler` | `Enabled` (true), `PollIntervalSeconds` (15), `MinimumIntervalMinutes` (5), `MissedRunPolicy` (`RunOnceOnRecovery`) | live |
| `Pipeline` | `MaxConcurrentRuns` (2), `MaxConcurrentModelCalls` (4), `RunTimeoutMinutes` (30), `MaxQueriesPerRun` (8), `MaxResultsPerQuery` (10), `MaxFetchesPerRun` (40), `MaxFindingsPerRun` (25), `DryRunEnabled` (true) | live |
| `Ranking` | default weights (section 17), `DuplicateSimilarity` (0.92), `RecencyHalfLifeHours` (72), `DisplayThreshold` (40) | live |
| `Search` | `SearxngUrl` (`http://searxng:8080`), `DefaultProviders` | live |
| `Fetch` | `UserAgent` (`MyMorningNews/0.1`), `RespectRobots` (true), `TimeoutMs` (20000), `PerDomainDelayMs` (1500), `MaxBytes` (2 MB), `UseHeadlessBrowser` (false) | live |
| `Tts` | `Provider` (`OpenAiCompatibleSpeech`), `Endpoint` (`http://tts:8880/v1`), `DefaultVoice`, `Format` (`mp3`) | live |
| `Media` | `Directory` (`/data/media`), `RetentionDays` (365) | restart (directory) |
| `Email` | section 23.6 | live |
| `Templates` | `ImportDirectory` (null) | live |
| `Diagnostics` | `CaptureLlmPayloads` (true), `LlmPayloadRetentionDays` (14) | live |

The settings API follows `BACKEND_ARCHITECTURE.md` exactly:
- `GET /v1.0/api/settings` returns the settings with secrets shown only as set or not set, plus per-field `restartRequired` and `overriddenByEnvironment` metadata.
- `PUT /v1.0/api/settings` validates, clamps, returns per-field errors, writes the file, applies live fields immediately, and never overwrites a secret with a mask placeholder.
- `POST /v1.0/api/settings/restart` drains in-flight work, then exits with a non-zero code so `restart: unless-stopped` relaunches the process.

Every settings change and restart is audited without secret values.

### 8.4 Request history and audit

These are implemented exactly as specified. Capture happens in `PostRouting` through `RequestHistoryCaptureService.Capture`, which builds the entry synchronously and writes it through a bounded channel. Secret headers are redacted (`Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`, `*api-key*`, `*token*`, plus `x-password` and `x-secret-key`). Bodies are truncated with byte counts. The summary endpoint fills empty buckets server-side, and an hourly prune runs. The tokenized public routes (`/v1.0/public/...`) record `PrincipalName = "email-link"`, and the token in the path is redacted to its first 6 characters.

---

## 9. Authentication, Multi-Tenancy, and RBAC

### 9.1 Principals and schemes

| Scheme | Carrier | Use |
|--------|---------|-----|
| Bearer session token | `Authorization: Bearer` or `x-token` (must match if both are present) | All dashboard and API calls |
| Login headers | `x-email`, `x-password`, `x-tenant-guid` on `POST /v1.0/token` only | Dashboard sign-in |
| Direct API key | `x-access-key`, `x-secret-key`, `x-tenant-guid` | Scripts and integrations (credentials) |
| Signed request | `x-access-key`, `x-date`, `x-nonce`, `x-signature`, `x-tenant-guid` | Preferred machine auth (Phase 12) |
| Admin API key | `x-api-key` | Platform administration only, never tenant data plane |
| Email action token | Path token on `/v1.0/public/feedback/{token}` | One-target, limited-action feedback from email (section 23.4) |
| Feed token | Path token on `/v1.0/public/feeds/{token}/...` | Podcast feed and audio enclosures |

Tokens are AES-256 encrypted JSON (session id, principal type, user or credential id, tenant id, issued, expires, nonce, scheme) with a random IV per token, prepended and Base64-encoded. Validation re-checks that the `authsessions` row, user, credential, and tenant are all active. Endpoints: `POST /v1.0/token`, `POST /v1.0/token/refresh`, `GET /v1.0/token`, `GET /v1.0/token/details`, and `DELETE /v1.0/token`.

### 9.2 Seed data (first boot, idempotent)

- Tenant `default` (name "Default", protected).
- User `admin@mymorningnews`, password `password` (stored as a SHA-256 hex hash), `IsAdmin = true`, `IsTenantAdmin = true`, protected. The login page shows these defaults with a "local development" note, and the README tells you to change them.
- Built-in roles with null tenant, protected, refreshed at startup while preserving their IDs: `TenantAdmin`, `SecurityAdmin`, `Auditor`, `ResourceAdmin` (`TopicAdmin`), `Editor`, `Viewer`, `TenantMember`, `Custom`.
- Default prompts and briefing templates for the tenant, the "Morning News" edition (weekdays 06:30 in the tenant time zone), a default **Anthropic** model endpoint (model `claude-opus-5-5`; API key from `MYMORNINGNEWS_ANTHROPIC_API_KEY` if set, otherwise blank so the setup wizard asks for it first), an embedding-only Ollama endpoint (`http://host.docker.internal:11434`, model `all-minilm`, 384 dimensions; Anthropic offers no embeddings API), the SearXNG and RSS search providers, and the Kokoro voice profile.
- No sample topics. First run opens the setup wizard.

### 9.3 Resource types and operations

The standard resource types from `AUTHENTICATION.md` apply, plus these domain types: `Topic`, `Schedule`, `RefinementSession`, `Run`, `Finding`, `Source`, `Evidence`, `Prediction`, `Suggestion`, `Feedback`, `LearnedPreference`, `Briefing`, `Edition`, `Template`, `Prompt`, `ModelEndpoint`, `SearchProvider`, `Voice`, `Media`, `EmailMessage`, `Settings`, `Analytics`.

Operations are `Create`, `Read`, `Update`, `Delete`, `Write`, `Execute` ("run now", "dry run", "send test email", "regenerate audio"), and `Admin`.

A static `RequestTypeEnum` gives one entry per route (for example `TopicRead`, `TopicWrite`, `RunExecute`, `EmailSendTest`). A single `OperationScopeMap` maps each request type to its `(ResourceType, Operation)` pair, so adding a route forces a decision about which permission gate applies.

Topics are user-owned (`tenantid` plus `userid`). Resource-scoped assignments let one user share a topic with another as `Viewer` or `Editor`, and topic children (runs, findings, predictions, suggestions) inherit the topic's resource scope.

### 9.4 Authorization flow

1. `AuthenticationService` resolves exactly one tenant. Conflicting tenant hints are rejected. It authenticates exactly one scheme and builds the `RequestContext` with `AuthenticationContext`, `AuthorizationContext`, `UrlContext`, and `RequestType`.
2. `AuthorizationService` applies the bypass order (admin key, then `IsAdmin`, then `IsTenantAdmin` within the tenant, then RBAC), resolves effective permissions (cached per principal and tenant, invalidated through a monotonic `authzversion` counter), and evaluates explicit deny first, then permit, then implicit deny.
3. Denials and bypasses are persisted to `auditrecords` with request, correlation, and trace IDs. A denial response carries `reason`, `requiredPermission`, `requestType`, and, where safe, `tenantGuid` and `resourceGuid`.
4. Admin endpoints never use the unassigned-user compatibility fallback.
5. The effective-permission endpoints `GET /v1.0/tenants/{tenantId}/users/{userId}/permissions` and `GET /v1.0/tenants/{tenantId}/credentials/{credentialId}/permissions` let the dashboard gate UI elements.

Counters: `auth_requests_total`, `session_events_total`, `rbac_mutations_total`, `authz_requests_total`, `authz_denials_total` (prefixed `mymorningnews_`). Tests prove isolation, modeled on Armada's `MultiTenantScopingTests`: cross-tenant reads return 404, cross-tenant writes are rejected, and enumeration never leaks rows.

---

## 10. Domain Model and Database Schema

### 10.1 Conventions

- Every table has `id VARCHAR(64) PRIMARY KEY` (PrettyId K-sortable, length 32), `createdutc TIMESTAMPTZ`, `lastupdateutc TIMESTAMPTZ`, and `active BOOLEAN`. Tenant-owned tables have `tenantid VARCHAR(64) NOT NULL` with an index. User-owned tables also have `userid`.
- Column names are lowercase with no separators, matching the `AUTHENTICATION.md` schema summary (`tenantid`, `isprotected`, `lastupdateutc`).
- Foreign keys use `ON DELETE CASCADE` where `AUTHENTICATION.md` requires cascading deletes: deleting a topic removes its schedules, runs, findings, evidence links, predictions, suggestions, feedback, and email action tokens.
- Tenant-scoped uniqueness uses composite unique indexes, for example `(tenantid, email)` and `(tenantid, name)`.
- No catch-all JSON columns. The only unmanaged text columns are content and diagnostics: `sourceitems.contenttext` (extracted document text), `briefings.markdown`, `llmcalls.prompttext` and `responsetext` (diagnostic, retention-bound), `requesthistory` bodies, `prompttemplateversions.content`, and `briefingtemplateversions.content`. Each is named for what it holds and documented in `docs/ARCHITECTURE.md`.
- Vectors use pgvector `vector` columns, unconstrained by dimension, because you may switch from `all-minilm` (384) to a model with a different dimensionality (for example `nomic-embed-text` at 768 or OpenAI `text-embedding-3-small` at 1536). Each `embeddings` row records `modelendpointid`, `model`, and `dimensions`, and similarity queries only compare vectors whose `(model, dimensions)` match the tenant's current Embedding assignment, so vectors of different sizes are never compared. Changing the assignment starts a `ReembedJob` that backfills vectors for the new model in the background, with progress shown on the Models page. Until a topic's backfill finishes, dedupe falls back to URL and content hashes and the feedback score stays neutral (50). Old-model vectors are deleted when the backfill completes. Searches are scoped to a tenant and topic, so exact search is fast enough at expected volumes. If needed, partial HNSW expression indexes per dimension (for example `USING hnsw ((vector::vector(384)) vector_cosine_ops) WHERE dimensions = 384`) are added later.
- Migrations are a `SchemaMigration` list (integer `Version`, `Description`, ordered `Statements`) applied idempotently and tracked in `schema_migrations`. Provider SQL lives in `Postgresql/Queries/*Queries.cs`.

### 10.2 ID prefixes (`Constants.IdPrefixes`, generated by `Helpers/IdGenerator.cs`)

| Prefix | Entity | Prefix | Entity |
|--------|--------|--------|--------|
| `ten_` | tenant | `src_` | source item |
| `usr_` | user | `evd_` | evidence excerpt |
| `crd_` | credential | `fnd_` | finding |
| `ses_` | auth session | `prd_` | prediction |
| `rol_` | role | `prv_` | prediction review |
| `prm_` | permission | `sug_` | suggestion |
| `rpm_` | role permission map | `fbk_` | feedback |
| `ura_` | user role assignment | `lrn_` | learned preference |
| `csa_` | credential scope assignment | `brf_` | briefing |
| `aud_` | audit record | `bsc_` | briefing section |
| `req_` | request history entry | `edn_` | edition |
| `top_` | topic | `btp_` | briefing template |
| `dim_` | topic dimension | `btv_` | briefing template version |
| `tcn_` | topic constraint | `pmt_` | prompt template |
| `rfs_` | refinement session | `pmv_` | prompt template version |
| `rfm_` | refinement message | `mdl_` | model endpoint |
| `rfq_` | refinement question | `mda_` | model assignment |
| `sch_` | schedule | `llm_` | llm call |
| `run_` | run | `med_` | media asset (audio) |
| `rst_` | run stage | `chp_` | media chapter |
| `qry_` | search query | `eml_` | email message |
| `srh_` | search result | `eat_` | email action token |
| `spv_` | search provider | `voi_` | voice profile |
| `fdt_` | feed token | `emb_` | embedding |

### 10.3 Tables

The auth tables follow `AUTHENTICATION.md` column for column: `tenants`, `users`, `credentials`, `authsessions`, `userroles`, `userroleassignments`, `credentialscopeassignments`, `permissions` (with `resourcetypes TEXT[]` and `operationtypes TEXT[]`, GIN-indexed), `rolepermissionmaps`, and `auditrecords`. The `userrolemaps` legacy table is omitted because this is a new system and the document says to prefer assignments. The domain tables follow.

**Topics and context**

| Table | Columns (beyond id, tenantid, timestamps, active) |
|-------|---------------------------------------------------|
| `topics` | `userid`, `name`, `slug`, `description`, `brief` (Markdown, user-editable), `audiencelevel` (Beginner, Practitioner, Expert), `horizon` (Days, Weeks, Months, Years), `language`, `status` (Draft, Active, Paused, Archived), `noveltypreference` (0 to 100), `emailonrun` (bool, default true), `emailwhenempty` (bool, default true), `maxemailfindings` (default 10), `displaythreshold`, ranking weight overrides (`weightllm`, `weightfeedback`, `weightnovelty`, `weightsource`, `weightrecency`, all nullable), `briefingtemplateid` (nullable), `lastrunutc`, `lastrefinedutc` |
| `topicdimensions` | `topicid`, `name`, `description`, `weight` (Exclude, Low, Normal, High), `islocked`, `origin` (Model, User), `sortorder` |
| `topicconstraints` | `topicid`, `dimensionid` (nullable), `kind` (IncludeKeyword, ExcludeKeyword, IncludeEntity, ExcludeEntity, PreferredDomain, BlockedDomain, TrackedQuestion, FeedUrl, SeedUrl), `value`, `note`, `islocked`, `origin` |
| `topicsearchproviders` | `topicid`, `searchproviderid`, `enabled`, `maxresults` |
| `topicrecipients` | `topicid`, `email`, `userid` (nullable), `active` |

**Refinement wizard**

| Table | Columns |
|-------|---------|
| `refinementsessions` | `topicid` (nullable until created), `userid`, `status` (Active, Completed, Abandoned), `step` (Describe, Dimensions, Questions, Context, Sources, Delivery, Review), `round`, `seedtext`, `goal`, `modelendpointid`, `completedutc` |
| `refinementmessages` | `sessionid`, `role` (System, Assistant, User), `content`, `llmcallid`, `sequence` |
| `refinementquestions` | `sessionid`, `round`, `dimensionid` (nullable), `questiontext`, `rationale`, `kind` (SingleChoice, MultiChoice, Scale, FreeText, YesNo), `answertext`, `skipped`, `sortorder` |
| `refinementquestionoptions` | `questionid`, `label`, `value`, `selected`, `sortorder` |

**Scheduling and runs**

| Table | Columns |
|-------|---------|
| `schedules` | `ownertype` (Topic, Edition), `ownerid`, `intervaltype` (section 14), `intervalvalue`, `daysofweekmask`, `daysofmonth` (`INT[]`), `cronexpression`, `timezone` (IANA), `startutc`, `endutc`, `runonceutc`, `enabled`, `missedrunpolicy`, `nextrunutc` (indexed), `lastrunutc`, `lockeduntilutc` |
| `scheduletimes` | `scheduleid`, `localtime` (`TIME`), `sortorder` |
| `runs` | `topicid`, `scheduleid` (nullable), `trigger` (Schedule, Manual, Edition, DryRun, Replay), `status` (Queued, Running, Succeeded, PartiallySucceeded, Failed, Canceled), `isdryrun`, `queuedutc`, `startedutc`, `completedutc`, `durationms`, `summary`, `newfindingcount`, `duplicatecount`, `sourcecount`, `inputtokens`, `outputtokens`, `estimatedcostusd`, `errormessage`, `cancelrequested`, `contextsnapshothash` |
| `runstages` | `runid`, `stage` (section 16), `status`, `startedutc`, `completedutc`, `durationms`, `itemsin`, `itemsout`, `errormessage`, `sortorder` |
| `searchqueries` | `runid`, `topicid`, `dimensionid`, `querytext`, `rationale`, `searchproviderid`, `resultcount`, `durationms`, `status` |
| `searchresults` | `searchqueryid`, `runid`, `url`, `canonicalurlhash`, `title`, `snippet`, `publishedutc`, `rank`, `disposition` (Fetched, SkippedSeen, SkippedBlocked, SkippedRobots, SkippedLimit, FetchFailed), `sourceitemid` |

**Sources, evidence, findings**

| Table | Columns |
|-------|---------|
| `sourceitems` | `canonicalurl`, `canonicalurlhash` (unique per tenant), `domain`, `title`, `author`, `publisher`, `publishedutc`, `fetchedutc`, `httpstatus`, `contenttype`, `contenthash`, `contenttext`, `contentbytes`, `language`, `firstseenrunid`, `timesseen` |
| `evidence` | `sourceitemid`, `runid`, `quote`, `startoffset`, `endoffset`, `contexttext`, `quotehash`, `verified` (whether the quote was found verbatim in the source) |
| `findings` | `runid`, `topicid`, `dimensionid`, `title`, `summary`, `whyitmatters`, `category` (News, Release, Research, Opinion, Tutorial, Data, Other), `relevancescore` (0 to 100), `llmscore`, `feedbackscore`, `noveltyscore`, `sourcescore`, `recencyscore`, `tier` (MustRead, WorthALook, Fyi, BelowThreshold), `relevancerationale`, `confidence`, `isduplicate`, `duplicateoffindingid`, `isupdateoffindingid`, `rank`, `userrating` (latest feedback, denormalized for filters) |
| `findingevidence` | `findingid`, `evidenceid`, `role` (Supports, Contradicts, Context), `sortorder` |
| `embeddings` | `ownertype` (Finding, SourceItem, Feedback, Prediction), `ownerid`, `topicid`, `modelendpointid`, `model`, `dimensions`, `vector` (`vector`), `contenthash` |

**Forward-looking outputs**

| Table | Columns |
|-------|---------|
| `predictions` | `runid`, `topicid`, `dimensionid`, `statement`, `rationale`, `confidence` (0 to 1), `horizonutc`, `status` (Open, ProposedResolved, Accurate, PartiallyAccurate, Inaccurate, Unresolvable, Withdrawn), `resolvedutc`, `resolvedby` (User, Agent, AgentConfirmed), `resolutionnote`, `brierscore` |
| `predictionreviews` | `predictionid`, `runid`, `proposedstatus`, `rationale`, `confidence`, `acceptedbyuser` (nullable) |
| `predictionevidence` | `predictionid` or `predictionreviewid`, `evidenceid`, `role` |
| `suggestions` | `runid`, `topicid`, `title`, `description`, `rationale`, `priority` (High, Medium, Low), `effort` (Minutes, Hours, Days), `status` (New, Accepted, Snoozed, Dismissed, Done), `snoozeduntilutc`, `statuschangedutc` |
| `suggestionfindings` | `suggestionid`, `findingid` |

**Feedback and learning**

| Table | Columns |
|-------|---------|
| `feedback` | `userid`, `targettype` (Finding, Prediction, PredictionReview, Suggestion, SourceItem, Domain, Briefing, BriefingSection, LearnedPreference), `targetid`, `topicid`, `relevance` (Relevant, SomewhatRelevant, NotRelevant, nullable), `accuracy` (Accurate, PartiallyAccurate, Inaccurate, TooEarly, nullable), `usefulness` (Useful, NotUseful, nullable), `reason` (free text "why"), `channel` (Dashboard, Email, Api), `emailmessageid` (nullable), `supersedesfeedbackid` |
| `feedbackreasontags` | `feedbackid`, `tag` (model-extracted theme such as "too basic", "already known", "vendor marketing", "wrong region"), `origin` |
| `learnedpreferences` | `topicid` (nullable means all topics), `kind` (Prefer, Avoid, PreferSource, AvoidSource, StyleNote), `statement`, `status` (Proposed, Active, Rejected, Disabled), `origin` (Distilled, User), `supportcount`, `confidence`, `lastevidenceutc` |
| `learnedpreferencefeedback` | `learnedpreferenceid`, `feedbackid` |

**Briefings, templates, media**

| Table | Columns |
|-------|---------|
| `editions` | `userid`, `name`, `description`, `briefingtemplateid`, `voiceprofileid`, `audioenabled`, `emailenabled`, `freshresearchbeforecompose` (bool), `freshresearchleadminutes`, `lookbackmode` (SinceLastEdition, FixedHours), `lookbackhours` |
| `editiontopics` | `editionid`, `topicid`, `sortorder` |
| `briefings` | `editionid` (nullable), `topicid` (nullable, for single-run briefings), `runid` (nullable), `periodstartutc`, `periodendutc`, `title`, `markdown`, `status` (Composing, Ready, Failed), `briefingtemplateversionid`, `mediaassetid`, `wordcount`, `readminutes` |
| `briefingsections` | `briefingid`, `sectionkey`, `heading`, `markdown`, `sortorder` |
| `briefingitems` | `briefingid`, `sectionid`, `itemtype` (Finding, Prediction, Suggestion), `itemid`, `sortorder` |
| `briefingtemplates` / `briefingtemplateversions` | `name`, `description`, `isdefault`, `currentversion`; versions hold `version`, `content` (Markdown), `changenote`, `createdbyuserid` |
| `prompttemplates` / `prompttemplateversions` | `key` (unique per tenant and topic scope), `topicid` (nullable override), `name`, `currentversion`; versions hold `version`, `content`, `changenote` |
| `mediaassets` | `briefingid`, `kind` (BriefingAudio, PodcastEpisode), `status` (Queued, Scripting, Synthesizing, Ready, Failed), `filepath`, `contenttype`, `bytes`, `durationseconds`, `voiceprofileid`, `scriptmarkdown`, `errormessage` |
| `mediachapters` | `mediaassetid`, `briefingsectionid`, `title`, `startseconds`, `endseconds` |
| `voiceprofiles` | `name`, `provider`, `endpoint`, `voice`, `secondaryvoice` (two-host format), `speed`, `format`, `apikeyencrypted` |
| `feedtokens` | `userid`, `editionid` (nullable), `tokenhash`, `lastusedutc`, `revokedutc` |

**Models, prompts in use, search providers**

| Table | Columns |
|-------|---------|
| `modelendpoints` | `name`, `provider` (Anthropic, OpenAi, OpenAiCompatible, Ollama, Gemini, XAi, OllamaCloud, AzureOpenAi, VoyageAi, Tei), `endpoint`, `apikeyencrypted`, `apikeylast4`, `model`, `embeddingmodel`, `embeddingdimensions` (detected), `supportstools`, `supportschat`, `supportsembeddings`, `temperature` (nullable, omitted for Anthropic), `maxtokens`, `reasoningeffort`, `contextlength`, `timeoutms`, `inputcostpermtok`, `outputcostpermtok`, `lastvalidatedutc`, `lastvalidationresult` |
| `modelassignments` | `task` (section 11.2), `topicid` (nullable means tenant default), `modelendpointid`, `modeloverride` |
| `llmcalls` | `runid`, `refinementsessionid`, `briefingid`, `task`, `modelendpointid`, `model`, `prompttemplateversionid`, `inputtokens`, `outputtokens`, `durationms`, `success`, `statuscode`, `errormessage`, `structuredoutputrepairs`, `estimatedcostusd`, `prompttext`, `responsetext` |
| `searchproviders` | `name`, `kind` (Searxng, Tavily, YouCom, HostedModelWebSearch, Rss, HackerNews, Arxiv), `endpoint`, `apikeyencrypted`, `enabled`, `isdefault`, `ratelimitperminute` |

**Email**

| Table | Columns |
|-------|---------|
| `emailmessages` | `userid` (nullable), `template` (RunDigest, BriefingPublished, RunFailed, PreferenceProposal, Test), `runid`, `briefingid`, `topicid`, `recipient`, `subject`, `status` (Queued, Sending, Sent, Failed, Suppressed, Skipped), `provider` (Mailgun, File, Disabled), `providermessageid`, `attempts`, `nextattemptutc`, `lasterror`, `idempotencykey` (unique per tenant), `renderedhtml` (diagnostic copy for the dashboard preview, retention-bound), `renderedtext`, `sentutc`, `failedutc` |
| `emailactiontokens` | `emailmessageid`, `userid`, `tokenhash` (unique), `targettype`, `targetid`, `allowedactions` (`TEXT[]`), `expiresutc`, `opencount`, `submitcount`, `lastusedutc`, `revokedutc` |
| `emailpreferences` | `userid`, `runemails` (bool), `briefingemails` (bool), `failureemails` (bool), `unsubscribetokenhash`, `unsubscribedutc` |

---

## 11. Model Management with PolyPrompt

### 11.1 Endpoints

A model endpoint maps to one PolyPrompt client:

| Provider | PolyPrompt client |
|----------|-------------------|
| Ollama | `OllamaClient(endpoint)` |
| OpenAI-compatible (vLLM, LM Studio, Ollama `/v1`, OpenRouter) | `OpenAiCompatibleClient(endpoint, apiKey)` |
| OpenAI | `OpenAiClient(apiKey, endpoint)` |
| Anthropic | `AnthropicClient(apiKey, endpoint)` |
| Gemini | `GeminiClient(...)` |
| xAI | `XAiClient(...)` |
| Ollama Cloud | `OllamaCloudClient(...)` |
| Azure OpenAI | `AzureOpenAiClient(endpoint, key)` |

API keys are AES-256 encrypted at rest and shown as their last four characters. Clients are created per endpoint, cached, and disposed when the endpoint changes, all sharing one `HttpClient`.

The dashboard Models page supports create, edit, duplicate, and delete; "Test connection" (`ValidateConnectivityAsync`); "List models" (`ListModelsAsync`, shown as a picker); a capability check (`Supports(Capability)`); and a sample prompt with latency and token counts.

### 11.2 Task roles and assignments

Each pipeline task has a role. A role resolves to a model through the topic override, then the tenant default, then the first active endpoint.

| Role | Used for | Seeded assignment |
|------|----------|-------------------|
| `Refinement` | Wizard dimensions, questions, context synthesis | Anthropic `claude-opus-5-5` |
| `QueryPlanning` | Search query generation | Anthropic `claude-opus-5-5` |
| `Extraction` | Findings and evidence from documents | Anthropic `claude-opus-5-5` |
| `Scoring` | LLM relevance judgment | Anthropic `claude-opus-5-5` |
| `Synthesis` | Run summary and trends | Anthropic `claude-opus-5-5` |
| `Prediction` | Predictions and prediction reviews | Anthropic `claude-opus-5-5` |
| `Suggestion` | Dig-into suggestions | Anthropic `claude-opus-5-5` |
| `Composition` | Briefing Markdown | Anthropic `claude-opus-5-5` |
| `PodcastScript` | Audio script | Anthropic `claude-opus-5-5` |
| `FeedbackAnalysis` | Reason tagging and preference distillation | Anthropic `claude-opus-5-5` |
| `Embedding` | Vectors for dedupe and affinity | Ollama `all-minilm` |

Every chat role starts on `claude-opus-5-5`. You can reassign any role to any endpoint (for example `claude-sonnet-5-5` or `claude-haiku-5-5` for the high-volume Scoring and QueryPlanning roles, or a local model) on the Models page; nothing is downgraded automatically. Each role also carries a reasoning effort (`ReasoningEffort`), set explicitly because Opus 5.5 defaults to `medium`: `high` for Refinement, Synthesis, Prediction, Suggestion, and Composition, and `medium` for the rest.

**Anthropic request rules the gateway enforces** (current Claude models): no `temperature`, `top_p`, or `top_k` (they return a 400, so the endpoint's sampling fields are nullable and omitted for Anthropic), no assistant prefill, no forced `tool_choice` (`any` or a named tool returns a 400), thinking left at its adaptive default, `max_tokens` about 16000 for non-streaming calls, and a `refusal` stop reason handled as a typed `ModelRefusalException` (logged on `llmcalls`, optionally retried once on the role's fallback assignment).

**Tool capability.** Some local models (for example Gemma 3) do not support tool calls. Each endpoint stores `supportstools`, detected during validation with a probe call. The Models page warns when a model without tool support is assigned to a structured-output role, and those calls use the JSON fallback path in 11.4.

### 11.3 ModelGateway

`ModelGateway.ExecuteAsync<TResult>(ModelTaskRequest request, CancellationToken token)` does the following:

1. Resolves the endpoint and model for the role.
2. Renders the prompt template (section 12).
3. Builds `CompletionOptions` (or the provider-specific subclass) from endpoint settings.
4. Calls `ChatAsync`.
5. Enforces structured output (11.4).
6. Records an `llmcalls` row and telemetry.
7. Respects `Pipeline.MaxConcurrentModelCalls` with a `SemaphoreSlim`.

HTTP failures come back as `ChatResponse.Success == false` rather than exceptions. The gateway converts them into a typed `ModelCallException` carrying the status and provider error, and retries 429 and 5xx with backoff (three attempts).

### 11.4 Structured output across providers

PolyPrompt supports JSON-schema output (`TextFormat`) only on OpenAI, Azure, and xAI, and those use it. Everywhere else the gateway offers a single `ToolDefinition.Function("submit_result", ..., schema)` tool with `ToolChoice = "auto"` and a system instruction to answer only by calling `submit_result`. Forced tool choice is never used, because current Claude models reject it. The arguments are parsed with `DeserializeArguments<T>()`. During Phase 3, PolyPrompt's `AnthropicClient` is checked for `strict` tool definitions or `output_config.format`, and the gateway uses them if exposed. If the model answers in text instead of calling the tool, or the endpoint has no tool support, the fallback is "respond only with JSON matching this schema", followed by tolerant extraction and at most two repair turns ("your output failed validation: ..."). Every result type (`DimensionProposalResult`, `QuestionBatchResult`, `ExtractionResult`, `ScoringResult`, ...) is a named class with a generated JSON schema and post-parse validation that clamps scores and rejects missing fields. Repairs are counted in `llmcalls.structuredoutputrepairs` and charted per model, which also tells you which local models handle the pipeline poorly.

---

## 12. Prompt Management

Every model call uses a named, versioned prompt template that you can edit.

**Keys (defaults in `Core/Defaults/Prompts/*.md`):**

- Refinement: `refinement.dimensions`, `refinement.questions`, `refinement.followup`, `refinement.synthesize`, `refinement.sources`
- Research run: `research.queries`, `research.extract`, `research.score`, `research.dedupe-judge` (borderline similarity only), `research.synthesize`, `research.predict`, `research.review-predictions`, `research.suggest`
- Briefing and audio: `briefing.compose`, `briefing.section`, `podcast.script`
- Feedback: `feedback.tag-reasons`, `feedback.distill-preferences`
- Email: `email.run-summary` (two to four sentence lead paragraph)

**Format.** A prompt template is Markdown with YAML front matter:

```markdown
---
key: research.score
role: Scoring
output: ScoringResult
variables: [topic.brief, topic.dimensions, topic.constraints, preferences.active, feedback.examples, candidate.title, candidate.summary, candidate.source]
---
## System
You rank research items for a reader. Score strictly against the reader's stated interests and learned preferences...

## User
Topic brief:
{{topic.brief}}
...
```

**Rendering.** A small, logic-less renderer (`PromptRenderer`) substitutes `{{variable}}` placeholders from a typed `PromptContext`, plus `{{#each list}}...{{/each}}` for lists. Unknown variables fail validation at save time, following BjjWithJoel's `EmailRenderer.Validate` token check. Context blocks that can grow (history, feedback examples) are budgeted with TextChunker token counts and truncated oldest-first.

**Scope and versioning.** Tenant prompts are copied from the defaults. A topic can override any key. Every save creates a new `prompttemplateversions` row with a change note. You can diff any two versions, roll back, or reset to default. Every `llmcalls` row records the version used, so the Analytics pages can compare relevance and accuracy before and after a prompt change.

**Prompt lab (dashboard).** Pick a key and a recent run or refinement session, render the prompt with real context, run it against any model, and compare outputs side by side with the active version. This is a dry run that writes only `llmcalls` rows marked dry-run. It doubles as the benchmark "product hook" (section 28).

---

## 13. Topic Intake and the Refinement Wizard

The wizard is modeled on Pneuma's `SubjectWizard` (step rail, server-persisted draft, streamed generation with cancel, guidance plus regenerate, lock and "yours" protection, retry on another model) and Armada's backlog refinement (the LLM asks questions, the user answers, a structured summary is drafted, previewed, and applied).

**Steps:**

1. **Describe.** Free text ("I want to learn about AI developer tools"). Optionally your goal (work decisions, investing, curiosity, hiring, building), depth (beginner to expert), horizon, and anything already known. The model role is chosen here, defaulting to the `Refinement` assignment.
2. **Dimensions.** `refinement.dimensions` proposes six to twelve dimensions, each with a description, why it might matter to you, a suggested weight, and example include and exclude terms. For AI developer tools this might be: coding assistants and IDE agents, agent frameworks and orchestration, evaluation and observability, model releases relevant to coding, pricing and licensing, open-source projects, research papers, security and supply chain. You can toggle each dimension, set its weight (High, Normal, Low, Exclude), edit it, add your own, lock it, and give guidance for a regenerate ("more about local models, less about enterprise procurement").
3. **Questions.** `refinement.questions` asks five to eight questions per round, informed by the dimensions you kept. Each question has a type (single choice, multi choice, scale, yes/no, free text), a one-line "why I'm asking", and options where relevant. Examples: "Which languages and ecosystems do you work in?", "Should vendor announcements count, or only things you can try?", "Is pricing news relevant?", "Do you want academic papers, and at what depth?", "Any companies or products to always include or always ignore?" You can skip any question. "Ask me more" runs `refinement.followup` for another round (up to `MaxRounds`, default 3). The model returns `sufficientContext: true` when further questions would add little, and the UI then suggests moving on.
4. **Context.** `refinement.synthesize` drafts the topic brief (Markdown), include and exclude constraints (keywords, entities), preferred and blocked domains, tracked questions ("When will X ship Y?"), audience level, novelty preference, and seed queries. Every item can be edited, locked, or removed. Locked items survive later regeneration.
5. **Sources.** Enable search providers for the topic. `refinement.sources` suggests RSS or Atom feeds and seed URLs, and each suggestion is validated by a live fetch before you can accept it (a green or red badge with the HTTP status).
6. **Delivery.** Research schedule (section 14), which editions include the topic, email on every run (default on, with recipients defaulting to you), optional topic-specific briefing template, and model assignment overrides.
7. **Review and preview.** A summary of everything captured, plus "Preview run": a dry run of the full pipeline that shows sample ranked findings without emailing or counting toward history. You can rate preview findings immediately. Those ratings are kept and seed the topic's feedback history. Then create the topic.

**Persistence.** Every step writes to `refinementsessions`, `refinementmessages`, `refinementquestions`, and `refinementquestionoptions` as you go, so closing the browser loses nothing and the session resumes. Generation streams progress over SSE (`GET /v1.0/api/refinement-sessions/{id}/events`, consumed through `fetch` streaming so the bearer header is sent). Failures show the error with "retry" and "retry with another model", following Pneuma's `WizardFailurePanel`.

**Refine again.** Any existing topic can reopen the wizard. The new session starts from the current context plus a feedback digest, and the model leads with questions grounded in your history, for example: "You marked 9 of the last 12 funding stories as not relevant. Should I exclude funding news entirely?" Changes are applied as a diff you review, and the topic keeps a context change history (each apply records a `topics.lastrefinedutc` change and an audit entry).

---

## 14. Scheduling

### 14.1 Interval types (`IntervalTypeEnum`)

| Type | Parameters | Example |
|------|-----------|---------|
| `EveryNMinutes` | `intervalvalue` (clamped to `Scheduler.MinimumIntervalMinutes` (default 5) through 1440) | Every 30 minutes |
| `EveryNHours` | `intervalvalue` (1 to 168), optional minute offset | Every 4 hours at :15 |
| `EveryNDays` | `intervalvalue` (1 to 365), one or more times of day | Every 2 days at 07:00 and 18:00 |
| `EveryNWeeks` | `intervalvalue` (1 to 52), days of week, times of day | Every 2 weeks on Monday and Thursday at 08:00 |
| `DaysOfWeek` | days of week, times of day | Weekdays at 06:30 |
| `DaysOfMonth` | days (1 to 31, with "last day" as -1), times of day | 1st and 15th at 09:00 |
| `Once` | `runonceutc` | Once on 2026-12-01 at 10:00 |
| `Cron` | `cronexpression` (Cronos syntax, advanced) | `0 */6 * * 1-5` |
| `Manual` | none | Only "Run now" |

Every schedule has an IANA time zone (defaulting to the user's browser time zone, captured at creation), optional start and end dates, and a missed-run policy (`Skip` or `RunOnceOnRecovery`). Daylight-saving transitions are handled by computing in local time and converting with `TimeZoneInfo`. A nonexistent local time moves forward and an ambiguous one takes the first occurrence. Both cases are tested.

### 14.2 Engine

- `ScheduleCalculator` is pure and unit-tested: `NextOccurrence(schedule, afterUtc)` and `NextOccurrences(schedule, afterUtc, count)`. The dashboard shows "Next 5 runs" live as you edit, from `POST /v1.0/api/schedules/preview`.
- `SchedulerWorker` polls every `PollIntervalSeconds`. It claims due schedules with `UPDATE schedules SET lockeduntilutc = ... WHERE id IN (SELECT id FROM schedules WHERE enabled AND nextrunutc <= now() AND (lockeduntilutc IS NULL OR lockeduntilutc < now()) FOR UPDATE SKIP LOCKED LIMIT n) RETURNING ...`, enqueues a `runs` row (or an edition composition), and advances `nextrunutc`.
- `RunWorker` claims queued runs the same way, bounded by `Pipeline.MaxConcurrentRuns`, and records a `queued` duration for telemetry while a run waits for a slot.
- A topic never has two overlapping runs. A due schedule whose previous run is still active is recorded as skipped with a reason.
- Editions with `freshresearchbeforecompose` enqueue their topics' runs `freshresearchleadminutes` ahead of composition.
- Controls: "Run now", "Dry run", "Cancel" (cooperative through `cancelrequested` plus the linked `CancellationToken`), "Pause topic", "Resume", "Retry from stage".

---

## 15. Search and Fetch Providers

There is no cross-provider web search library in `~/Code`, so search is pluggable behind `ISearchProvider.SearchAsync(SearchRequest request, CancellationToken token)`:

| Kind | Implementation | Keys | Notes |
|------|----------------|------|-------|
| `Searxng` | HTTP client to the bundled `searxng` container (JSON format) | none | Default. Self-hosted meta-search, so no API key is needed to get started. |
| `Tavily` | `Mux.Search` if it is published as a NuGet package, otherwise a small direct client | API key | Higher quality. Optional. |
| `YouCom` | Same as Tavily | API key | Optional |
| `HostedModelWebSearch` | PolyPrompt `ResponsesApiTools.WebSearch()` on OpenAI, Azure, or xAI endpoints, with results taken from `ChatResponse.Citations` | the model key | Optional, and only offered when the endpoint supports it |
| `Rss` | `System.ServiceModel.Syndication` over topic `FeedUrl` constraints | none | No RSS library exists in `~/Code` |
| `HackerNews` | Algolia HN search API | none | Optional |
| `Arxiv` | arXiv Atom API | none | Optional, useful for research dimensions |

**Fetch.** `IFetchService` uses CrawlSharp `WebCrawler` in single-page mode (`FollowLinks = false`, `IncludeSitemap = false`, robots respected, per-domain delay, timeout, size cap). The optional headless mode is off by default because Playwright adds hundreds of megabytes to the image. HTML is converted to Markdown with DocConverter and then trimmed of navigation boilerplate. Canonical URLs are normalized (scheme, host case, tracking parameters stripped, trailing slash) and hashed (SHA-256) for dedupe.

---

## 16. The Research Pipeline

One run executes these stages in order. Each stage is a class implementing `IRunStage`, records a `runstages` row, opens a `stage:<Name>` span, observes `mymorningnews_run_stage_duration_seconds{stage}`, and can be retried from its position because its inputs are persisted.

| # | Stage | What it does | Persists |
|---|-------|--------------|----------|
| 1 | `AssembleContext` | Builds the `PromptContext`: brief, dimensions, constraints, active learned preferences, last N run summaries, open predictions, recent suggestions and their statuses, top relevant and not-relevant feedback examples with reasons, and seen-URL hashes. Hashes the snapshot. | `runs.contextsnapshothash` |
| 2 | `PlanQueries` | `research.queries` generates up to `MaxQueriesPerRun` queries spread across dimensions by weight. Excluded dimensions and exclude constraints are passed as negatives. Tracked questions get their own queries. | `searchqueries` |
| 3 | `Search` | Runs each query against the topic's providers in parallel (rate-limited). Adds RSS items since the last run. | `searchresults` |
| 4 | `Filter` | Drops blocked domains, results already seen in this topic (by URL hash, recorded as `SkippedSeen`), robots-disallowed URLs, and overflow beyond `MaxFetchesPerRun`, prioritizing preferred domains and provider rank. | `searchresults.disposition` |
| 5 | `Fetch` | Fetches and extracts text. Existing `sourceitems` rows are reused when the content hash is unchanged. | `sourceitems` |
| 6 | `Extract` | `research.extract` returns candidate findings per document, each with title, summary, why it matters, dimension, category, and verbatim supporting quotes. Quotes are verified against the source text (`evidence.verified`). Unverifiable quotes are dropped, and a candidate with no verified evidence is discarded. | `evidence`, candidate findings |
| 7 | `Embed` | Embeds candidates (title plus summary). | `embeddings` |
| 8 | `Dedupe` | Cosine similarity against this topic's findings from the last 90 days. At or above `DuplicateSimilarity`, the candidate is marked duplicate. In the borderline band (0.85 to 0.92), `research.dedupe-judge` decides between duplicate and update. An update links to the prior finding through `isupdateoffindingid`. | `findings.isduplicate` and related links |
| 9 | `Score` | `research.score` produces the LLM relevance score and rationale, and the ranker computes the composite score (section 17), the tier, and the rank. | `findings` scores |
| 10 | `Synthesize` | `research.synthesize` writes the run summary: what changed, themes, and how this run relates to earlier ones. | `runs.summary` |
| 11 | `ReviewPredictions` | For open predictions in this topic (and any past horizon), `research.review-predictions` checks the new evidence and proposes a status with rationale and evidence. A proposal is never final until you confirm it in the dashboard or from an email link, unless `AutoResolvePredictions` is on for the topic. | `predictionreviews`, `predictionevidence` |
| 12 | `Predict` | `research.predict` writes zero to five new predictions, each with a falsifiable statement, confidence, horizon, and linked evidence. A new prediction is checked for near-duplicates against open predictions. | `predictions` |
| 13 | `Suggest` | `research.suggest` writes up to five dig-into suggestions linked to findings, avoiding suggestions you dismissed or completed before. | `suggestions` |
| 14 | `ComposeRunBriefing` | Optional per topic. A single-run briefing with the topic template. | `briefings` |
| 15 | `Notify` | Enqueues the run digest email to each topic recipient (section 23), even when nothing new was found, if `emailwhenempty` is set. | `emailmessages` |

A failure in a non-critical stage (Predict, Suggest, ComposeRunBriefing) marks the run `PartiallySucceeded` and continues. A failure in a critical stage (Search with zero results from every provider, Fetch, or Extract) fails the run, and the failure email goes to tenant admins if they have opted in. Run progress streams over SSE at `GET /v1.0/api/runs/{id}/events` and drives a live stage timeline in the dashboard.

---

## 17. Relevance Ranking

Every finding gets a composite relevance score from 0 to 100. The components are stored separately so the dashboard can explain each score and the calibration charts can show which signal is helping.

| Component | Source | Default weight |
|-----------|--------|----------------|
| `llmscore` | `research.score` judgment against brief, dimension weights, constraints, and learned preferences | 0.45 |
| `feedbackscore` | Similarity-weighted vote over the k = 10 nearest rated findings in the topic (pgvector cosine): Relevant counts +1, Somewhat +0.5, Not relevant -1, mapped to 0 to 100, with a neutral 50 when there are fewer than 3 neighbors | 0.20 |
| `noveltyscore` | 100 × (1 - maximum similarity to findings in the last 30 days), scaled by the topic's novelty preference | 0.15 |
| `sourcescore` | Bayesian average of your ratings for the domain (prior 50, prior weight 5), with a penalty for preferred and blocked lists already applied | 0.10 |
| `recencyscore` | Exponential decay on published date, half-life `RecencyHalfLifeHours` (default 72) | 0.10 |

`relevancescore = clamp(sum(weight × component))`. Tiers: Must read (80 and up), Worth a look (60 to 79), FYI (40 to 59), Below threshold (under the topic's `displaythreshold`, hidden by default but retained). Weights can be overridden per topic.

**Calibration and tuning.** The Relevance analytics page plots the predicted score bucket against the share you rated relevant, overall and per component, before and after weight or prompt changes. Every 50 new ratings, `RankingTuner` fits a regularized logistic regression of your Relevant/Not relevant labels on the five components and proposes new weights with the expected improvement. Proposed weights are shown for you to accept, and are never applied automatically.

---

## 18. History, Reinforcement, and Learned Preferences

History feeds back into the pipeline in five places:

1. **Dedupe.** Seen URLs are skipped outright, and semantically similar findings are suppressed or linked as updates. The "items examined" history is the `searchresults` and `sourceitems` tables, browsable on the Sources page with disposition.
2. **Ranking.** Feedback affinity and source reputation (section 17).
3. **Prompt context.** Stage 1 includes a feedback digest: up to 5 relevant and 5 not-relevant exemplars, each with its "why", the active learned preferences, and the status of recent suggestions ("you completed X; you dismissed Y as too basic").
4. **Learned preferences.** After every 5 new feedback items on a topic, and nightly, `LearningWorker` runs two passes:
   - `feedback.tag-reasons` tags free-text reasons into reusable themes (`feedbackreasontags`).
   - `feedback.distill-preferences` proposes preference statements with supporting feedback links and confidence.

   Proposals land as `Proposed` and appear in the dashboard and in the next email ("It looks like you don't want vendor funding news. Apply or dismiss?"), each with a one-click link. Accepted preferences become `Active` and are injected into prompts. You can always write your own, disable one, or delete it. `AutoApplyLearnedPreferences` per topic (default off) skips the approval step.
5. **Prediction track record.** The model sees its own accuracy on the topic ("past predictions on this topic: 7 accurate, 3 inaccurate, overconfident above 0.8") and is asked to calibrate.

---

## 19. Evidence Tracking

- Every finding, prediction, prediction review, and suggestion links to evidence. Every evidence row is a verbatim quote verified against the stored source text, with offsets, the surrounding paragraph, the URL, the fetch time, and the content hash. If a source changes later, the stored text is still what the claim was based on.
- Briefing Markdown cites evidence with numbered references (`[1]`) that resolve to a Sources section with title, domain, date, and link. The dashboard reader shows the quote on hover.
- The Evidence view of a finding shows the claim, each supporting or contradicting quote highlighted in its source context, and every run that touched that source.
- Feedback on evidence is supported (target type `SourceItem` or `Domain`), so "this site is unreliable" adjusts the source score and can become a learned preference.
- `evidence.verified = false` items are never shown as support. The share of extracted quotes that fail verification is charted per extraction model, because hallucinated evidence is the most important quality signal to watch.

---

## 20. Accuracy and Relevance Tracking

**Predictions lifecycle.** Open, then ProposedResolved (agent review or horizon passed), then Accurate, PartiallyAccurate, Inaccurate, or Unresolvable (by you, or by the agent if auto-resolve is on), or Withdrawn. When a horizon passes with no resolution, the prediction surfaces in the next email and on the Accuracy page as "due for review".

Metrics, each stored or computed with a SQL view and exposed through `AnalyticsRoutes`:

- Accuracy rate (accurate plus half of partially accurate, over resolved predictions) by topic, dimension, model, and prompt version, over time.
- Brier score per prediction and averaged, plus a calibration curve (confidence bucket against observed accuracy).
- Resolution latency (horizon to resolution) and the share unresolvable.
- Agent-vs-user agreement: how often you accept the agent's proposed resolution.
- Relevance precision: the share of shown findings rated Relevant or Somewhat, by topic, tier, dimension, source domain, model, and prompt version, over time.
- Feedback volume and coverage (the share of findings that received any rating), by channel (dashboard or email).
- Suggestion outcomes: accepted, done, dismissed, and snoozed rates and time to decision.
- Reason themes over time (from `feedbackreasontags`).

The Feedback page lists every feedback item with its reason, target, channel, and the preferences it contributed to, with filters, and lets you edit or retract feedback. The audit history of a feedback item shows how its effect flowed into preferences, ranking, and later runs.

---

## 21. Briefings, Templates, and Editions

### 21.1 Briefing template format

A briefing template is a Markdown file you write. Front matter sets options, and each `##` section is either a fixed heading with instructions to the composer or a data-driven section. Specified in `docs/BRIEFING_TEMPLATES.md`:

```markdown
---
name: Morning News
tone: direct, skeptical, no hype
length: medium            # short | medium | long
audience: practitioner
citations: numbered       # numbered | inline | none
audio:
  enabled: true
  format: two-host        # narrator | two-host
  targetMinutes: 8
  intro: "Good morning. Here is what moved in your topics."
---

# {{edition.name}} for {{date.long}}

## Top of the news
<!-- mmn:section source=findings tier=MustRead limit=5 order=relevance -->
Lead with the single most important development. One paragraph per item.
Say why it matters to me specifically, using my topic briefs.

## By topic
<!-- mmn:section source=findings group=topic tier=WorthALook limit=4 -->
Two sentences per item.

## Where this might go
<!-- mmn:section source=predictions status=new limit=5 -->
State each prediction, its confidence, and what would change my mind.

## Predictions to grade
<!-- mmn:section source=predictions status=due limit=5 -->

## Dig into next
<!-- mmn:section source=suggestions status=New limit=5 order=priority -->

## Sources
<!-- mmn:section source=citations -->
```

Directive attributes include `source` (findings, predictions, suggestions, citations, run-summaries, or none for free-form composer text), `tier`, `status`, `group`, `limit`, `order`, `topic`, and `dimension`. Text under a heading is passed to the composer as that section's instruction. The template is validated on save: front matter schema, known directives, and known variables. Errors are reported per line, and a live preview renders against the latest real data.

### 21.2 Composition

`BriefingComposer` selects items per section by directive, then calls `briefing.section` per section (bounded parallel), then `briefing.compose` for the opening and transitions. It assembles Markdown with resolved citations and stores both the briefing and its sections. Single-topic run briefings use the topic template, or the default "Run Briefing" template.

### 21.3 Editions

"Morning News" is only a seeded starting point: you can edit its schedule, topics, template, voice, and delivery, or delete it, and you can create any number of your own editions, each with its own custom schedule (any interval type in section 14).

An edition has a schedule, topics, a template, a voice, an audio toggle, an email toggle, and a look-back window. "Morning News" is seeded. Compose-now and preview are available in the dashboard. The briefing reader renders Markdown (sanitized, after Armada's `Markdown.tsx` with `rehype-sanitize`), has inline feedback controls per item and section, shows the audio player with chapters, and offers downloads as `.md` (raw) and `.html` (rendered with HtmlPageBuilder).

---

## 22. Audio and Podcast Output

There is no text-to-speech in `~/Code` (Polyvox is speech-to-text), so TTS is pluggable behind `ITtsProvider.SynthesizeAsync(TtsRequest request, CancellationToken token)`:

| Provider | Notes |
|----------|-------|
| `OpenAiCompatibleSpeech` (default) | `POST {endpoint}/audio/speech`. Works with the bundled Kokoro-FastAPI container (local, CPU, free), OpenAI TTS, and other compatible servers. |
| `ElevenLabs` | Optional, Phase 12 or later |
| `Disabled` | No audio |

**Flow (MediaWorker):**

1. `podcast.script` turns the briefing into a spoken script following the template's `audio` block. Narrator mode gives a single voice. Two-host mode alternates `HOST_A:` and `HOST_B:` lines. Citations become spoken attributions, and URLs are never read aloud.
2. The script is split into segments at sentence boundaries under the provider's input limit.
3. Each segment is synthesized, with bounded parallelism and retries.
4. Segments are concatenated and loudness-normalized with `ffmpeg` (installed in the server image).
5. Chapters are recorded per briefing section.
6. The MP3 is stored on the `media` volume and the `mediaassets` row is updated.

**Playback.** The dashboard player follows Auralytic's `CallReviewView` pattern: an authenticated fetch to a blob URL, native controls, chapter list, and the current section highlighted in the Markdown. Audio routes support HTTP `Range` requests.

**Podcast feed.** Per user and per edition, the user can create a private feed URL (`/v1.0/public/feeds/{token}/podcast.xml`, RSS 2.0 with iTunes tags, artwork from `assets/glyph.png`). Podcast apps can subscribe to it. Enclosure URLs carry the same token. Feed tokens are hashed at rest, revocable, and audited.

---

## 23. Email Delivery Component

An email goes out after every iteration. Each research run sends a run digest to the topic's recipients, and each edition sends its briefing. Every email carries deep links to the details and feedback links for each item. The implementation follows the house patterns:

- **Xeno** (`~/Code/Xeno/src/Xeno.Email`): the provider abstraction (`IEmailProvider` with `MailgunEmailProvider`, `FileEmailProvider`, `DisabledEmailProvider`, and `EmailProviderFactory`), `EmailMessage` and `EmailSendResult` models, `EmailTemplateBase<TContext>` rendering through HtmlPageBuilder with every value encoded exactly once, `EmailTemplateTheme`, and `EmailRedactionHelpers`.
- **BjjWithJoel** (`~/Code/BjjWithJoel/trelliskit/backend/src`): the queued outbox (`EmailMessageRecord` with status, attempts, `NextAttemptUtc`, `LastError`, and `IdempotencyKey`, delivered and retried with backoff by a worker); `MailgunEmailSender` over `MailgunSender.SendAsync` with message-id normalization and typed failures; `EmailRenderer`, `EmailComposer` blocks (paragraph, subheading, Markdown, fields, callout, notice, list, button, divider), and `EmailMarkdown` (a safe Markdown subset); and `StandardEmailLayout`, which is HtmlPageBuilder, inline-styled, max 600 px, readable at 360 px, dark-mode aware through `color-scheme` meta and a `prefers-color-scheme` block, with a hidden preheader and a matching plain-text rendering.
- **Mux** has no SendWithMailgun or HtmlPageBuilder usage. Its only email artifact is a third-party React Email skill pack, which does not fit a C# backend, so nothing is taken from Mux for email (Q5).

### 23.1 Components (in `MyMorningNews.Core/Email/` and `MyMorningNews.Server/Workers/`)

| Class | Responsibility |
|-------|----------------|
| `IEmailProvider` | `Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token)`, plus `IsConfigured` |
| `MailgunEmailProvider` | SendWithMailgun `MailgunSender(domain, apiKey, baseUrl)`. Sends the HTML body, because SendWithMailgun sends one body format per message (the BjjWithJoel note), and normalizes the provider message ID |
| `FileEmailProvider` | Writes `<eml_id>.html` and `<eml_id>.txt` to the `emailout` volume. This is the default in Docker until Mailgun is configured, so the loop works out of the box and you can open the emails locally |
| `DisabledEmailProvider` | Records the message as `Skipped` |
| `EmailProviderFactory` | Builds the provider from settings, re-read live on change |
| `EmailTemplateBase<TContext>` | HtmlPageBuilder rendering of shared chrome (logo from `PublicServerBaseUrl/v1.0/public/assets/logo.png`, footer, preferences link) |
| `RunDigestEmailTemplate` | Section 23.2 |
| `BriefingPublishedEmailTemplate` | Briefing lead, section headings with their top items, audio link, the same per-item feedback links |
| `RunFailedEmailTemplate` | For opted-in admins |
| `PreferenceProposalEmailTemplate` | Folded into the digest when proposals exist; a standalone template only for "send now" |
| `TestEmailTemplate` | "Send test email" from Settings |
| `EmailComposer` / `EmailBlock` | BjjWithJoel block set, extended with `FindingBlock`, `PredictionBlock`, `SuggestionBlock`, and `FeedbackButtonRowBlock` |
| `EmailMarkdown` | Safe Markdown subset to HTML for run summaries and briefing text |
| `EmailService` | `QueueRunDigestAsync(run, ct)`, `QueueBriefingAsync(briefing, ct)`, `RenderPreviewAsync(...)`, `SendTestAsync(...)`. Builds per-recipient messages and action tokens, enforces idempotency (`run:{runId}:{recipient}`), honors preferences and suppression, and never throws to the pipeline |
| `EmailDeliveryWorker` | Claims due `Queued` messages (`SKIP LOCKED`), sends, and retries with exponential backoff up to `DeliveryMaxAttempts` (default 5) |
| `EmailActionTokenService` | Issues, validates, and revokes action tokens (section 23.4) |
| `PublicFeedbackRoutes` | Landing pages rendered with HtmlPageBuilder (section 23.5) |

The SendWithMailgun meter and source (`MailgunTelemetry.MeterName`) are subscribed in Radiant. Application counters `mymorningnews_email_messages_total{template,provider,outcome}` and `mymorningnews_email_feedback_clicks_total{targettype,action}` sit alongside them.

### 23.2 Run digest email contents

- **Subject.** `[My Morning News] {topic}: {n} new ({mustRead} must-read)`, or `{topic}: nothing significant this run` when there is nothing new. The subject format can be changed in Settings with validated tokens, using the BjjWithJoel override pattern.
- **Preheader.** The title of the top finding.
- **Header.** Logo, topic name, run time in your time zone, and a "View this run" button linking to `{PublicDashboardBaseUrl}/dashboard/runs/{runId}`.
- **Summary.** The `email.run-summary` lead paragraph.
- **Top findings.** Up to `maxemailfindings`, ordered by rank. Each finding shows:
  - rank, a tier badge, and the score (for example "Must read · 87")
  - the title, linking to `/dashboard/findings/{findingId}`
  - a two-sentence summary and "why it matters"
  - source chips (domain and date) linking to the original URLs
  - a feedback row: **Relevant**, **Somewhat**, **Not relevant**, and **Tell us why**
- **Where it might go.** New predictions, each with confidence, horizon, a details link, and **Relevant** and **Not relevant** links.
- **Predictions to grade.** Agent-proposed resolutions and predictions past their horizon, each with the proposed status and rationale and the links **Accurate**, **Partially**, **Inaccurate**, **Too early**, and **Tell us why**.
- **Dig into next.** Suggestions with priority and effort, and the links **Useful**, **Not useful**, **Done**, and **Snooze a week**.
- **Learned preference proposals,** if any, with **Apply** and **Dismiss** links.
- **Footer.** Links to the full run, today's briefing, email preferences, and one-click unsubscribe from run emails for this topic.
- **Plain-text rendering** with the same links, stored for preview. SendWithMailgun sends one format, so the HTML is what is transmitted.

### 23.3 Links

- Detail links point at the dashboard (`Email.PublicDashboardBaseUrl`, default `http://127.0.0.1:12101`). The dashboard routes `/dashboard/runs/{id}`, `/dashboard/findings/{id}`, `/dashboard/predictions/{id}`, and `/dashboard/suggestions/{id}` deep-link after login. The return path is preserved through the login redirect.
- Feedback links point at the server's public pages (`Email.PublicServerBaseUrl`, default `http://127.0.0.1:12100`): `/v1.0/public/feedback/{token}?a=relevant`.
- Both base URLs are settings, because links in an email only work if the recipient's device can reach them. The README covers this for remote access, for example behind a reverse proxy or a VPN.

### 23.4 Action tokens and safety

- One token per item per email, covering that item's allowed actions. Each token is a 32-byte random value (URL-safe Base64). Only its SHA-256 hash is stored, in `emailactiontokens`, bound to tenant, user, email message, target type, target ID, and allowed actions. Tokens expire after `Email.ActionTokenLifetimeDays` (default 30) and can be revoked.
- **Link scanners.** Mail security scanners (Outlook Safe Links, Gmail, corporate gateways) follow GET links automatically, so a GET never records feedback by default. A GET opens a landing page with your choice preselected, the item summary, a "why" text box, and one **Submit** button (POST). Setting `Email.RecordFeedbackOnClick` (default false) records the rating on GET for people who prefer a true one-click flow and accept the scanner risk. Opens are counted separately from submissions so the analytics stay honest.
- Submissions create a `feedback` row with `channel = Email` and `emailmessageid`. A later submission through the same token supersedes the earlier one while keeping history, and the newest rating wins in metrics.
- Public routes are rate-limited per token and per IP. Responses are uniform for unknown, expired, and revoked tokens (no enumeration). Every submission is audited, and a token can only act on its bound target. A tenant or user that is deactivated invalidates its tokens.

### 23.5 Landing pages (server-rendered with HtmlPageBuilder)

- `GET /v1.0/public/feedback/{token}?a={action}` shows the branded page: logo, item title and summary, the action as a selected segmented control (changeable), the "Why?" text area (optional, 2000 characters), Submit, and a link to open the item in the dashboard.
- `POST /v1.0/public/feedback/{token}` records the feedback and shows a thank-you page with a "change my answer" option, a link to the run, and, when the reason contained text, a note that it will help tune future runs.
- `GET /v1.0/public/unsubscribe/{token}` and `POST` turn off one email category, with an undo link.
- The pages are self-contained HTML with inline CSS, light and dark (`prefers-color-scheme`), usable at 360 px, with no JavaScript required. Strings come from a server-side catalog keyed by the recipient's preferred locale, falling back to English (section 25.7).

### 23.6 Email settings

| Setting | Default |
|---------|---------|
| `Email.Enabled` | true |
| `Email.Provider` | `File` (`Mailgun`, `File`, `Disabled`) |
| `Email.FromAddress` / `FromName` / `ReplyToAddress` | `briefings@example.test` / `My Morning News` / none |
| `Email.SubjectPrefix` | `[My Morning News]` |
| `Email.Mailgun.Domain`, `ApiKey` (env `MYMORNINGNEWS_MAILGUN_API_KEY`), `BaseUrl` | blank, blank, `https://api.mailgun.net/v3/` (EU: `https://api.eu.mailgun.net/v3/`) |
| `Email.Mailgun.ValidateRecipients` | false (when true, uses `MailgunValidator` on recipient add) |
| `Email.File.Directory` | `/data/email` |
| `Email.PublicDashboardBaseUrl` / `PublicServerBaseUrl` | `http://127.0.0.1:12101` / `http://127.0.0.1:12100` |
| `Email.ActionTokenLifetimeDays` | 30 (clamp 1 to 365) |
| `Email.RecordFeedbackOnClick` | false |
| `Email.DeliveryMaxAttempts` / `DeliveryIntervalSeconds` | 5 / 15 |
| `Email.MinimumMinutesBetweenRunEmails` | 0 (send every iteration). Set above 0 to coalesce emails for high-frequency topics into one digest. |
| `Email.MessageRetentionDays` | 90 |

### 23.7 Dashboard surfaces for email

- **Settings, then Email:** provider, sender, Mailgun keys (masked, set or not set), base URLs, token lifetime, and "Send test email".
- **Email Outbox:** a server-side table (time, template, recipient, topic, status, attempts, provider ID), with row actions View (rendered HTML preview in a modal, sandboxed iframe), Resend, View JSON, and Delete. Bulk delete by filter. Charts of messages per day by status, and feedback submissions per email by action.
- **Topic, then Delivery:** email on run, send when empty, recipient list, and maximum findings.
- **Profile, then Email preferences:** categories, unsubscribe state, and active feed and action tokens with revoke.
- **Email preview:** render the run digest for any past run, exactly as it would be sent, with sample tokens.

---

## 24. REST API, SDKs, and MCP Surface

Every route is registered with `OpenApiRouteMetadata` (summary, tag, request and response types) so `/openapi.json` drives the API Explorer. `REST_API.md` documents every endpoint (method, path, parameters, bodies, status codes, auth, examples). `assets/postman/MyMorningNews.postman_collection.json` mirrors it with folders by resource, collection-, folder-, and request-level descriptions, and variables for `baseUrl`, `port`, `tenantId`, `email`, `password`, `token`, and `adminApiKey`. A login request stores the token automatically.

List endpoints share one contract: `pageNumber`, `pageSize`, `sortBy`, `sortDirection`, `search`, typed filters per resource, and `fromUtc` / `toUtc`. The response is `{ items, pageNumber, pageSize, totalCount, totalPages, durationMs }`. Errors are `{ error, message, requestId, details }`.

| Group | Routes (abbreviated) |
|-------|---------------------|
| Health | `GET /v1.0/api/health` (anonymous; includes database, workers, search, TTS, and email provider status) |
| Token | `POST/GET/DELETE /v1.0/token`, `POST /v1.0/token/refresh`, `GET /v1.0/token/details` |
| Tenants (control plane) | `/v1.0/tenants`, `/v1.0/tenants/{tenantId}` and nested `users`, `credentials`, `roles`, `permissions`, `assignments`, `users/{id}/permissions`, `credentials/{id}/permissions` |
| Audit | `GET/DELETE /v1.0/api/audit`, `GET/DELETE /v1.0/api/audit/{id}` |
| Request history | exactly per `BACKEND_ARCHITECTURE.md` at `/v1.0/api/request-history` |
| Settings | `GET/PUT /v1.0/api/settings`, `POST /v1.0/api/settings/restart`, `GET /v1.0/api/server-info` |
| Models | `/v1.0/api/model-endpoints` (CRUD), `POST .../{id}/validate`, `GET .../{id}/models`, `POST .../{id}/test`, `/v1.0/api/model-assignments` |
| Prompts | `/v1.0/api/prompts` (CRUD by key and scope), `GET .../{id}/versions`, `POST .../{id}/rollback`, `POST .../{id}/reset`, `POST /v1.0/api/prompts/lab` |
| Topics | `/v1.0/api/topics` (CRUD), `/topics/{id}/dimensions`, `/constraints`, `/recipients`, `/search-providers`, `POST /topics/{id}/run`, `POST /topics/{id}/dry-run`, `POST /topics/{id}/pause`, `POST /topics/{id}/resume` |
| Refinement | `POST /v1.0/api/refinement-sessions`, `GET .../{id}`, `POST .../{id}/dimensions/generate`, `POST .../{id}/questions/generate`, `PUT .../{id}/questions/{qid}`, `POST .../{id}/context/generate`, `POST .../{id}/sources/suggest`, `POST .../{id}/apply`, `GET .../{id}/events` (SSE) |
| Schedules | `/v1.0/api/schedules` (CRUD), `POST /v1.0/api/schedules/preview` |
| Runs | `GET /v1.0/api/runs`, `GET .../{id}`, `GET .../{id}/stages`, `GET .../{id}/queries`, `GET .../{id}/llm-calls`, `GET .../{id}/events` (SSE), `POST .../{id}/cancel`, `POST .../{id}/retry?fromStage=` |
| Findings | `GET /v1.0/api/findings`, `GET .../{id}` (with evidence and score breakdown), `PUT .../{id}` (pin or hide), `DELETE` |
| Sources and evidence | `GET /v1.0/api/sources`, `GET .../{id}`, `GET /v1.0/api/evidence/{id}` |
| Predictions | `GET /v1.0/api/predictions`, `GET .../{id}`, `POST .../{id}/resolve`, `POST .../reviews/{id}/accept`, `POST .../reviews/{id}/reject` |
| Suggestions | `GET /v1.0/api/suggestions`, `PUT .../{id}/status` |
| Feedback | `POST /v1.0/api/feedback`, `GET /v1.0/api/feedback`, `PUT/DELETE .../{id}` |
| Preferences | `/v1.0/api/learned-preferences` (CRUD), `POST .../{id}/accept`, `POST .../{id}/reject` |
| Briefings and editions | `/v1.0/api/editions` (CRUD), `POST .../{id}/compose`, `POST .../{id}/preview`, `GET /v1.0/api/briefings`, `GET .../{id}`, `GET .../{id}/markdown`, `GET .../{id}/html` |
| Templates | `/v1.0/api/briefing-templates` (CRUD and versions), `POST .../validate`, `POST .../preview`, `POST .../import`, `GET .../{id}/export` |
| Media | `GET /v1.0/api/media/{id}` (Range), `POST /v1.0/api/briefings/{id}/audio` (regenerate), `/v1.0/api/voices` (CRUD and test) |
| Search providers | `/v1.0/api/search-providers` (CRUD), `POST .../{id}/test` |
| Email | `GET /v1.0/api/email/messages`, `GET .../{id}`, `POST .../{id}/resend`, `DELETE`, `POST /v1.0/api/email/test`, `POST /v1.0/api/email/preview`, `GET/PUT /v1.0/api/email/preferences` |
| Feeds | `/v1.0/api/feed-tokens` (CRUD); public `GET /v1.0/public/feeds/{token}/podcast.xml`, `GET /v1.0/public/feeds/{token}/media/{id}` |
| Public feedback | `GET/POST /v1.0/public/feedback/{token}`, `GET/POST /v1.0/public/unsubscribe/{token}`, `GET /v1.0/public/assets/{name}` |
| Analytics | `GET /v1.0/api/analytics/{overview, runs, relevance, accuracy, calibration, feedback, reasons, suggestions, sources, usage, models, prompts, email}` with `fromUtc`, `toUtc`, `bucketMinutes`, `topicId`, and `modelEndpointId`. Every time series is gap-filled server-side. |

### 24.1 SDKs

Three SDKs live under `sdk/`, following Verbex's layout (`~/Code/Verbex/sdk`): `sdk/csharp` (`MyMorningNews.Sdk` class library plus `MyMorningNews.Sdk.TestHarness` console app, in `MyMorningNews.Sdk.sln`), `sdk/js` (`mymorningnews-sdk.js` ES module plus `test-harness.js`, Node 20+, Fetch only), and `sdk/python` (`mymorningnews_sdk.py` plus `test_harness.py`, Python 3.10+, `requests`). `sdk/README.md` indexes them. The SharpAI C# SDK harness (`~/Code/SharpAI/SharpAI/sdk/csharp`) is the model for test structure: explicit API details, environment setup, helpers, a base test class, and individual cases.

- **Coverage.** Token login (headers) and logout; topics (CRUD, dimensions, constraints, run now, dry run); refinement sessions (start, generate, answer, apply); schedules and preview; runs (list, get, stages, cancel, and SSE event streaming in C# and JS); findings, sources, and evidence; predictions (list, resolve); suggestions (status); feedback (submit with reason); learned preferences; editions and briefings (Markdown and HTML, and audio download); templates and prompts (get, update, versions); model endpoints (CRUD, validate); email messages (list, preview, test); analytics series; health. Control-plane admin calls are in the C# SDK only.
- **Conventions.** Default base URL `http://127.0.0.1:12100`, never `localhost` (`REPOSITORY_REQUIREMENTS.md` item 7). Typed models mirror the REST DTOs. Errors are typed exceptions with status, error code, message, and request ID. Every async C# method takes a `CancellationToken`. The C# SDK follows `CODE_STYLE.md` in full.
- **Harnesses.** Each harness runs against a live server, creates its own topic and data, and deletes them afterward. It uses the stub model, search, and TTS servers when started with `--stubs` and exits non-zero on any failure. Each language has `run-tests.sh` and `run-tests.bat`.
- **CI.** The `sdk` job in `tests.yml` starts Postgres as a service, launches the server with stub endpoints, runs all three harnesses, and uploads their results with `if: always()`.

### 24.2 MCP server

`src/MyMorningNews.McpServer` is a separate executable built on Voltaic 2.4 (`~/Code/Voltaic`), as LiteGraph (`LiteGraph.McpServer`) and Verbex (`Verbex.Mcp`) do. It serves Streamable HTTP on `/mcp` (container port 8100, host port 12105, compose service `mcp` with a cURL healthcheck on its health endpoint and `depends_on: server: service_healthy`) and stdio mode (`--stdio`) for local clients such as Claude Code.

- **Auth.** Every MCP request authenticates with the same material as REST: a bearer session token or an access key and secret, passed in HTTP headers for Streamable HTTP or through environment variables in stdio mode. The MCP server never holds its own privileges. It calls the REST API through the C# SDK with the caller's credentials, so tenant isolation and RBAC are enforced in exactly one place. Pneuma's `McpAuthenticatedRequestContext` is the request-context pattern.
- **Tools.** `list_topics`, `get_topic`, `create_topic` (from a description, running the refinement defaults without questions), `run_topic`, `get_run`, `list_findings` (filters: topic, tier, minimum score, since), `get_finding` (with evidence), `search_findings`, `get_latest_briefing`, `get_briefing`, `list_predictions`, `resolve_prediction`, `list_suggestions`, `update_suggestion_status`, `submit_feedback` (target, rating, reason), `list_learned_preferences`, `accept_learned_preference`, `get_analytics_summary`. Each tool declares input and output JSON schemas, which Voltaic validates.
- **Resources.** `mmn://briefings/latest`, `mmn://briefings/{id}` (Markdown), `mmn://topics/{id}/brief`, and `mmn://runs/{id}/summary`.
- **Prompts.** `review_morning_briefing` (walk through today's briefing and collect feedback) and `refine_topic` (conversational refinement of an existing topic).
- **Telemetry.** The `Voltaic` meter and source are subscribed in the MCP server's own Radiant host, exporting to the same Tempo and Prometheus.
- **Docs.** `MCP_API.md` covers every tool (name, purpose, input schema with types and required flags, output shape, examples), the resources, the prompts, the transports, and client configuration snippets. It is kept in sync with the tool surface.
- **Tests.** An `Mcp` suite in `Test.Shared` starts the REST server and the MCP server in process, then drives every tool over Streamable HTTP and stdio with Voltaic's client, including cross-tenant denial.

---

## 25. Dashboard

### 25.1 Stack

React 19, Vite (current major), React Router 7, plain JSX with PropTypes, Fetch-based `ApiClient`, i18next with `react-i18next`, `i18next-browser-languagedetector`, and `i18next-icu`, `react-markdown` with `remark-gfm` and `rehype-sanitize`, and `lucide-react` icons (as Xeno uses). No chart library: every chart is hand-rolled SVG, which the frontend architecture requires and which every reference dashboard already does. Tests use Vitest with Testing Library, and visual QA uses Playwright. ESLint 9 flat config (`eslint.config.js`, resolving the `.eslintrc.cjs` mismatch in the frontend document) and Prettier. Port 12190 in development, nginx on 80 in the container.

### 25.2 Reference map (what is copied from where)

| Need | Model |
|------|-------|
| Shell, grouped collapsible sidebar, context topbar | Armada `src/components/Layout.tsx`, `src/lib/navModel.ts`, `navConfig.tsx`; Pneuma `config/nav.js` and `WorkspaceTabs.jsx` for tabbed workspaces; Hydra for route-driven title and summary |
| Tokens and theming | Pneuma `admin-dashboard/src/index.css` scale, Xeno `-soft` tones, Conductor chart series palette, Armada `ThemeContext.tsx` (system default, favicon swap) |
| Tables | Armada `shared/DataTable.tsx`, `ColumnChooser.tsx`, `FilterBar.tsx`, `lib/useResourceTable.ts`; Tempo `TableFrame` and `TablePagination` for above-table controls |
| Charts | Xeno `dashboard/src/charts/*` (`TimeChart`, `HeatmapChart`, `HorizontalBarChart`, `StatusStrip`, `ChartCard`, `chartCatalog.json`), Armada `lib/chartImage.ts` (copy chart as PNG) |
| KPI tiles | Xeno `MetricCard`, Armada `.card.clickable`, Pneuma `grid/SummaryCards.jsx` |
| Topic wizard | Pneuma `admin-dashboard/src/components/wizard/*` and `NEW_SUBJECT_WIZARD.md` |
| Refinement chat | Armada `shared/CaptainChatPanel.tsx`, `ObjectiveDetail.tsx` refinement flow, `components/ask/AskConfirmCard.tsx` |
| Feedback | Xeno `chat/ChatPage.jsx` thumbs with reason modal, Pneuma `FeedbackView.jsx`, AssistantHub `FeedbackChart` |
| Timelines | Armada `blackbox/BlackBoxTimeline.tsx`, `CommitTimeline.tsx` |
| Leaderboards and scorecards | Xeno `ModelLeaderboardView`, Armada `EvaluationDetail.tsx` |
| Markdown | Armada `shared/Markdown.tsx` with sanitize |
| Audio | Auralytic `Dashboard/src/views/CallReviewView.jsx` |
| Login | Armada `LoginFlow.tsx` (tenant step, reveal password), Xeno `LoginView` split brand panel |
| i18n | Xeno `src/i18n/*` and `scripts/verify-i18n.mjs` |
| Request History, API Explorer, Settings | Hydra `RequestHistoryView.jsx` and `RequestDetailsModal.jsx`, Conductor `ApiExplorer.jsx`, Lattice `ApiExplorer.jsx` |
| Setup wizard and tour | Conductor `SetupWizard.jsx`, `Tour.jsx`, `OnboardingContext.jsx` |

### 25.3 Shell

- **Sidebar** (240 px, collapsed 64 px, persisted). `glyph.png` mark plus wordmark, and grouped navigation (25.4). Footer: setup wizard, tour, version, environment badge.
- **Topbar.** Collapse toggle, route title and summary, then context chips (server URL with copy, tenant, user, role pill: Global admin, Tenant admin, Editor, Viewer), health and live indicator (green, amber, or red from `/v1.0/api/health`, with a running-jobs count), and utility icons: refresh, notifications (run finished or failed, preference proposals), language selector, theme toggle, GitHub link (`https://github.com/jchristn/MyMorningNews`), and logout.
- **Login.** Split layout. The brand panel shows `logo.png` and a one-line description. The form takes server URL (default `http://127.0.0.1:12100`), email, password with reveal, and tenant ID (default `default`). A muted "Local development defaults: admin@mymorningnews / password / tenant default" note is shown. Language selector and theme toggle are on the login page.
- **Favicon.** `favicon.ico` and `icon.png` are generated from `glyph.png` (16, 32, 48, 180, 192, and 512 px), with apple-touch and manifest icons.
- **Command palette** (Cmd-K, after Armada) to jump to topics, runs, and pages.

### 25.4 Route inventory

Written here before any code, as `DASHBOARD_STYLE_AND_USABILITY.md` requires. All routes are under `/dashboard`. "RBAC" names the resource checked.

| Group | Route | User job | Roles | Backend | Tables, filters | Actions and modals | Empty and error states |
|-------|-------|----------|-------|---------|-----------------|--------------------|------------------------|
| Start | `/home` | See what happened and what needs me | all | analytics/overview, runs, predictions due, health | recent runs table | Run now, open briefing, grade predictions | first-run CTA to create a topic; per-panel retry |
| Start | `/today` | Read the latest briefing and listen | all | briefings latest | none | feedback per item, audio, download | "No briefing yet" with Compose now |
| Research | `/topics` | Manage topics | Topic | topics | status, owner, schedule, last run, precision | + Add (wizard), View, Edit, Duplicate, Pause, Run now, Delete, View JSON, bulk pause and delete | "Tell me what you care about" CTA |
| Research | `/topics/new` and `/topics/:id/refine` | Refinement wizard | Topic Write | refinement-sessions | none | step rail, regenerate, lock, preview run | model failure panel |
| Research | `/topics/:id` | Topic workspace (tabs: Overview, Context, Schedule, Sources, Runs, Findings, Predictions, Suggestions, Feedback, Preferences, Analytics, Delivery, Prompts, Models) | Topic Read | many | per tab | per tab | per tab |
| Research | `/runs` and `/runs/:id` | Monitor and inspect runs | Run | runs, stages, llm-calls, SSE | status, trigger, topic, date, duration | Cancel, Retry from stage, View JSON, live stage timeline, query and result drill-down | "No runs yet" |
| Research | `/findings` and `/findings/:id` | Browse ranked results across topics | Finding | findings | topic, tier, score range, dimension, rating, date, domain | Rate with why, pin, hide, evidence drawer, score breakdown | "Nothing above threshold" with threshold control |
| Research | `/sources` | Everything examined, with dispositions | Source | sources, search-results | domain, disposition, date, times seen | View text, rate domain, block domain | |
| Research | `/predictions` and `/predictions/:id` | Track and grade predictions | Prediction | predictions, reviews | status, topic, confidence, horizon, due | Resolve, accept or reject review, why | |
| Research | `/suggestions` | Work the dig-into list | Suggestion | suggestions | status, priority, topic | Accept, Done, Snooze, Dismiss with why | |
| Briefings | `/briefings` and `/briefings/:id` | Archive and reader with audio | Briefing | briefings, media | edition, topic, date | Read, listen, feedback, regenerate audio, download | |
| Briefings | `/editions` | Configure editions | Edition | editions, schedules | | + Add, Compose now, Preview, Duplicate | |
| Briefings | `/templates` and `/templates/:id` | Write briefing templates | Template | briefing-templates | | Markdown editor with live preview and validation, versions and diff, import and export `.md`, reset | |
| Insights | `/insights/relevance` | Is ranking working? | Analytics | analytics relevance, calibration | topic, model, prompt version, range | weight tuning proposal accept | |
| Insights | `/insights/accuracy` | Are predictions coming true? | Analytics | analytics accuracy | topic, model, range | | |
| Insights | `/insights/feedback` | Review feedback over time | Feedback | feedback, reasons | channel, target, rating, topic, has reason | edit, retract | |
| Insights | `/insights/preferences` | Review learned preferences | LearnedPreference | learned-preferences | status, topic, kind | Accept, Reject, Edit, Add | |
| Insights | `/insights/usage` | Tokens, cost, latency by model and task | Analytics | analytics usage, llm-calls | model, task, range | | |
| Configuration | `/models` | Model endpoints and assignments | ModelEndpoint | model-endpoints, assignments | | Test, List models, Duplicate | "Add your first model" |
| Configuration | `/prompts` and `/prompts/:key` | Edit prompts | Prompt | prompts | key, scope | editor, versions, diff, rollback, reset, Prompt lab | |
| Configuration | `/search-providers` | Search providers | SearchProvider | search-providers | | Test query | |
| Configuration | `/voices` | TTS voices | Voice | voices | | Test (plays sample) | |
| Configuration | `/email` | Email outbox and preview | EmailMessage | email | status, template, recipient, topic, date | View, Resend, Delete, bulk delete, Send test | "No email sent yet" |
| Observability | `/requests` (alias `/request-history`) | Request History | Admin | request-history | per frontend doc | per frontend doc | three distinct empty states |
| Observability | `/explorer` (alias `/api-explorer`) | API Explorer | all (inherits auth) | openapi.json | | per frontend doc | "/openapi.json unavailable" |
| Observability | `/health` | Workers, queues, providers | all | health, analytics | | | |
| Observability | `/audit` | Security events | Audit | audit | type, principal, result, date | View JSON | |
| Administration | `/admin/tenants` | Tenants | global admin | tenants | | CRUD | |
| Administration | `/admin/users` | Users | Admin | users | | CRUD, reset password, effective permissions | |
| Administration | `/admin/credentials` | API credentials | Admin | credentials | | create (secret shown once), revoke | |
| Administration | `/admin/roles` and `/admin/assignments` | RBAC | Admin | roles, permissions, assignments | | CRUD, clone built-in | |
| System | `/settings` | Server settings | Admin | settings | | form, restart-required badges, Restart Server | |
| System | `/server-info` | Version, endpoints, feature flags | all | server-info | | copy URLs | |
| System | `/profile` | My profile, email preferences, feed tokens | self | users, email preferences, feed-tokens | | change password, revoke tokens | |

### 25.5 Shared components

`DataTable` (server-side sort, filter, and pagination; above-table toolbar with "Showing x to y of z", first/previous/jump/next/last, page size, refresh, + Add, Select Columns with pinned columns, selected count; header filter row; portaled `ActionMenu`; row click opens Edit or View with interactive targets excluded; bulk bar), `Modal`, `ConfirmModal`, `JsonViewerModal` (collapsible tree after Pneuma), `DialogShell` drawer variant, `CopyButton` and `CopyableId`, `PageHeader`, `StatusBadge`, `TierBadge`, `ScoreBar` (component breakdown on hover), `FeedbackControl` (segmented rating plus why modal, after Xeno), `EvidenceDrawer`, `MarkdownView`, `MarkdownEditor` (monospace with preview and validation markers), `ScheduleEditor` (interval type picker, days-of-week chips, time list, time zone, next-runs preview), `StageTimeline`, `AudioPlayer`, `EmptyState`, `ErrorState`, `LoadingState`, `Toast`, and `LanguageSelector`. No browser `alert`, `confirm`, or `prompt` anywhere.

### 25.6 Charts (hand-rolled SVG, defined in `src/charts/chartCatalog.json`)

All charts support light and dark themes, tooltips (portaled), keyboard focus, range control (hour, day, week, month, quarter where meaningful), manual refresh, copy as PNG, click-through to a filtered table, translated labels, and RTL. Data is gap-filled server-side.

| # | Chart | Kind | Page |
|---|-------|------|------|
| 1 | Request activity (success and failure) | stacked columns | Home, Request History |
| 2 | Runs over time by status | stacked columns | Home, Runs |
| 3 | Run duration p50 and p95 by stage | lines and timing bars | Runs, Health |
| 4 | Findings per run by tier | stacked columns | Topic, Findings |
| 5 | Relevance score distribution | histogram | Findings, Relevance |
| 6 | Relevance precision over time (share rated relevant) | line with band | Home, Relevance |
| 7 | Score calibration (score bucket vs share relevant) | reliability diagram | Relevance |
| 8 | Component contribution (llm, feedback, novelty, source, recency) | horizontal bars | Relevance |
| 9 | Precision by dimension | horizontal bars | Topic Analytics |
| 10 | Precision by source domain (top 20) | horizontal bars | Sources, Relevance |
| 11 | Feedback volume by channel (dashboard vs email) | stacked columns | Feedback |
| 12 | Feedback coverage (share of findings rated) | line | Feedback |
| 13 | Reason themes over time | stacked area | Feedback |
| 14 | Prediction accuracy over time | line | Home, Accuracy |
| 15 | Calibration curve (confidence vs observed) | reliability diagram | Accuracy |
| 16 | Brier score trend | line | Accuracy |
| 17 | Predictions by status | status strip | Accuracy, Topic |
| 18 | Accuracy by topic and model | heatmap | Accuracy |
| 19 | Suggestion outcomes funnel | funnel bars | Suggestions |
| 20 | Suggestion time to decision | histogram | Suggestions |
| 21 | Topic activity by day and hour | heatmap | Home, Topic |
| 22 | Novelty trend (duplicate share per run) | line | Topic Analytics |
| 23 | Sources examined vs fetched vs used | stacked columns | Sources |
| 24 | Search provider yield (results to findings) | horizontal bars | Search Providers |
| 25 | Tokens by model and task | stacked columns | Usage |
| 26 | Estimated cost over time | line | Usage |
| 27 | Model latency p50 and p95 | lines | Usage, Models |
| 28 | Structured output repair rate by model | horizontal bars | Models |
| 29 | Evidence verification failure rate by model | horizontal bars | Models |
| 30 | Model leaderboard (precision, accuracy, cost, latency) | ranked table with inline bars | Models |
| 31 | Prompt version comparison (precision before and after) | paired bars | Prompts |
| 32 | Learned preferences over time by status | stacked columns | Preferences |
| 33 | Emails sent by status | stacked columns | Email |
| 34 | Email feedback opens vs submissions | lines | Email |
| 35 | Audio minutes generated and TTS latency | columns and line | Briefings |
| 36 | Schedule load (runs scheduled per hour, next 7 days) | columns | Home, Health |
| 37 | Sparkline per topic (precision, last 14 runs) | sparkline | Topics table |

### 25.7 i18n

- `src/i18n/index.js`, `localeRegistry.js` (storage key `mymorningnews.locale`), `resources.js` (lazy-loaded namespaces per locale under `src/i18n/locales/<locale>/<namespace>.json`), `formatters.js` (explicit-locale `formatNumber`, `formatDate`, `formatDateTime`, `formatRelativeTime`, `formatDuration`, `formatBytes`, `formatPercent`, `formatList`), `LanguageSelector.jsx` (autonyms).
- The 12 baseline locales: en, es, pt, fr, it, de, zh, ar (RTL), ru, ms, hi, ja. Pseudo-locales: `qps-ploc` (expansion) and `qps-rtl`. English is the source. The other catalogs are produced by translation, flagged as machine-translated in `docs/I18N.md`, and kept complete by CI.
- Enum display labels come from a client-side catalog keyed by stable codes, for statuses, tiers, interval types, roles, providers, and stages. Server errors return stable codes plus English messages, and the client localizes known codes.
- Briefing, email, and audio **content** is generated in the topic's `language` (default English). It is content, not UI chrome, and the prompts receive the target language. Email chrome and the public feedback pages use a server-side string catalog for the same 12 locales, keyed by the recipient's preferred locale.
- CI (`dashboard.yml`) runs `scripts/verify-i18n.mjs` (missing keys, orphaned keys, hard-coded string detection), unit tests for formatters and locale resolution, and a pseudo-locale smoke run.

### 25.8 Quality gates

- Vitest covers routing guards, login, `ApiClient`, the table behaviors (server sort, filter, page, column chooser), schedule editor next-run rendering, feedback control, the empty and error states, and i18n bootstrap.
- Playwright captures light and dark themes at 1280, 768, and 390 px for: login, shell, Home, Today, Topics, the wizard steps, Topic workspace, Run detail, Findings with evidence drawer, Briefing reader with audio, Templates editor, Insights pages, Request History, the Request Details modal, API Explorer, Settings, and the Email outbox. It also covers modal overflow, action-menu portaling, filter rows, pagination, topbar truncation, and mobile navigation.
- The dashboard image is rebuilt and the container restarted on every dashboard change during development.
- A handoff report is written at the end of Phase 12: route inventory, references used, backend gaps, API Explorer mode, visual QA summary, and tests run.

---

## 26. Telemetry and Observability

- **Collection.** Radiant in-process, subscribed to meters and sources `Watson`, `PolyPrompt`, `SendWithMailgun`, `CrawlSharp`, `Touchstone` (tests only), and `MyMorningNews`. Traces go to Tempo (`http://tempo:4317`), logs to Loki, and metrics are scraped by Prometheus from `server:8000/metrics`. Telemetry is best effort: a failure to start logs a warning and never stops the server.
- **Application metrics** (prefix `mymorningnews_`, snake case, low-cardinality labels; no tenant, topic, or user IDs as labels):
  - `runs_total{trigger,outcome}`, `run_duration_seconds{trigger}`, `run_stage_duration_seconds{stage}`, `run_stage_events_total{stage,outcome}`, `run_queue_wait_seconds`
  - `llm_calls_total{task,provider,outcome}`, `llm_call_duration_seconds{task,provider}`, `llm_tokens_total{task,provider,direction}`, `llm_structured_repairs_total{task,provider}`
  - `search_requests_total{provider,outcome}`, `search_duration_seconds{provider}`
  - `fetch_requests_total{outcome}`, `fetch_duration_seconds`
  - `findings_total{tier}`, `feedback_total{channel,target_type,rating}`, `predictions_resolved_total{status}`
  - `tts_requests_total{provider,outcome}`, `tts_duration_seconds`, `audio_seconds_generated_total`
  - `email_messages_total{template,provider,outcome}`, `email_feedback_clicks_total{target_type,action}`
  - `scheduler_claims_total`, `scheduler_lag_seconds`
  - `auth_requests_total`, `authz_requests_total`, `authz_denials_total`, `session_events_total`, `rbac_mutations_total`
- **Spans.** A root `run` span (attributes `run.id`, `tenant.id`, `topic.id`, `run.trigger`), a `stage:<Name>` span per stage, and `<service> <operation>` spans for every integration (`polyprompt chat`, `searxng search`, `crawlsharp fetch`, `tts synthesize`, `mailgun send`, `postgres query` where useful). Span status is set on caught failures.
- **Grafana.** Folder `MyMorningNews` with Overview (uptime, request rate, error ratio, active runs), HTTP (rate by route and status class, latency quantiles, top routes by p95), Research Runs and Scheduler, Models and Prompts, Search and Fetch, Briefings and Audio, Email, and Integrations. Datasource UIDs `prometheus`, `tempo`, `loki`. JSON lives in `assets/grafana/` and is mirrored into `docker/grafana/dashboards/mymorningnews/` and the factory profile.
- **External Services card** (low on Home): Grafana `http://127.0.0.1:12110` (admin / admin), Prometheus `http://127.0.0.1:12111`, Tempo `http://127.0.0.1:12112`, Loki `http://127.0.0.1:12115`, SearXNG `http://127.0.0.1:12103`, Kokoro TTS `http://127.0.0.1:12104`. URLs are copyable, credentials are shown in monospace, and an absent service degrades gracefully. `GF_USERS_ALLOW_SIGN_UP=false`, and the README says to change Grafana credentials for anything beyond local use.

---

## 27. Testing and CI

### 27.1 Backend suites (`Test.Shared/MyMorningNewsSuites.cs`)

Every case creates its own data in a fresh, uniquely named database (`mmn_test_<hash>`), cleaned up in `afterSuiteAsync`. Assertions throw specific exceptions, and every async call receives the token. Expensive dependencies are replaced by in-process stubs bound to `127.0.0.1`:

- `StubModelServer`, which speaks both the Ollama and OpenAI-compatible protocols and returns deterministic, scripted responses per prompt key, including tool calls, malformed JSON for the repair path, and 429s.
- `StubSearchServer` (SearXNG JSON), `StubWebServer` (pages, robots.txt, redirects, slow responses), and `StubTtsServer` (a fixed MP3 frame).
- A Mailgun mock modeled on SendWithMailgun's own in-process mock.

| Suite | Covers |
|-------|--------|
| `Identifiers` | prefixes, K-sortability, lengths |
| `Settings` | load, env override, clamping, masked secrets, write-back, restart-required metadata |
| `Migrations` | fresh apply, idempotent reapply, version tracking |
| `Seeding` | default tenant, admin user, built-in roles refresh with stable IDs, default prompts and templates |
| `Tokens` | issue, validate, refresh, revoke, expiry, random IV, cross-tenant rejection, conflicting carriers |
| `Authentication` | each scheme, rate limiting, inactive principals |
| `Authorization` | bypass order, deny over permit, implicit deny, resource scope, owner ceiling, admin endpoint hardening, effective permissions |
| `TenantIsolation` | every resource: cross-tenant read 404, write rejected, enumeration clean (after Armada `MultiTenantScopingTests`) |
| `Audit` | denials and bypasses persisted, filters, retention |
| `RequestHistory` | capture, redaction, truncation, summary gap fill, prune, disabled mode |
| `Tenants`, `Users`, `Credentials`, `Roles` | CRUD and cascades |
| `ModelEndpoints` | CRUD, key encryption, validation through the stub |
| `ModelGateway` | role resolution, tool-call structured output, JSON fallback and repair, retries, call logging |
| `Prompts` | render, unknown-variable rejection, versioning, rollback, reset, topic override |
| `Topics` | CRUD, dimensions, constraints, cascade delete |
| `Refinement` | full wizard flow against the stub, resume, apply diff, refine again with feedback digest |
| `ScheduleCalculator` | every interval type, time zones, DST forward and back, end dates, Once, Cron |
| `Scheduler` | claim with `SKIP LOCKED` under concurrency, no overlapping runs, missed-run policies |
| `Search`, `Fetch` | providers against stubs, robots, blocked domains, canonicalization, size caps |
| `Pipeline` | full run end to end against stubs; each stage in isolation; retry from stage; cancel; partial success |
| `Evidence` | quote verification, unverifiable quote rejection |
| `Dedupe`, `Ranking` | similarity thresholds, update linking, composite score math, tiers, tuner proposal |
| `Predictions` | creation, review proposals, resolution, Brier math |
| `Suggestions`, `Feedback`, `LearnedPreferences` | lifecycle, supersede, distillation proposal flow |
| `Briefings`, `Templates` | template validation errors, directive selection, composition, citations, HTML export |
| `Media` | script, segmenting, synthesis against stub, concatenation, Range responses, feed XML |
| `Email` | each template renders (HTML and text, encoded values), outbox idempotency, retry and backoff, file provider output, Mailgun mock send, preferences and unsubscribe, every link present and correct |
| `EmailActionTokens` | hash-only storage, expiry, revoke, target binding, allowed actions, GET does not record by default, POST records, supersede, rate limit, uniform errors |
| `Analytics` | each series, gap filling, tenant scoping |
| `Routes` | every route: auth required, typed errors, OpenAPI presence |

`Test.Automated` strips `--provider postgresql` (the only accepted value) and passes a connection string from environment variables to the suites. A missing or unknown value returns `ConsoleRunner.ExitInvalidArguments`. `Test.Xunit3` hosts the same suites as one fact and as per-case theories.

### 27.2 Workflows

- **`tests.yml`** (every push, every branch, every PR). Job `Full suite (ubuntu-latest)` with a `pgvector/pgvector:pg17` service on `127.0.0.1:5432` (throwaway credentials). Steps: setup .NET 8 and 10, restore, build, `dotnet run --project src/Test.Automated -f net8.0 -- --provider postgresql --results results-net8.0.json`, the same for net10.0, `dotnet test src/Test.Xunit3`, and upload `results-*.json` with `if: always()`. There are no filters, tags, or `continue-on-error`. Job `Provider matrix (postgresql)` as the document describes.
- **`tests.yml`, job `SDK harnesses`** (Phase 11 onward). Postgres service, server started with stub endpoints, then `./sdk/csharp/run-tests.sh`, `./sdk/js/run-tests.sh`, and `./sdk/python/run-tests.sh` (`shell: bash`), with results uploaded using `if: always()`.
- **`dashboard.yml`.** `npm ci`, lint, Vitest, i18n verification, pseudo-locale smoke, and build.
- **`benchmarks.yml`** (Phase 13). Brings up the bench stack and runs `ci-gate`.
- **Required checks on `main`:** `Full suite (ubuntu-latest)`, `Provider matrix (postgresql)`, `Dashboard`, from Phase 11 `SDK harnesses`, and from Phase 13 `Benchmark gate`.

---

## 28. Benchmarking and Simulated User Testing

**Benchmarking** (Phase 13, per `BENCHMARKING.md`). `src/Test.Benchmark` (assembly `MyMorningNews.Benchmark`) is a black-box harness using a typed `MyMorningNewsClient` against an isolated stack (`benchmarks/docker/compose.yaml`, project `mymorningnews-bench`, ports 125xx, the same pinned images). Commands: `validate`, `ingest` (create topics and feedback from a dataset), `quality` (relevance precision and NDCG of ranking against labeled probes, evidence verification rate, dedupe precision and recall), `e2e` (topic to run to briefing to email, with stage attribution from `/metrics`), `load` (concurrent runs and API reads, open and closed loop), `resilience` (search, model, and TTS slow, down, or 429), `compare`, `history`, and `stub`. Datasets: a frozen live snapshot (`mymorningnews-live.json`) and a synthetic corpus of served pages with known relevant, irrelevant, duplicate, and update items (negative probes included). Public datasets may not fit this task. If none does, `BENCHMARKING.md` says why. The CI gate runs only deterministic, stub-backed checks. Paid runs need explicit opt-in and a spend cap. The product hooks are the dry-run endpoints (`/topics/{id}/dry-run`, `/prompts/lab`, `/email/preview`), documented in `REST_API.md`. Deliverables: `BENCHMARKING.md`, `IMPROVEMENTS.md`, `benchmarks/README.md`, `benchmarks/RESULTS.md` with round 0, and the `.sh` and `.bat` script pairs.

**Simulated user testing** (Phase 13, and a focused session after each fix batch, per `SIMULATED_USER_TESTING.md`). A clean `mymorningnews-usertest` stack on 127xx ports with stubbed LLM, search, TTS, and Mailgun. Personas are the defaults, plus a "morning reader who only uses email" persona so the email-only feedback loop gets exercised. The report goes to `user-testing/<date>-<label>.md` with a coverage matrix, and findings are surfaced to you for decisions before any fix is made.

---

## 29. Implementation Phases

Each phase ends with a green build (no warnings), the tests for that phase passing, docs updated, and `CHANGELOG.md` updated. Each completed phase is committed and pushed to `origin/main`, and this plan's checkboxes and a progress log (section 32) are updated in the same commit.

### Phase 0: Repository bootstrap

- [x] `git init`, `main` branch, `origin` set to `https://github.com/jchristn/MyMorningNews` (done; nothing committed or pushed yet).
- [x] `.gitignore` (.NET, Node, `docker/` runtime data, `benchmarks/.run`, `user-testing/evidence/`, `.env`), `.dockerignore`, `LICENSE.md` (MIT), `CHANGELOG.md` (`0.1.0-alpha`, unreleased), `README.md` skeleton with logo, `DOCKERHUB_README.md` skeleton with absolute `raw.githubusercontent.com` asset URLs.
- [x] `CLAUDE.md` with the full `CODE_STYLE.md` rule set, the decisions in section 4, the port table, and "no em-dashes".
- [x] `src/MyMorningNews.sln` with Core, Server, Test.Shared, Test.Automated, and Test.Xunit3 projects building empty.
- [x] `dashboard/` scaffolded (Vite, React 19, router, ESLint flat config, Prettier, Vitest).
- [x] Generated icons from `glyph.png` into `assets/` and `dashboard/public/`.
- [x] `docker/compose.yaml` with postgres, server, and dashboard (stubs) healthy. `update.bat`, `update.sh`, `factory/reset.bat`, and `factory/reset.sh`.
- [x] `tests.yml` and `dashboard.yml` running (trivially green).

Exit: `docker compose up -d --build` gives three healthy containers, and both CI workflows pass.

Notes: ESLint is pinned to 9.x because `eslint-plugin-react` does not yet support ESLint 10. `Touchstone.Xunit3Adapter` is pinned to 1.1.0 because 1.1.1 requires an unpublished `Touchstone.Core` 1.1.1. Mux.Search 1.8.0 is on nuget.org and is used directly for Tavily and You.com.

### Phase 1: Backend foundation

- [ ] Settings classes, loader, validation, env overrides, and settings file in `docker/server/`.
- [ ] `Bootstrapper`, `MyMorningNewsServer`, Preflight, PostRouting, exception handler, serializer, OpenAPI.
- [ ] Database layer: `DatabaseSettings`, `DatabaseTypeEnum`, `DatabaseDriverBase`, `DatabaseDriverFactory`, `SchemaMigration`, Postgresql driver, migrations v1 (auth, audit, request history), seeding.
- [ ] Auth: tokens, schemes, `AuthenticationService`, `AuthorizationService`, `OperationScopeMap`, effective permissions, audit, rate limiting.
- [ ] Routes: Health, Token, Tenant, User, Credential, Role, Permission, Assignment, Audit, RequestHistory, Settings (read, write, restart), ServerInfo.
- [ ] Request history capture, summary, prune.
- [ ] Radiant telemetry plus Prometheus, Tempo, Loki, and Grafana in compose, with Overview and HTTP dashboards.
- [ ] Suites: Identifiers, Settings, Migrations, Seeding, Tokens, Authentication, Authorization, TenantIsolation, Audit, RequestHistory, Tenants, Users, Credentials, Roles, Routes.

Exit: sign in with the default credentials through Postman and curl, every admin route works, telemetry is visible in Grafana, and the suites are green in CI.

### Phase 2: Dashboard foundation

- [ ] Tokens (light and dark), `ThemeContext`, `AuthContext`, `AppContext`, `ApiClient` with query builders and `ApiError`, `useApi`.
- [ ] i18n runtime, the 12 locales (initially English plus pseudo-locales; the rest filled in Phase 12), `LanguageSelector`, `verify-i18n.mjs`.
- [ ] Shell: sidebar, topbar, command palette, notifications, route metadata, login with brand assets.
- [ ] Shared components (section 25.5) and the chart primitives (`TimeChart`, `HorizontalBarChart`, `HeatmapChart`, `StatusStrip`, `ChartCard`).
- [ ] Pages: Home (system panels), Request History plus the Request Details modal, API Explorer, Settings (with restart), Server Info, Health, Audit, Tenants, Users, Credentials, Roles and Assignments, Profile.
- [ ] Vitest coverage for these. First Playwright captures.

Exit: every Phase 1 capability can be operated from the dashboard in both themes at three widths.

### Phase 3: Models and prompts

- [ ] Model endpoints (CRUD, encryption, validate, list models, test), assignments, `ModelGateway`, structured-output strategy, `llmcalls`.
- [ ] Prompt templates with defaults for every key in section 12, renderer, validation, versions, rollback, reset, topic overrides, Prompt lab.
- [ ] `StubModelServer`.
- [ ] Dashboard: Models, Prompts (editor, diff, lab), Usage page shell.
- [ ] Suites: ModelEndpoints, ModelGateway, Prompts.

Exit: you can register Ollama and an API model, assign roles, edit a prompt, and run it in the lab against both.

### Phase 4: Topics and the refinement wizard

- [ ] Topic, dimension, constraint, and recipient models, routes, and cascades.
- [ ] Refinement sessions, all wizard steps with SSE generation, resume, apply, and refine again.
- [ ] Dashboard: Topics table, wizard (step rail, guidance, regenerate, lock, failure panel), Topic workspace Overview and Context tabs.
- [ ] Suites: Topics, Refinement.

Exit: "I want to learn about AI developer tools" produces dimensions, at least two rounds of questions, and a reviewable context, persisted and resumable.

### Phase 5: Schedules, search, and fetch

- [ ] `ScheduleCalculator`, schedules and schedule times, the preview endpoint, `SchedulerWorker`, `RunWorker` queue.
- [ ] Search providers (SearXNG container with curl, Tavily or You.com, hosted model search, RSS, Hacker News, arXiv), `FetchService` with CrawlSharp and DocConverter, canonicalization.
- [ ] Dashboard: `ScheduleEditor` with next-runs preview, Search Providers page, topic Schedule and Sources tabs.
- [ ] Suites: ScheduleCalculator, Scheduler, Search, Fetch.

Exit: a topic scheduled for every 5 minutes enqueues runs on time across a container restart, and DST cases pass.

### Phase 6: Research pipeline, ranking, and evidence

- [ ] All stages in section 16, embeddings (pgvector), dedupe, composite ranking, tiers, evidence verification, run SSE, cancel, retry from stage, dry run.
- [ ] Dashboard: Runs (live stage timeline, queries, results, LLM calls), Findings (score breakdown, evidence drawer), Sources, Predictions, Suggestions, plus charts 2 to 5, 21 to 24.
- [ ] Suites: Pipeline, Evidence, Dedupe, Ranking, Predictions, Suggestions.

Exit: a real run against SearXNG and Ollama produces ranked, evidence-backed findings, predictions, and suggestions, with every stage visible and replayable.

### Phase 7: Briefings, templates, and editions

- [ ] Template format, validator, directives, versioning, import and export, default templates.
- [ ] `BriefingComposer`, editions, the edition schedule, fresh-research lead, single-run briefings, HTML export.
- [ ] Dashboard: Today, Briefings reader with inline feedback, Editions, Templates editor with live preview.
- [ ] Suites: Briefings, Templates.

Exit: "Morning News" composes on schedule from the template you wrote, with citations that resolve.

### Phase 8: Audio and podcast

- [ ] Kokoro TTS container (with curl), `ITtsProvider`, `MediaWorker`, script, segmenting, ffmpeg concatenation, chapters, Range serving, voices, feed tokens, podcast RSS.
- [ ] Dashboard: audio player with chapters and synced section highlighting, Voices page, feed-token management in Profile.
- [ ] Suites: Media.

Exit: a briefing produces a playable MP3 with chapters, and a podcast app can subscribe to the private feed.

### Phase 9: Email delivery

- [ ] Provider abstraction (Mailgun, File, Disabled), factory, outbox, delivery worker with backoff, idempotency.
- [ ] Layout and templates on HtmlPageBuilder (run digest, briefing published, run failed, test), `EmailComposer` blocks, `EmailMarkdown`.
- [ ] Action tokens, public feedback and unsubscribe landing pages, preferences.
- [ ] Pipeline `Notify` stage and edition email.
- [ ] Dashboard: Email outbox and preview, Settings Email section with Send test, topic Delivery tab, Profile email preferences, charts 33 and 34.
- [ ] Suites: Email, EmailActionTokens.

Exit: every run sends a digest (File provider by default, Mailgun when configured). Each finding, prediction, and suggestion has working detail and feedback links, and feedback submitted from the email shows up in the dashboard with its reason.

### Phase 10: Feedback, learning, and analytics

- [ ] Feedback API across all targets, reason tagging, `LearningWorker`, learned preference lifecycle, email proposals, `RankingTuner`, prediction review acceptance, Brier and calibration math, every analytics endpoint.
- [ ] Dashboard: Insights pages (Relevance, Accuracy, Feedback, Preferences, Usage), every remaining chart in section 25.6, topic Analytics tab.
- [ ] Suites: Feedback, LearnedPreferences, Analytics.

Exit: feedback visibly changes ranking and prompts on the next run, and every chart renders real data with drill-down.

### Phase 11: SDKs and MCP server

- [ ] `sdk/csharp` (`MyMorningNews.Sdk` plus `MyMorningNews.Sdk.TestHarness`), `sdk/js` (`mymorningnews-sdk.js` plus `test-harness.js`), and `sdk/python` (`mymorningnews_sdk.py` plus `test_harness.py`). Each has a README, a `run-tests.sh` and `run-tests.bat`, and covers the API surface in section 24.1.
- [ ] `src/MyMorningNews.McpServer` on Voltaic: Streamable HTTP on `/mcp` (port 12105, `mcp` compose service with a healthcheck) and stdio mode. Tools, resources, and prompts as in section 24.2. Calls the REST API through the C# SDK.
- [ ] `MCP_API.md`. A README section for connecting Claude Code and other MCP clients.
- [ ] `sdk` CI job running all three harnesses against a live server. MCP suites in `Test.Shared`.

Exit: each SDK harness passes against a fresh stack, and an MCP client can list topics, read today's briefing, and submit feedback with a reason.

### Phase 12: Hardening, documentation, and polish

- [ ] Signed-request credential auth, CORS allow-list setting, rate-limit tuning, retention workers for every table with a retention setting.
- [ ] `REST_API.md` complete. The Postman collection covers every route with descriptions and variables.
- [ ] Complete translations for the 12 locales. RTL and expansion QA.
- [ ] Grafana dashboards for every domain. Factory seeded demo profile.
- [ ] README (use cases, architecture, quick start, default credentials, ports, email setup, models, AOT support statement, database support statement, benchmarks link) and DOCKERHUB_README, both run through the humanizer pass and the Signs of AI writing review.
- [ ] `docs/` complete. Dashboard handoff report. Full Playwright visual QA.

Exit: a clean checkout reaches a first briefing by following only the README.

### Phase 13: Benchmarks, simulated user testing, release readiness

- [ ] Benchmark harness, datasets, isolated stack, CI gate, round 0 (bugs found are fixed first), `RESULTS.md`, `IMPROVEMENTS.md`.
- [ ] Simulated user testing session, report, your decisions, approved fixes verified on a fresh instance.
- [ ] Final review against every document in section 3.

Exit: all Definition of Done checklists in `BENCHMARKING.md` and `SIMULATED_USER_TESTING.md` are satisfied, or each exception is recorded with a reason.

---

## 30. Open Questions

Decided on 2026-11-19:

| # | Question | Decision |
|---|----------|----------|
| Q1 | Text-to-speech engine | Bundled Kokoro-FastAPI container behind an OpenAI-compatible speech provider (which also covers OpenAI TTS). ElevenLabs later. |
| Q2 | Default web search | Bundled SearXNG, plus optional Tavily or You.com keys, model-hosted web search where supported, RSS, Hacker News, and arXiv. |
| Q3 | CI operating systems | `AOT support: Not supported`. Full suite on `ubuntu-latest` with a Postgres service container. |
| Q4 | Scope of heavier items | All in scope: benchmark suite and simulated user testing (Phase 13), SDKs and MCP server (Phase 11). Native installers out of scope (Docker images only, section 4, C4). |
| Q5 | Mux reference for email | Nothing from Mux. Xeno (provider abstraction and templates) and BjjWithJoel (outbox, composer blocks, standard layout) are the patterns. |
| Q6 | Email link reachability | `127.0.0.1` defaults with configurable `PublicDashboardBaseUrl` and `PublicServerBaseUrl`. README covers reverse proxy, VPN, and LAN setups. |
| Q7 | One-click feedback vs link scanners | GET opens a prefilled page and one click on Submit records it. `Email.RecordFeedbackOnClick` stays available but off. |

| Q8 | Default edition | "Morning News", weekdays at 06:30 local, all topics, audio and email on. It is fully editable, and users create their own editions with custom schedules. |
| Q9 | Default model | Anthropic `claude-opus-5-5` for every chat role, through PolyPrompt (which also supports Ollama, OpenAI, OpenAI-compatible, Gemini, xAI, and others). Embeddings on Ollama `all-minilm`, with dimension changes handled (section 10.1). Ollama is not the default chat model. |
| Q10 | Commits | Commit and push to `origin/main` at the end of each phase. |

No open questions remain.

---

## 31. Risks

| Risk | Mitigation |
|------|------------|
| Local models produce weak structured output or hallucinated quotes | Tool-call structured output with validation and repair, verbatim quote verification, and repair-rate and verification-failure charts per model so you can see which models to assign where |
| Cost runaway on API models with frequent schedules | Per-run caps (queries, fetches, findings), per-model cost tracking, a minimum interval setting, a Usage page, and an optional monthly budget per tenant that pauses runs (Phase 12) |
| Email links unreachable or prefetched | Configurable base URLs, scanner-safe GET, and open vs submit tracked separately |
| Search quality from SearXNG varies | Multiple providers per topic, provider yield chart, RSS and seed sources |
| Sites block fetching | Robots respected, per-domain delay, honest dispositions recorded, optional headless mode |
| Kokoro CPU synthesis is slow for long briefings | Segment parallelism, an audio length target in the template, a background worker, and the option to point at a faster OpenAI-compatible TTS |
| Scope is large | The phases are ordered so each one produces a usable product. After Phase 6 the system already researches, ranks, and shows results, and Phase 9 completes the email loop. |

---

## 32. Progress Log

| Date | Phase | Status | Notes |
|------|-------|--------|-------|
| 2026-11-19 | Plan | Done | Plan written, all questions answered, repository initialized with `origin`. |
| 2026-11-19 | 0 | Done | Solution (Core, Server, McpServer placeholder, Test.Shared, Test.Automated, Test.Xunit3), minimal Watson host with health, OpenAPI, CORS. Dashboard scaffold (React 19, Vite 8, ESLint 9, Vitest). Icons from glyph.png. Compose stack (postgres, server, dashboard) healthy. CI workflows. README, DOCKERHUB_README, CLAUDE.md. |
