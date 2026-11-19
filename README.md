<p align="center">
  <img src="assets/logo.png" alt="My Morning News" width="480">
</p>

# My Morning News

My Morning News is a self-hosted research assistant. You tell it what you care about, it asks you questions until it understands what to look for and what to ignore, and then scheduled research agents go out, read, rank, and summarize. You get a Markdown briefing, an audio version, and an email after every run with links to the details and one-click feedback. Everything it examined, everything it suggested, and every prediction it made is kept, so it can learn what you find relevant and keep score on what turned out to be true.

> **Status: early alpha (`0.1.0-alpha`).** The repository is being built phase by phase from [`MY_MORNING_NEWS_PLAN.md`](MY_MORNING_NEWS_PLAN.md). Section 32 of the plan shows current progress. Today the stack starts, the server answers health checks and serves OpenAPI, and the dashboard shell is in place.

## What it will do

- **Refine a topic with you.** Describe an interest in plain language ("I want to learn about AI developer tools"). The configured model proposes dimensions, asks clarifying questions in rounds, and drafts a brief with includes, excludes, and preferred sources that you edit.
- **Research on your schedule.** Every n minutes, hours, days, or weeks, specific days of the week or month, once, or cron, each in your time zone.
- **Rank and back up every result.** Findings get a 0 to 100 relevance score built from model judgment, your past feedback, novelty, source reputation, and recency. Every claim links to a verbatim quote from the source it came from.
- **Look ahead.** Each run writes predictions with confidence and a horizon date, then grades them later. Suggestions tell you what to dig into next.
- **Deliver it your way.** Briefing structure comes from a Markdown template you write. Output is Markdown, audio (local Kokoro TTS by default), a private podcast feed, and an email after every run.
- **Learn from you.** Rate anything as relevant or not and say why. The system distills that into preferences you approve, and dozens of charts show how relevance and accuracy change over time.
- **Use any model.** Model access goes through [PolyPrompt](https://github.com/jchristn/PolyPrompt): Anthropic (the default), OpenAI, OpenAI-compatible servers, Ollama, Gemini, xAI, and more, assignable per task.
- **Multi-tenant with RBAC,** request history, audit, OpenAPI-driven API explorer, Grafana dashboards, SDKs, and an MCP server.

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
| Grafana, Prometheus, Tempo, Loki (planned) | 12110 to 12115 |

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
