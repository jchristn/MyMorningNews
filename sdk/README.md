# My Morning News SDKs

Client libraries for the My Morning News REST API, each with a harness that runs against a live server.

| SDK | Folder | Runtime | Package | Docs |
|-----|--------|---------|---------|------|
| C# | [`csharp/`](csharp/) | .NET 8 and .NET 10, no dependencies beyond the BCL | `MyMorningNews.Sdk` 0.1.0-alpha | [README](csharp/README.md) |
| JavaScript | [`js/`](js/) | Node 20+ (global `fetch`), ES module with type declarations | `mymorningnews-sdk` 0.1.0-alpha | [README](js/README.md) |
| Python | [`python/`](python/) | Python 3.10+, `requests` | `mymorningnews-sdk` 0.1.0a0 | [README](python/README.md) |

All three:

- Default to `http://127.0.0.1:12100`.
- Sign in with an email and password, an access key and secret, or signed requests (`MMN1-HMAC-SHA256`, so the secret never travels), and log out.
- Raise a typed error carrying the HTTP status, error code, message, request ID, and details from the server's `{ error, message, requestId, details }` body.
- Page through lists with `pageNumber`, `pageSize`, `sortBy`, `sortDirection`, `search`, `fromUtc`, `toUtc`, and per-resource filters, and offer a helper that walks every page.
- Download briefings (Markdown, HTML, audio) and stored files, with byte ranges.
- Wait for a run by polling, because the server has no run event stream yet.

The C# SDK also covers the control plane (tenants, users, credentials, roles, permissions, assignments), promotional codes and plans, audit, request history, settings, and vault reveal. The MCP server (`src/MyMorningNews.McpServer`, see [`MCP_API.md`](../MCP_API.md)) is built on it.

## Signed requests

Each request carries `x-access-key`, `x-tenant-guid`, `x-date` (ISO 8601 UTC), `x-nonce` (8 to 128 characters, unique per request), and `x-signature`: the lowercase hex HMAC-SHA256, keyed with the UTF-8 secret key, of these lines joined by a line feed:

```
MMN1-HMAC-SHA256
GET
/v1.0/api/topics?pageSize=1
default
2026-01-01T00:00:00Z
nonce-12345678
e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
```

The last line is the SHA-256 of the body (here, of nothing). With the secret `secret_test` this vector signs to `230d17d0e29816b3bec0fd251a730c14a192f0994792062dbebb7512589aae24`, and every SDK's harness checks it. The server allows 300 seconds of clock skew (`Auth.SignatureMaxSkewSeconds`) and refuses a nonce it has seen. A credential with auth mode `SignedRequest` accepts only signed requests.

## Harnesses

Each harness creates its own data, prefixed `SDK-<language>-`, removes it afterward, and exits non-zero on any failure. Cases that would call real models or start research runs are skipped unless you pass `--with-run`.

```bash
./sdk/csharp/run-tests.sh
./sdk/js/run-tests.sh
./sdk/python/run-tests.sh
```

Each accepts `--base-url`, `--email`, `--password`, `--tenant`, `--with-run`, and `--verbose`, and reads `MYMORNINGNEWS_BASE_URL`, `MYMORNINGNEWS_EMAIL`, and `MYMORNINGNEWS_PASSWORD`. The cross-user checks sign in as `demo@mymorningnews` / `password`, which the factory demo seed creates. On Windows, use the `.bat` scripts.

The `SDK harnesses` job in `.github/workflows/tests.yml` starts PostgreSQL and the server, seeds the demo member, and runs all three.

## Publishing

`.github/workflows/publish-sdks.yml` publishes all three when a tag such as `sdk-v0.1.0-alpha` is pushed (or when run by hand): NuGet `MyMorningNews.Sdk`, npm `mymorningnews-sdk` (dist tag `alpha`), and PyPI `mymorningnews-sdk`. Each registry is skipped until its repository secret exists: `NUGET_API_KEY`, `NPM_TOKEN`, `PYPI_API_TOKEN`. Versions change only with the owner's approval.

## Regenerating models

The C# and Python models are generated from `src/MyMorningNews.Core`. After changing a model, request, response, or enum there, run:

```bash
python3 sdk/csharp/tools/generate_models.py
```

and update the Python dataclasses and the JavaScript typedefs and `index.d.ts` to match (their READMEs say where).

---

(c) 2026 Lattice AI LLC
