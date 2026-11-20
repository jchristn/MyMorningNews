# My Morning News JavaScript SDK

A JavaScript client for the My Morning News REST API. It is a single ES module (`mymorningnews-sdk.js`) for Node 20 and later. It uses the global `fetch` only and has no runtime dependencies. Type declarations ship in `index.d.ts`.

- Package: `mymorningnews-sdk`, version `0.1.0-alpha`, MIT license.
- Default server: `http://127.0.0.1:12100`.
- Every method returns the parsed JSON body (camelCase keys, enums as PascalCase strings, dates as ISO 8601 UTC strings), `null` for `204 No Content`, or a download result for binary routes.

## Install

The SDK is not published to a registry yet. Copy `mymorningnews-sdk.js` (and `index.d.ts` for TypeScript) into your project, or install from this folder:

```bash
npm install /path/to/MyMorningNews/sdk/js
```

```js
import { MyMorningNewsClient } from 'mymorningnews-sdk';
// or, with the file copied next to your code:
import { MyMorningNewsClient } from './mymorningnews-sdk.js';
```

## Quick start

```js
import { MyMorningNewsClient, MyMorningNewsError } from 'mymorningnews-sdk';

const client = new MyMorningNewsClient({ baseUrl: 'http://127.0.0.1:12100' });
await client.login('admin@mymorningnews', 'password', { tenantId: 'default' });

const topic = await client.topics.create({ name: 'Vector databases', description: 'Open source releases and benchmarks' });
await client.topics.addDimension(topic.id, { name: 'Releases', weight: 'High' });

const page = await client.findings.list({ topicId: topic.id, minScore: 60, pageSize: 20 });
for (const f of page.items) console.log(f.relevanceScore, f.title);

await client.logout();
```

## Client options

```js
const client = new MyMorningNewsClient({
    baseUrl: 'http://127.0.0.1:12100', // default
    token: undefined,                  // an existing session token
    accessKey: undefined,              // access key and secret, sent on every request
    secretKey: undefined,
    apiKey: undefined,                 // administration API key (x-api-key)
    tenantId: undefined,               // x-tenant-guid for access keys, the API key, and login
    signedRequests: false,             // sign each request instead of sending the secret
    timeoutMs: 30000,                  // per request, minimum 1
    fetch: undefined,                  // custom fetch implementation
    headers: {}                        // extra headers on every request
});
```

The client is safe to share across concurrent requests. `client.request(method, path, options)` reaches any route the SDK does not wrap; it accepts `query`, `body`, `headers`, `auth`, `raw`, `range`, `signal`, and `timeoutMs`.

## Authentication

| Method | What it does |
|--------|--------------|
| `login(email, password, { tenantId })` | `POST /v1.0/token` with `x-email` and `x-password`; stores the token. |
| `loginWithAccessKey(accessKey, secretKey, { tenantId })` | `POST /v1.0/token` with `x-access-key`, `x-secret-key`, and `x-tenant-guid`; stores the token. |
| `logout()` | `DELETE /v1.0/token`; clears the stored token. |
| `refreshToken()` | `POST /v1.0/token/refresh`; stores the new token. |
| `validateToken()`, `tokenDetails()` | `GET /v1.0/token`, `GET /v1.0/token/details`. |
| `me()`, `updateMe(body)`, `changePassword(current, next)` | The signed-in profile. |
| `health()`, `clientConfig()` | Anonymous server status and client configuration. |
| `serverInfo()`, `settings.get()` | Server information; settings for administrators. |

Access keys can also be sent directly on every request instead of a token:

```js
const svc = new MyMorningNewsClient({ accessKey: 'ak_...', secretKey: '...', tenantId: 'default' });
const me = await svc.me();
```

### Signed requests

With `signedRequests: true` (and `accessKey`, `secretKey`, `tenantId`), the client never sends the secret. Each request carries `x-access-key`, `x-tenant-guid`, `x-date` (ISO 8601 UTC), `x-nonce` (32 random hex characters, unique per request), and `x-signature`: the lowercase hex HMAC-SHA256, keyed with the UTF-8 secret, of

```
MMN1-HMAC-SHA256\n{METHOD}\n{path and query exactly as sent}\n{tenant}\n{x-date}\n{x-nonce}\n{lowercase hex SHA-256 of the body}
```

