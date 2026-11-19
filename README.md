<p align="center">
  <img src="assets/logo.png" alt="My Morning News" width="480">
</p>

# My Morning News

My Morning News is a self-hosted research assistant. You tell it what you care about, it asks you questions until it understands what to look for and what to ignore, and then scheduled research agents go out, read, rank, and summarize. You get a Markdown briefing, an audio version, and an email after every run with links to the details and one-click feedback. Everything it examined, everything it suggested, and every prediction it made is kept, so it can learn what you find relevant and keep score on what turned out to be true.

> **Status: early alpha (`0.1.0-alpha`).** The repository is being built phase by phase from [`MY_MORNING_NEWS_PLAN.md`](MY_MORNING_NEWS_PLAN.md). Section 32 of the plan shows current progress. Today the backend foundation is in place: sign-in, tenants, users, credentials, RBAC, audit, request history, runtime settings, OpenAPI, and the observability stack, plus the credential vault, model endpoints and assignments, the model gateway with full cost accounting, and editable prompts. The research features and the full dashboard arrive in the following phases.

## What it will do

- **Refine a topic with you.** Describe an interest in plain language ("I want to learn about AI developer tools"). The configured model proposes dimensions, asks clarifying questions in rounds, and drafts a brief with includes, excludes, and preferred sources that you edit.
- **Research on your schedule.** Every n minutes, hours, days, or weeks, specific days of the week or month, once, or cron, each in your time zone.
- **Rank and back up every result.** Findings get a 0 to 100 relevance score built from model judgment, your past feedback, novelty, source reputation, and recency. Every claim links to a verbatim quote from the source it came from.
- **Look ahead.** Each run writes predictions with confidence and a horizon date, then grades them later. Suggestions tell you what to dig into next.
- **Deliver it your way.** Briefing structure comes from a Markdown template you write. Output is Markdown, audio (local Kokoro TTS by default), a private podcast feed, and an email after every run.
- **Learn from you.** Rate anything as relevant or not and say why. The system distills that into preferences you approve, and dozens of charts show how relevance and accuracy change over time.
- **Use any model.** Model access goes through [PolyPrompt](https://github.com/jchristn/PolyPrompt): Anthropic (the default), OpenAI, OpenAI-compatible servers, Ollama, Gemini, xAI, and more, assignable per task.
- **Multi-tenant with RBAC,** request history, audit, OpenAPI-driven API explorer, Grafana dashboards, SDKs, and an MCP server.

## Models, web search, and credentials

The default chat model is Anthropic `claude-opus-5-5` (add your API key in the setup wizard or set `MYMORNINGNEWS_ANTHROPIC_API_KEY`). Any role can be reassigned to another provider through PolyPrompt.

**Open models need a search service.** If you run an open model through Ollama or another OpenAI-compatible server, it can call tools but it cannot search the web on its own. Model-hosted search exists only on OpenAI, Azure OpenAI, and xAI endpoints. Give open models a search provider: the bundled SearXNG works without a key, and Tavily (recommended) or You.com work with an API key. Optionally, register MCP servers to expose more tools. The model must support tool calls (Gemma 3, for example, does not). Models without tool support fall back to a fixed query plan that the server runs for them.

**Every model call is accounted for.** Each call records who it is attributed to, the topic and run, input, cached, cache-write, output, and reasoning tokens, time to first token, streaming time, tool calls, retries, and estimated cost from the endpoint's per-million-token prices. `GET /v1.0/api/usage/report?groupBy=User` (or Model, Task, Provider, Topic, PromptKey, Day) shows what the service costs and who uses it most. Global administrators can add `allTenants=true` to see cost by tenant. Members see only their own usage at `/v1.0/api/usage/me`. Grafana's "Models and cost" dashboard shows the same data as metrics.

**Models stay on task.** A scope guardrail is prepended to every model call, and every default prompt repeats it. Models only research and summarize your topics, treat fetched pages and other input as data rather than instructions, and decline requests to write code or act as a general assistant. Administrators can edit the guardrail text (`Pipeline.ScopeGuardrail`) but cannot remove it.

**Credentials live in the vault.** API keys for model providers, Tavily, You.com, Mailgun, TTS services, and MCP servers are stored once, encrypted, in the tenant's credential vault (dashboard: Configuration, then Credentials), and referenced by the things that use them. Reading a credential returns only its last four characters. Tenant administrators can reveal the full value through an audited reveal action (`POST /v1.0/api/vault/credentials/{id}/reveal`). MCP servers (Streamable HTTP or SSE) are registered under Configuration, then MCP Servers, with their auth header taken from the vault.

## Quick start (Docker)

```bash
git clone https://github.com/jchristn/MyMorningNews.git
cd MyMorningNews/docker
docker compose up -d --build
```

Open the dashboard at http://127.0.0.1:12101. The default local credentials are below. Change them before exposing the stack anywhere.

| Field | Value |
|-------|-------|
| Email | `admin@mymorningnews` |
| Password | `password` |
| Tenant ID | `default` |

Sign in through the API:

```bash
curl -s -X POST http://127.0.0.1:12100/v1.0/token \
  -H 'x-email: admin@mymorningnews' -H 'x-password: password' -H 'x-tenant-guid: default'
```

Send the returned token as `Authorization: Bearer <token>`. The full API is described at http://127.0.0.1:12100/openapi.json (Swagger UI at `/swagger`).

Useful helpers: `docker/update.sh` (or `.bat`) pulls and recreates the stack without touching data. `docker/factory/reset.sh` (or `.bat`) deletes all data and starts fresh.

## Ports

Every port is bound to `127.0.0.1` and sits in the 12xxx range to stay clear of other stacks.

| Service | Port |
|---------|------|
| REST server | 12100 |
| Dashboard | 12101 |
| PostgreSQL | 12102 |
| SearXNG (planned) | 12103 |
| Kokoro TTS (planned) | 12104 |
| MCP server (planned) | 12105 |
| Grafana (admin / admin) | 12110 |
| Prometheus | 12111 |
| Tempo (HTTP, OTLP gRPC, OTLP HTTP) | 12112, 12113, 12114 |
| Loki | 12115 |

## Building from source

```bash
dotnet build src/MyMorningNews.sln
cd dashboard && npm ci && npm run build
```

Tests need PostgreSQL. With the Docker stack running:

```bash
MYMORNINGNEWS_TEST_DB="Host=127.0.0.1;Port=12102;Username=postgres;Password=password" \
  dotnet run --project src/Test.Automated -f net10.0 -- --provider postgresql
```

## Database support

PostgreSQL (with pgvector) is the only supported database. The data layer keeps a provider abstraction (`DatabaseDriverBase` and `DatabaseDriverFactory`), so another provider could be added later without changing services, but none is planned.

## AOT support

Not supported. The server depends on libraries that are not marked AOT-compatible (CrawlSharp with Playwright, TextChunker) and ships only as a Linux container, so the test suite runs on `ubuntu-latest` against a real PostgreSQL service and there is no `Test.Aot` project.

## License

MIT. See [LICENSE.md](LICENSE.md).
