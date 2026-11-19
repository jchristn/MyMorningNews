# Changelog

All notable changes to this project are documented here. The project follows [Semantic Versioning](https://semver.org). Versions below 1.0.0 are alpha and may include breaking changes.

## [0.1.0-alpha] - Unreleased

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
- Touchstone test suites (62 cases) for security primitives, settings, database, authentication, authorization, tenant isolation, resources, and observability.
