# Changelog

All notable changes to this project are documented here. The project follows [Semantic Versioning](https://semver.org). Versions below 1.0.0 are alpha and may include breaking changes.

## [0.1.0-alpha] - Unreleased

### Fixed

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
- Scope guardrail on every model call and in every default prompt, so models stay on research and briefing tasks and treat fetched content as data.
- Prompt templates: 19 defaults, renderer with list expansion, save-time variable validation, versions, rollback, reset, topic overrides, and a prompt lab.
- New tenants are seeded with an Anthropic `claude-opus-5-5` endpoint, an Ollama `all-minilm` embedding endpoint, assignments for every task, and the default prompts.