```js
const signed = new MyMorningNewsClient({ accessKey: 'ak_...', secretKey: '...', tenantId: 'default', signedRequests: true });
await signed.me();
```

The server rejects a reused nonce and an `x-date` outside its allowed clock skew. `signRequest(input)` and `stringToSign(input)` are exported for callers that sign requests themselves. Signing uses `node:crypto`.

When an email exists in several tenants and no tenant is given, `login` throws `TenantSelectionRequiredError` with `tenants: [{ id, name }]`. Retry with `{ tenantId }`.

## Errors

Every failure throws `MyMorningNewsError`:

| Property | Meaning |
|----------|---------|
| `status` | HTTP status, or `0` when no response arrived. |
| `code` | Server error code (`NotFound`, `ValidationFailed`, `Conflict`, `Unauthorized`, `Forbidden`, `PromoCodeInvalid`, `ArtifactExpired`, `RangeNotSatisfiable`, ...), or `Timeout`, `Aborted`, `NetworkError`. |
| `message` | Server message. |
| `requestId` | Server request ID (from the body or the `X-Request-Id` header). |
| `details` | Detail map (field errors, conflict IDs), or `null`. |
| `body` | Parsed error body. |
| `method`, `path` | The request that failed. |

```js
try {
    await client.vault.create({ name: 'x', kind: 'ApiKey', provider: 'Custom' });
} catch (err) {
    if (err instanceof MyMorningNewsError && err.code === 'ValidationFailed') console.log(err.details); // { secret: 'Required.' }
    else throw err;
}
```

## Lists and pagination

List methods take `pageNumber`, `pageSize`, `sortBy`, `sortDirection` (`Ascending` or `Descending`), `search`, `fromUtc`, `toUtc` (strings or `Date`), and the typed filters of each route. They return `{ items, pageNumber, pageSize, totalCount, totalPages, durationMs }`.

`paginate(listFn, options, { maxItems })` is an async generator over every page. Methods are bound, so they can be passed directly:

```js
import { paginate } from 'mymorningnews-sdk';

for await (const topic of paginate(client.topics.list, { status: 'Active', pageSize: 50 })) {
    console.log(topic.name);
}
// also available as client.paginate(...)
```

## Downloads

Binary routes return `{ data: ArrayBuffer, contentType, fileName, status, contentLength, contentRange, totalBytes, etag }` and accept a byte range:

```js
const md = await client.briefings.download(briefingId);                       // Markdown file
const html = await client.briefings.html(briefingId);                         // HTML as text
const head = await client.artifacts.content(artifactId, { range: { start: 0, end: 1023 } }); // 206
const tail = await client.artifacts.content(artifactId, { range: { suffix: 4096 } });
const audio = await client.briefings.downloadAudio(briefingId, { range: { start: 0 } });
```

## Runs

`runs.events(id, { signal })` streams a run's progress from `GET /v1.0/api/runs/{id}/events` (server-sent events). It uses fetch streaming rather than `EventSource`, so the bearer token or the request signature is sent. It yields `{ event, id, data, raw }`, with `data` parsed as JSON:

| Event | Data |
|-------|------|
| `run` | The run, on every change. |
| `stage` | One run stage, whenever it changes. |
| `ping` | `{}`, every 15 seconds. |
| `done` | The final run; the stream then ends. |
| `timeout` | The stream reached its maximum length; call `events` again to reconnect. |

The current run and all its stages come first, so a reconnect needs no event ID. Break out of the loop or abort the signal to stop; an abort throws `MyMorningNewsError` with `code` `Aborted`.

```js
const queued = await client.topics.run(topicId, { dryRun: true }); // real research and model calls
for await (const ev of client.runs.events(queued.id)) {
    if (ev.event === 'stage') console.log(ev.data.stage, ev.data.status);
    if (ev.event === 'done') console.log('finished', ev.data.status);
}
```

`runs.follow(id, onEvent, { signal, maxReconnects })` does the same loop, reconnects after `timeout` (20 times by default), and resolves with the final run. `parseServerSentEvents(chunks)` is exported for other streams.

`runs.wait(id, { timeoutMs, intervalMs, signal, onPoll })` polls `GET /v1.0/api/runs/{id}` instead, until the run reaches `Completed`, `PartiallySucceeded`, `Failed`, `Cancelled`, or `Skipped`. It throws `MyMorningNewsError` with `code` `Timeout` (and the last run in `body`) when the time runs out.

