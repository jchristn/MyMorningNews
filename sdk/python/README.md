# My Morning News Python SDK

A Python client for the My Morning News REST API. It covers topics and the refinement wizard, one-time research requests, schedules, runs, findings, sources, predictions, suggestions, feedback, learned preferences, editions, briefings, templates, prompts, audio, stored files, podcast feeds, email, analytics, usage, model endpoints, search providers, voices, the vault, devices, and the account plan.

- Single module: `mymorningnews_sdk.py`
- Python 3.10 or later
- One dependency: `requests`
- Version `0.1.0-alpha` (`mymorningnews_sdk.__version__`; the package metadata uses the PEP 440 form `0.1.0a0`)

Control-plane administration (tenants, users, credentials, roles, permissions, assignments), promo code and plan administration, audit, request history, and vault secret reveal are in the C# SDK only. `client.request(...)` can still reach any route when you need one of them.

## Install

From this folder:

```bash
pip install -r requirements.txt     # just requests
pip install .                       # or install the module as the mymorningnews-sdk package
```

Or copy `mymorningnews_sdk.py` into your project.

## Quick start

```python
from mymorningnews_sdk import MyMorningNewsClient

with MyMorningNewsClient("http://127.0.0.1:12100") as client:
    client.login("admin@mymorningnews", "password")

    topic = client.topics.create(
        name="Solid-state batteries",
        brief="Track electrolyte and manufacturing progress.",
        audience_level="Expert",
    )
    print(topic.id, topic.status)          # top_..., Draft

    detail = client.topics.get(topic.id)   # TopicDetail with dimensions, constraints, recipients
    print(len(detail.dimensions))

    client.topics.delete(topic.id)
    client.logout()
```

The default base URL is `http://127.0.0.1:12100`.

## Conventions

- **Names.** Python arguments, filters, and model fields are snake_case. The SDK sends camelCase on the wire for query parameters and JSON bodies. Bodies may use either form: `{"audience_level": "Expert"}` and `{"audienceLevel": "Expert"}` are the same. Keys inside free-form maps (`variables`, `features`, `details`) are sent exactly as given.
- **Bodies.** Create and update methods take an optional `body` mapping plus keyword fields, merged with the keywords winning: `client.topics.update(tid, {"description": "x"}, novelty_preference=70)`.
- **Models.** Responses are dataclasses (`Topic`, `Run`, `Finding`, `Briefing`, and so on). Every field is optional. Dates are ISO 8601 UTC strings and enums are their string names. Each model keeps the full JSON in `model.raw`, and `model["camelCaseKey"]` or `model.get("snake_key")` reads fields this SDK version does not declare. `model.to_dict()` returns the camelCase JSON.
- **Enums.** String enums such as `TopicStatus`, `RunStatus`, `FeedbackTargetType`, `RelevanceRating`, `IntervalType`, and `ModelProvider` are provided. Plain strings work everywhere too.
- **Python keywords.** Filters named `from` and `all` are passed as `from_` and `all_`.
- **Less common responses** (most analytics reports, ranking recommendations, search tests, email previews, prompt lab results, settings) are returned as plain dicts with the server's camelCase keys. Each method's docstring says so.

## Authentication

```python
# Email and password. Without tenant_id a single matching tenant signs in.
client.login("admin@mymorningnews", "password", tenant_id="default")

# Exchange an access key and secret for a session token.
client.login_with_access_key("ak_...", "sk_...", tenant_id="default")

# Or send the access key on every request instead of a token.
client = MyMorningNewsClient(access_key="ak_...", secret_key="sk_...", tenant_id="default")

# Or reuse an existing bearer token.
client = MyMorningNewsClient(token=saved_token)

# Or sign every request with the secret instead of sending it (MMN1-HMAC-SHA256).
client = MyMorningNewsClient(access_key="ak_...", secret_key="sk_...", tenant_id="default", signed_requests=True)

client.token_details()   # TokenDetails: principal, tenant, admin flags, issued and expiry times
client.validate_token()  # GET /v1.0/token
client.refresh()         # new token; the old one is revoked
client.logout()          # DELETE /v1.0/token, forgets the token
```

