# CLAUDE.md

Guidance for agents working in this repository. The authoritative design is `MY_MORNING_NEWS_PLAN.md`. Keep its phase checkboxes and the section 32 progress log current, and commit and push to `origin/main` at the end of each phase.

## Product

My Morning News: multi-tenant research assistant. Topics are refined through an LLM wizard, researched on schedules by a staged pipeline, ranked, backed by verified evidence, and delivered as Markdown briefings, audio, and an email after every run with detail and feedback links. Feedback, predictions, and suggestions are tracked over time.

## Layout

- `src/MyMorningNews.Core`: models, enums, requests, responses, database (Postgresql only), services, pipeline, email.
- `src/MyMorningNews.Server`: Watson 7 host, route registrars, workers, settings.
- `src/MyMorningNews.McpServer`: Voltaic MCP server.
- `src/Test.Shared`, `src/Test.Automated`, `src/Test.Xunit3`: Touchstone tests. `Test.Automated` requires `--provider postgresql`.
- `dashboard/`: React 19 + Vite dashboard.
- `sdk/{csharp,js,python}`: SDKs with test harnesses.
- `docker/`: compose stack, settings, factory reset, observability config.

## Build and test

```bash
dotnet build src/MyMorningNews.sln
# tests need Postgres; start one with: docker compose -f docker/compose.yaml up -d postgres
MYMORNINGNEWS_TEST_DB="Host=127.0.0.1;Port=12102;Username=postgres;Password=password" \
  dotnet run --project src/Test.Automated -f net10.0 -- --provider postgresql
cd dashboard && npm ci && npm run lint && npm test && npm run build
cd docker && docker compose up -d --build
```

## Decisions (see plan section 4)

- Postgres is the only database. Keep `DatabaseDriverBase`, `DatabaseDriverFactory`, `DatabaseTypeEnum` (only `Postgresql`), and `Database/Postgresql/{Implementations,Queries}`.
- Watson 7 routes are registered only through `server.Routes.{PreAuthentication,PostAuthentication}.{Static,Parameter}.Add(...)` in per-feature registrar classes. Never use `server.Get(...)`, `ApiRequest`, or tuple returns.
- Route prefixes: `/v1.0/token` (sessions), `/v1.0/tenants/...` (control plane), `/v1.0/api/...` (everything else), `/v1.0/public/...` (tokenized email and feed pages).
- IDs are PrettyId K-sortable with prefixes from `Constants.IdPrefixes`, generated only by `Helpers/IdGenerator`. The seeded tenant ID is the literal `default`.
- Column names are lowercase with no separators (`tenantid`, `createdutc`). No catch-all JSON columns.
- Default chat model is Anthropic `claude-opus-5-5`. Anthropic requests omit temperature/top_p/top_k, never prefill, never force `tool_choice`, and set `ReasoningEffort` explicitly. Embeddings default to Ollama `all-minilm`, and vectors carry model and dimensions.
- Charts are hand-rolled SVG. No chart libraries.
- Use `127.0.0.1`, never `localhost`, for loopback.
- Host ports are 12xxx: server 12100, dashboard 12101, postgres 12102, searxng 12103, tts 12104, mcp 12105, grafana 12110, prometheus 12111, tempo 12112 to 12114, loki 12115, ollama 12120, vite dev 12190. Never 12345.
- Never change version numbers (`0.1.0-alpha`) without explicit approval.

## C# code style (strict)

- The namespace declaration goes first, and `using` directives go inside it. System and Microsoft usings come first, alphabetical, then the rest, alphabetical.
- No `var`. No tuples. No partial classes. One class or enum per file.
- XML documentation on every public class, constructor, member, method, and enum value. No XML docs on private members. Document defaults, minimums, maximums, nullability, thread safety, and thrown exceptions (`<exception>`).
- Private fields are `_PascalCase`. Public members are `PascalCase`. Public members that need validation use explicit getters and setters with backing fields, null checks, and `Math.Clamp`.
- Configurable values are public properties with private backing defaults, not constants.
- Every async method takes a `CancellationToken` (unless the class owns a token or source), checks cancellation where appropriate, and every `await` uses `.ConfigureAwait(false)`.
- Methods returning `IEnumerable` also get an async variant taking a `CancellationToken`.
- Use specific exception types with meaningful messages. Custom exceptions for domain errors. Exception filters where appropriate.
- Guard clauses with `ArgumentNullException.ThrowIfNull` at method start.
- `<Nullable>enable</Nullable>`. Prevent null reference exceptions proactively.
- Use classic `using (...) { }` blocks, not using declarations. Full Dispose pattern with `protected virtual void Dispose(bool disposing)`.
- `Interlocked` for simple atomics. `ReaderWriterLockSlim` for read-heavy locking. Prefer LINQ where readable, `.Any()` over `.Count() > 0`, `.FirstOrDefault()` with null checks.
- No `Console.WriteLine` in library code.
- Regions (`Public-Members`, `Private-Members`, `Constructors-and-Factories`, `Public-Methods`, `Private-Methods`) are not required in files under 500 lines.
- Do not assume members exist on opaque classes. Read the source in `~/Code/<Library>` or ask.
- Compile with zero warnings and zero errors.

## Writing

Never use em-dashes anywhere: code, comments, docs, Postman, commit messages. Rewrite the sentence instead.