`runs.retry(id, { fromStage })` retries a finished run (202). With `fromStage` set to `Synthesize`, `ReviewPredictions`, `Predict`, `Suggest`, `ComposeBriefing`, or `Notify`, the same run is queued again from that stage and `resumeFromStage` is set; otherwise a new run of the topic is queued. A run that is not finished gets 409, and an unknown stage gets 400. Retrying performs real research and model calls.

## Resource groups

Most groups offer `list(options)`, `get(id)`, `create(body)`, `update(id, body)`, and `remove(id)`, plus the verbs below.

### topics

```js
const t = await client.topics.create({ name: 'Battery chemistry', seedQueries: ['solid state battery'] });
await client.topics.update(t.id, { brief: 'Track solid state battery research.', displayThreshold: 50 });
await client.topics.pause(t.id); await client.topics.resume(t.id); await client.topics.archive(t.id);
await client.topics.setStatus(t.id, 'Paused');
const copy = await client.topics.duplicate(t.id);
await client.topics.addConstraint(t.id, { kind: 'BlockedDomain', value: 'example.com' });
await client.topics.listConstraints(t.id, { kind: 'BlockedDomain' });
await client.topics.addRecipient(t.id, { email: 'me@example.test' });
await client.topics.setSearchProviders(t.id, [{ searchProviderId, enabled: true }]);
await client.topics.learn(t.id);
await client.topics.rankingRecommendation(t.id);
const session = await client.topics.refine(t.id);
```

Dimensions: `listDimensions`, `addDimension`, `updateDimension`, `removeDimension`. Constraints: `listConstraints`, `addConstraint`, `updateConstraint`, `removeConstraint`. Recipients: `listRecipients`, `addRecipient`, `removeRecipient`. Runs: `run(id, { dryRun })`, `dryRun(id)`.

### refinement

```js
const s = await client.refinement.start({ seedText: 'Open source vector databases' });
await client.refinement.generateDimensions(s.session.id);   // model call
await client.refinement.generateQuestions(s.session.id);    // model call
await client.refinement.answer(s.session.id, questionId, { answerText: 'Benchmarks' });
await client.refinement.generateContext(s.session.id);      // model call
await client.refinement.suggestSources(s.session.id);       // model call
await client.refinement.acceptSources(s.session.id, { feedUrls: ['https://example.test/feed.xml'] });
await client.refinement.complete(s.session.id);             // or abandon(id)
```

### requests (one-time research)

```js
const r = await client.requests.create({ requestText: 'Compare open source OCR engines', skipQuestions: true });
await client.requests.answer(r.request.id, questionId, { skipped: true });
await client.requests.replan(r.request.id);
await client.requests.confirm(r.request.id, { runAtUtc: new Date(Date.now() + 3600000) });
await client.requests.cancel(r.request.id); // also convert(id), remove(id)
```

### schedules

```js
const preview = await client.schedules.preview({ intervalType: 'DaysOfWeek', daysOfWeekMask: 62, timesOfDay: ['07:00'], timeZone: 'America/New_York' });
const s = await client.schedules.create({ ownerType: 'Topic', ownerId: topicId, intervalType: 'EveryNDays', intervalValue: 1, timesOfDay: ['06:30'] });
await client.schedules.update(s.id, { enabled: false });
```

### runs

`list({ topicId, status, trigger })`, `get`, `start(topicId, { dryRun })`, `stages`, `queries`, `results(id, { disposition })`, `llmCalls(id, options)` (paged, oldest first), `emailPreview`, `cancel`, `retry(id, { fromStage })`, `events(id, { signal })`, `follow(id, onEvent)`, `wait`, and `RunsResource.isTerminal(status)`.

### findings, evidence, sources

```js
const top = await client.findings.list({ topicId, tier: 'MustRead', sortBy: 'RelevanceScore' });
const detail = await client.findings.get(top.items[0].id); // with evidence
await client.findings.pin(findingId); await client.findings.unpin(findingId);
await client.findings.hide(findingId);                     // left out of lists, briefings, and emails
await client.findings.list({ includeHidden: true, pinned: true });
await client.findings.update(findingId, { pinned: true, hidden: false });
await client.findings.remove(findingId);
const ev = await client.evidence.get(evidenceId);           // owner only; also findings.getEvidence(id)
const src = await client.sources.list({ runId });
```