### Signed requests

With `signed_requests=True` (which needs `access_key`, `secret_key`, and `tenant_id`), the client sends `x-access-key`, `x-tenant-guid`, `x-date` (ISO 8601 UTC), `x-nonce` (a fresh random value), and `x-signature` instead of `x-secret-key`. The signature is the lowercase hex HMAC-SHA256, keyed with the UTF-8 secret, of:

```
MMN1-HMAC-SHA256
{METHOD}
{path and query exactly as sent}
{tenant}
{x-date}
{x-nonce}
{lowercase hex SHA-256 of the body bytes}
```

The client prepares each request first and signs the exact path, query string, and body bytes that go on the wire. The server rejects a reused nonce or an `x-date` outside its allowed clock skew. A session token, when set, takes precedence over signing. The helpers `sign_request`, `string_to_sign`, and `body_sha256` are public if you need to sign requests yourself:

```python
from mymorningnews_sdk import sign_request

sign_request("secret_test", "GET", "/v1.0/api/topics?pageSize=1", "default",
             "2026-01-01T00:00:00Z", "nonce-12345678", b"")
# 230d17d0e29816b3bec0fd251a730c14a192f0994792062dbebb7512589aae24
```

When an email belongs to several tenants, `login` raises `TenantSelectionRequired`; its `tenants` list holds `TenantChoice(id, name)` entries to retry with.

The client never prints or logs credentials, and `repr(client)` shows only the auth mode.

## Errors

Every non-2xx response raises a subclass of `MyMorningNewsError`:

| Status | Exception |
|--------|-----------|
| 400 | `BadRequestError` (`BadRequest`, `ValidationFailed`) |
| 401 | `AuthenticationError` |
| 403 | `PermissionDeniedError` (adds `reason` and `required_permission`) |
| 404 | `NotFoundError` |
| 409 | `ConflictError` (`TenantSelectionRequired` on login) |
| 410 | `GoneError` (for example `ArtifactExpired`) |
| 416 | `RangeNotSatisfiableError` |
| 429 | `RateLimitError` |
| 5xx | `ServerError` (for example `UpstreamError`, `TtsError`) |

