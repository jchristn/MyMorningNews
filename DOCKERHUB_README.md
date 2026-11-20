<p align="center">
  <img src="https://raw.githubusercontent.com/jchristn/MyMorningNews/main/assets/logo.png" alt="My Morning News" width="480">
</p>

# My Morning News

A self-hosted research assistant that refines what you care about through an LLM-guided interview, researches it on your schedule, ranks and cites everything it finds, and delivers a Markdown briefing, audio, and an email after every run with one-click feedback links. It learns from that feedback and keeps score on its own predictions.

> Early alpha (`0.1.0-alpha`). Source, plan, and progress: https://github.com/jchristn/MyMorningNews

## Use cases

- Track a field you need to stay current on (AI developer tools, a market, a technology) without reading everything yourself.
- Get a morning briefing you can read or listen to, built from a template you control.
- See which sources and topics actually matter to you, and how accurate the forward-looking calls have been.

## Architecture

| Image | Purpose | Port |
|-------|---------|------|
| `server` | C# REST API on Watson 7, research pipeline, scheduler, email | 12100 |
| `dashboard` | React dashboard served by nginx | 12101 |
| `pgvector/pgvector` | PostgreSQL with pgvector | 12102 |
| `searxng` | Private metasearch for research, no API key needed | 12103 |
| `kokoro` | Local text-to-speech for audio briefings and the podcast feed | 12104 |
| `mcp` | MCP server for Claude Code and other MCP clients (profile `mcp`) | 12105 |

## Getting started

```bash
git clone https://github.com/jchristn/MyMorningNews.git
cd MyMorningNews/docker
docker compose up -d --build
```

Open http://127.0.0.1:12101 and sign in with `admin@mymorningnews` / `password` (tenant `default`, or leave it blank). Change these credentials before exposing the stack.

## License

MIT

(c) 2026 Lattice AI LLC