### predictions, suggestions

```js
await client.predictions.resolve(predictionId, { status: 'Accurate', note: 'Shipped on time.' });
await client.predictions.acceptReview(reviewId); // or rejectReview(reviewId)
await client.suggestions.setStatus(suggestionId, 'Snoozed', { snoozedUntilUtc: new Date(Date.now() + 7 * 86400000) });
```

### feedback

```js
const f = await client.feedback.submit({ targetType: 'Finding', targetId: findingId, action: 'not-relevant', reason: 'Old news' });
// channel: Dashboard, Email (recorded as Api when sent here), Mobile, Api, or Mcp
await client.feedback.update(f.id, { reason: 'Covered last week' });
await client.feedback.list({ topicId, includeHistory: true });
await client.feedback.remove(f.id);
```

### preferences (learned preferences)

```js
const p = await client.preferences.create({ statement: 'Prefer primary sources', kind: 'PreferSource', topicId });
await client.preferences.disable(p.id); await client.preferences.enable(p.id);
await client.preferences.accept(proposedId); // or reject(proposedId)
```

### editions, briefings

```js
const e = await client.editions.create({ name: 'Morning', briefingTemplateId });
await client.editions.setTopics(e.edition.id, [topicA, topicB]);
const preview = await client.editions.preview(e.edition.id);    // may call a model when there are items
const queued = await client.editions.compose(e.edition.id);     // 202
const b = await client.briefings.get(queued.id);
await client.briefings.pin(queued.id); await client.briefings.unpin(queued.id);
await client.briefings.regenerate(queued.id);
await client.briefings.generateAudio(queued.id, { voiceProfileId });
const markdown = await client.briefings.markdown(queued.id);
```

### templates (briefing templates)

```js
const check = await client.templates.validate(markdown);
const t = await client.templates.create({ content: markdown, name: 'Short', shared: false });
await client.templates.update(t.template.id, { content: edited, changeNote: 'Tighter' });
await client.templates.versions(t.template.id);
await client.templates.rollback(t.template.id, 1);
const file = await client.templates.export(t.template.id); // { text, fileName, ... }
await client.templates.reset(seededTemplateId);
```

### prompts

```js
const list = await client.prompts.list({ key: 'briefing.compose', tenantOnly: true });
const o = await client.prompts.createOverride({ key: 'briefing.compose', topicId });
await client.prompts.update(o.template.id, { content, changeNote: 'Topic tone' });
await client.prompts.versions(o.template.id);
await client.prompts.rollback(o.template.id, 1);
await client.prompts.reset(o.template.id);
await client.prompts.lab({ key: 'briefing.compose', variables: { 'edition.name': 'Test' } }); // model call
```

### media, artifacts

```js
const episodes = await client.media.list({ status: 'Ready' });
const audio = await client.media.download(episodeId, { range: { start: 0 } });
const files = await client.artifacts.list({ briefingId });
const url = await client.artifacts.downloadUrl(files.items[0].id);
const bytes = await client.artifacts.download(files.items[0].id);
```

### feedTokens, publicApi

```js
const ft = await client.feedTokens.create({ name: 'Phone' }); // secret and feedUrl shown once
const xml = await client.publicApi.podcastFeed(ft.secret);
await client.feedTokens.revoke(ft.token.id);
await client.feedTokens.purge(ft.token.id);
await client.publicApi.checkPromoCode('WELCOME');
await client.publicApi.asset('logo.png');
```

### email

```js
const m = await client.email.test();                   // 202
await client.email.listMessages({ template: 'Test' });
await client.email.getMessage(m.id);
await client.email.resend(m.id);
await client.email.removeMessage(m.id);
await client.email.runPreview(runId);
await client.email.updatePreferences({ runEmails: true, unsubscribeTopicIds: [topicId] });
```

### analytics, usage, llmCalls

```js
await client.analytics.relevance({ topicId, from: new Date(Date.now() - 30 * 86400000) });
// also overview, accuracy, feedback (with byChannelDay), reasons, suggestions, quality, email,
// or analytics.report(name, options)
await client.usage.me();
await client.usage.report({ groupBy: 'Model' });
await client.usage.timeseries({ bucketMinutes: 60 });
const calls = await client.llmCalls.list({ runId });
await client.llmCalls.get(calls.items[0].id);
```