Each error carries `status`, `code` (the server's `error` field), `message`, `request_id`, `details` (a string map or None), and `body` (the parsed response). A server that cannot be reached raises `MyMorningNewsError` with `status == 0` and `code == "ConnectionError"`.

```python
from mymorningnews_sdk import NotFoundError, BadRequestError

try:
    client.topics.get("top_missing")
except NotFoundError as ex:
    print(ex.status, ex.code, ex.message, ex.request_id)

try:
    client.topics.create(description="no name")
except BadRequestError as ex:
    print(ex.code)   # ValidationFailed
```

## Pagination

List methods return a `Page` with `items`, `page_number`, `page_size`, `total_count`, `total_pages`, `duration_ms`, and `has_more`. They accept `page_number`, `page_size`, `sort_by`, `sort_direction` (`Ascending` or `Descending`), `search`, `from_utc`, `to_utc`, and typed filters per route.

```python
from mymorningnews_sdk import iterate, SortDirection

page = client.findings.list(topic_id=tid, min_score=60, page_size=50, sort_direction=SortDirection.DESCENDING)

for finding in iterate(client.findings.list, topic_id=tid, page_size=100):
    print(finding.title)
```

`iterate(list_fn, **filters)` (also `client.iterate`) fetches pages until the last one. A few routes return plain lists rather than pages (`feed_tokens.list()`, `devices.list()`, topic dimensions, run stages); `iterate` yields those once.

## Downloads and byte ranges

Binary routes return a `Download` with `content` (bytes), `content_type`, `file_name`, `status`, `content_range`, `total_bytes`, `etag`, `partial`, `text`, and `save(path)`. Pass `byte_range` as `(start, end)`, `(None, n)` for the last `n` bytes, an int start offset, or a `"bytes=..."` string.

```python
md = client.briefings.download(briefing_id, "md")         # or "html"
md.save("briefing.md")
first_kb = client.artifacts.content(artifact_id, byte_range=(0, 1023))
assert first_kb.partial and first_kb.total_bytes
audio = client.briefings.download_audio(briefing_id)
```

## Waiting for runs

The server has no run event stream, so `runs.wait` polls `GET /v1.0/api/runs/{id}` until the status is terminal (`Completed`, `PartiallySucceeded`, `Failed`, `Cancelled`, or `Skipped`). It raises `WaitTimeoutError` (with `last`) when time runs out.

```python
run = client.topics.run(topic_id)                  # spends model tokens
run = client.runs.wait(run.id, timeout=900, interval=3, on_poll=lambda r: print(r.status))
```

## Resource groups

Each group is an attribute of the client. Groups with records offer `list(**filters)`, `get(id)`, `create(...)`, `update(id, ...)`, and `delete(id)` where the API supports them, plus the route verbs below.

### Server, profile, settings

```python
client.health()            # anonymous
client.client_config()     # anonymous
client.server_info()
client.me(); client.update_me(time_zone="America/Los_Angeles")
client.change_password("old", "new")
client.settings.get()      # administrators; secrets masked
```

### topics

```python
t = client.topics.create(name="Grid storage", brief="Utility-scale batteries")
client.topics.update(t.id, horizon="Months")
client.topics.pause(t.id); client.topics.resume(t.id); client.topics.archive(t.id)
copy = client.topics.duplicate(t.id)
client.topics.add_dimension(t.id, name="Policy", weight="High")
client.topics.add_constraint(t.id, kind="BlockedDomain", value="example.com")
client.topics.constraints(t.id, kind="BlockedDomain")
client.topics.add_recipient(t.id, "me@example.com")
client.topics.set_search_providers(t.id, [provider_id])   # [] restores tenant defaults
client.topics.ranking_recommendation(t.id)
client.topics.run(t.id); client.topics.dry_run(t.id)       # spend tokens
client.topics.learn(t.id)                                  # learning pass
```

### refinement (wizard)

```python
state = client.refinement.start(seed_text="perovskite solar cells", goal="Track efficiency records")
client.refinement.generate_dimensions(state.session.id)   # calls a model
client.refinement.generate_questions(state.session.id)    # calls a model
client.refinement.answer(state.session.id, question_id, answer_text="Lab results only")
client.refinement.generate_context(state.session.id)      # calls a model
client.refinement.suggest_sources(state.session.id)       # calls a model
client.refinement.accept_sources(state.session.id, feed_urls=["https://example.com/feed.xml"])
client.refinement.complete(state.session.id)              # Draft becomes Active
client.refinement.abandon(state.session.id)
client.topics.refine(topic_id)                            # refine an existing topic
```

### requests (one-time research)

```python
rr = client.requests.create(request_text="Compare sodium-ion cell makers", length="Standard")
client.requests.answer(rr.request.id, question_id, skipped=True)
client.requests.replan(rr.request.id)
client.requests.confirm(rr.request.id)                    # run now, or run_at_utc=...
client.requests.cancel(rr.request.id); client.requests.convert(rr.request.id)
```

### schedules

```python
client.schedules.preview(owner_type="Topic", owner_id=tid, interval_type="EveryNDays", interval_value=1,
                         times_of_day=["07:00"], time_zone="UTC", preview_count=5)
s = client.schedules.create(owner_type="Topic", owner_id=tid, interval_type="DaysOfWeek",
                            days_of_week_mask=62, times_of_day=["06:30"], time_zone="America/New_York")
client.schedules.list(owner_type="Topic", owner_id=tid)
```

### runs

```python
client.runs.list(topic_id=tid, status="Completed")
client.runs.get(run_id); client.runs.stages(run_id); client.runs.queries(run_id)
client.runs.results(run_id, disposition="Fetched")
client.runs.llm_calls(run_id)          # paged, oldest first
client.runs.email_preview(run_id); client.runs.cancel(run_id); client.runs.wait(run_id)
```

### findings, sources, predictions, suggestions

```python
client.findings.list(topic_id=tid, tier="MustRead", min_score=70)
client.findings.list(include_hidden=True, pinned=True)   # hidden findings are left out by default
client.findings.get(finding_id).evidence
client.findings.pin(finding_id); client.findings.hide(finding_id)
client.findings.update(finding_id, pinned=False, hidden=False)
client.findings.delete(finding_id)
client.evidence.get(evidence_id)       # EvidenceView with source URL, title, and domain (owner only)
client.sources.list(run_id=run_id); client.sources.get(source_id)
client.predictions.resolve(prediction_id, "Accurate", note="Shipped in March")
client.predictions.accept_review(review_id); client.predictions.reject_review(review_id)
client.suggestions.set_status(suggestion_id, "Snoozed", snoozed_until_utc="2026-12-01T00:00:00Z")
```

### feedback and preferences

```python
from mymorningnews_sdk import FeedbackTargetType, RelevanceRating

fb = client.feedback.submit(FeedbackTargetType.FINDING, finding_id, relevance=RelevanceRating.NOT_RELEVANT,
                            reason="Press release, no data")      # channel defaults to Api; Dashboard, Email, Mobile, Mcp
client.feedback.update(fb.id, reason="Press release with no numbers")
client.feedback.list(topic_id=tid, include_history=True)
client.feedback.delete(fb.id)

p = client.preferences.create(statement="Prefer peer-reviewed sources", kind="Prefer", topic_id=tid)
client.preferences.disable(p.id); client.preferences.enable(p.id)
client.preferences.accept(proposed_id); client.preferences.reject(proposed_id)
```

### editions, briefings, templates

```python
e = client.editions.create(name="Morning", briefing_template_id=template_id, audio_enabled=True)
client.editions.set_topics(e.edition.id, [tid1, tid2])
client.editions.preview(e.edition.id); client.editions.compose(e.edition.id)   # call a model

client.briefings.list(edition_id=e.edition.id, status="Ready")
client.briefings.markdown(briefing_id); client.briefings.html(briefing_id)
client.briefings.pin(briefing_id); client.briefings.regenerate(briefing_id)
client.briefings.generate_audio(briefing_id, voice_profile_id=voice_id)

client.templates.validate(markdown)
t = client.templates.create(content=markdown, name="My template")
client.templates.update(t.template.id, content=new_markdown, change_note="Shorter")
client.templates.versions(t.template.id); client.templates.rollback(t.template.id, 1)
client.templates.export(t.template.id).save("template.md")
client.templates.reset(seeded_template_id)
```

### prompts

```python
p = client.prompts.list(key="briefing.compose").items[0]
client.prompts.get(p.id).variables
override = client.prompts.create_override("briefing.compose", tid, content)
client.prompts.update(override.template.id, content=edited, change_note="Tighter")
client.prompts.versions(override.template.id); client.prompts.rollback(override.template.id, 1)
client.prompts.reset(override.template.id); client.prompts.delete(override.template.id)
client.prompts.lab(key="briefing.compose", variables={"edition.name": "Morning"})   # calls a model
```

### media, artifacts, feed_tokens, public

```python
client.media.list(briefing_id=briefing_id); client.media.download(media_id, byte_range=(0, 65535))
client.artifacts.list(briefing_id=briefing_id); client.artifacts.download_url(artifact_id)

feed = client.feed_tokens.create(name="Car", edition_id=edition_id)   # secret shown once
client.public.podcast_feed(feed.secret)                               # RSS XML
client.feed_tokens.revoke(feed.token.id); client.feed_tokens.purge(feed.token.id)

client.public.promo_code("SPRING-2026"); client.public.asset("logo.png")
client.public.feedback_page(action_token); client.public.unsubscribe(action_token)
```

### email

```python
client.email.test()
client.email.list(status="Sent"); client.email.get(message_id); client.email.resend(message_id)
client.email.preferences(); client.email.set_preferences(run_emails=False)
```

### analytics, usage, llm_calls

```python
client.analytics.overview()                            # OverviewReport
client.analytics.email(from_="2026-11-01T00:00:00Z")   # EmailReport
client.analytics.feedback().by_channel_day             # FeedbackReport
client.analytics.relevance(topic_id=tid, from_="2026-11-01T00:00:00Z")
client.analytics.accuracy(); client.analytics.reasons()
client.analytics.suggestions(); client.analytics.quality()
client.usage.me(); client.usage.report(group_by="Model"); client.usage.timeseries(bucket_minutes=60)
client.llm_calls.list(run_id=run_id); client.llm_calls.get(call_id)
```

### model_endpoints, model_assignments, search_providers, voices

```python
ep = client.model_endpoints.create(name="Local Ollama", provider="Ollama", endpoint="http://127.0.0.1:12120",
                                   embedding_model="all-minilm", supports_embeddings=True)
client.model_endpoints.validate(ep.id); client.model_endpoints.models(ep.id)
client.model_endpoints.test(chat_endpoint_id, prompt="Say ok.")       # may spend tokens
client.model_assignments.set(task="Scoring", topic_id=tid, model_endpoint_id=ep.id)

sp = client.search_providers.create(name="HN", kind="HackerNews", max_results=10)
client.search_providers.test(sp.id, query="battery"); client.search_providers.fetch_test("https://example.com")

v = client.voices.create(name="Kokoro", provider="OpenAiCompatibleSpeech", model="kokoro", voice="af_heart")
client.voices.test(v.id, "Good morning.").save("sample.mp3")
```

### vault, devices, account

```python
cred = client.vault.create(name="Tavily", kind="ApiKey", provider="Tavily", secret="...")   # never returned
client.vault.update(cred.id, secret="rotated"); client.vault.usage(cred.id)

d = client.devices.register("ExponentPushToken[...]", "ios", name="Phone")
client.devices.test(d.id); client.devices.delete(d.id)

client.account.plan(); client.account.redeem("SPRING-2026")
```

### Any other route

```python
client.request("GET", "/v1.0/api/audit", query={"page_size": 10})
```

## Test harness

`test_harness.py` runs against a live server. It signs in as the admin user, creates its own data with names starting `SDK-python-`, exercises every resource group (create, read back, update, list contains it, delete, 404 after delete, and typed errors on bad requests), checks request signing against a known test vector offline and with a temporary credential on the live server (including replay and stale-date refusal), checks that the `demo@mymorningnews` user cannot see the admin's topic, and deletes everything it created. Cases that start research runs or call a paid model are skipped unless you pass `--with-run`.

```bash
./run-tests.sh                       # macOS and Linux
run-tests.bat                        # Windows
./run-tests.sh --with-run --verbose  # include run and model cases, print details
```

The scripts create `.venv` on first use, install `requirements.txt`, and pass every argument to the harness.

| Argument | Default | Environment variable |
|----------|---------|----------------------|
| `--base-url` | `http://127.0.0.1:12100` | `MYMORNINGNEWS_BASE_URL` |
| `--email` | `admin@mymorningnews` | `MYMORNINGNEWS_EMAIL` |
| `--password` | `password` | `MYMORNINGNEWS_PASSWORD` |
| `--tenant` | none | `MYMORNINGNEWS_TENANT` |
| `--demo-email`, `--demo-password` | `demo@mymorningnews`, `password` | `MYMORNINGNEWS_DEMO_EMAIL`, `MYMORNINGNEWS_DEMO_PASSWORD` |
| `--with-run` | off | |
| `--verbose` | off | |

Each case prints one line (`PASS`, `FAIL`, or `SKIP`, its name, and milliseconds), followed by a cleanup report and a summary. The exit code is non-zero when any case or cleanup step fails. Tokens and secrets are never printed.

## License

MIT. See `LICENSE.md` at the repository root.

(c) 2026 Lattice AI LLC