### modelEndpoints, modelAssignments

```js
const ep = await client.modelEndpoints.create({ name: 'Local', provider: 'Ollama', endpoint: 'http://127.0.0.1:12120', model: 'llama3', supportsChat: true });
await client.modelEndpoints.validate(ep.id);
await client.modelEndpoints.models(ep.id);
await client.modelEndpoints.test(ep.id, { prompt: 'Say hello.' }); // model call
await client.modelAssignments.upsert({ task: 'Synthesis', topicId, modelEndpointId: ep.id });
await client.modelAssignments.list({ topicId });
```

### searchProviders, voices, vault

```js
const sp = await client.searchProviders.create({ name: 'Feeds', kind: 'Rss' });
await client.searchProviders.test(sp.id, { feedUrls: ['https://example.test/feed.xml'] });
await client.searchProviders.fetchTest('https://example.test/article');

const v = await client.voices.create({ name: 'Narrator', provider: 'OpenAiCompatibleSpeech', model: 'kokoro', voice: 'af_heart' });
const sample = await client.voices.test(v.id, { text: 'Good morning.' }); // audio/wav

const cred = await client.vault.create({ name: 'Tavily', kind: 'ApiKey', provider: 'Tavily', secret: process.env.TAVILY_KEY });
await client.vault.usage(cred.id); // secrets are never returned; reveal is in the C# SDK only
```

### devices, account

```js
const d = await client.devices.register({ expoPushToken: 'ExponentPushToken[...]', platform: 'ios' });
await client.devices.test(d.id);
await client.devices.remove(d.id);

const plan = await client.account.plan();
await client.account.redeem('WELCOME'); // 409 PromoCodeInvalid with details.reason when it cannot be redeemed
```

Control-plane administration (tenants, users, credentials, roles, permissions, assignments), promotional code and plan administration, audit, and request history are in the C# SDK only. `client.request(...)` can reach them when needed.

## Test harness

`test-harness.js` runs the SDK against a live server. It signs in, creates its own data with names that start with `SDK-js-`, checks create, read back, update, list, delete, 404 after delete, and typed errors for every group, checks that the `demo@mymorningnews` user cannot see an administrator's topic, and deletes everything it created. It prints one line per case (`PASS`, `FAIL`, or `SKIP`, the name, and milliseconds), a summary, and exits non-zero on any failure. Tokens and secrets are never printed.

```bash
./run-tests.sh                                   # macOS and Linux
run-tests.bat                                    # Windows
npm test                                         # same harness
./run-tests.sh --base-url http://127.0.0.1:12100 --email admin@mymorningnews --password password --tenant default --verbose
```

| Argument | Environment variable | Default |
|----------|----------------------|---------|
| `--base-url` | `MYMORNINGNEWS_BASE_URL` | `http://127.0.0.1:12100` |
| `--email` | `MYMORNINGNEWS_EMAIL` | `admin@mymorningnews` |
| `--password` | `MYMORNINGNEWS_PASSWORD` | `password` |
| `--tenant` | `MYMORNINGNEWS_TENANT` | `default` |
| `--with-run` | | off |
| `--verbose` | | off |

Cases that trigger real research runs or model calls (refinement generation, one-time requests, a dry run with `runs.wait`, the prompt lab, and briefing audio) are skipped unless `--with-run` is given, because the model endpoint of a live stack can cost money. The edition compose and preview cases run on an edition with no active topics, and the harness asserts that composing it made no model calls.

The access key and signed request cases create and delete temporary credentials through `client.request` against the control plane, so they need an administrator. The signed request cases also check a known signature vector offline, check a replayed nonce and a bad signature are rejected, and check that the credential stops working once deleted. The run stream cases parse a canned event stream offline (split chunks, CR and LF line endings, comments, multi-line data, UTF-8 split across chunks) and drive `events` and `follow` against a fake fetch; the live case checks 404, 409, and 400 without queuing anything and replays a finished run when one exists. It skips when the server does not have the events route yet. Finding edit cases skip when the server has no findings; they restore each finding's pinned and hidden flags afterwards and never delete real findings.

## Requirements

- Node 20 or later (global `fetch`, `AbortController`, and async generators).
- A running My Morning News server. The Docker stack in `docker/` serves it on `http://127.0.0.1:12100`.

(c) 2026 Lattice AI LLC
