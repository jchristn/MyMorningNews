# Simulated user testing: full session, 2026-11-20

A full session run under `~/Code/Agents/requirements/SIMULATED_USER_TESTING.md`. The product was cloned fresh from
GitHub, stood up by following the README, and used as five different people would use it: through the dashboard,
the REST API, the MCP server, three SDK harnesses, email, the podcast feed, Grafana, the helper scripts, and the
mobile checks. Nothing in the product was changed. The only repository change outside this report would have been
the `.gitignore` rule for `user-testing/evidence/`, and that rule already exists.

The short version: the core loop works. A topic went from wizard to run to ranked findings to briefing to audio to
podcast feed to email with working feedback and unsubscribe links, all against stubbed providers. Tenant isolation
held under every probe. Six problems are worth fixing before anyone else installs this, and the most painful of them
is that no server setting can be saved from the dashboard or API in the documented Docker stack, which also
blocks the documented way to open sign-up.

## Environment

| Item | Value |
|---|---|
| Commit | `6f0357bd07d892023ee07cb34f9f69c34fd30c7b` (main), cloned from `https://github.com/jchristn/MyMorningNews.git`, clean tree |
| Host | macOS 26.6 (25G72), arm64 |
| Docker | Engine 29.8.2, Compose v5.5.1 |
| Runtimes | .NET SDK 10.0.403 (also 8.0.425), Node 24.21.0, npm 11.19.0, Python 3.14.8 |
| Browser automation | Playwright 1.56.1 with its bundled Chromium (headless) |
| Postman runner | newman 6.2.1 |
| Compose project | `mmn-usertest` (see deviations) |
| Stack ports | 12100 to 12115 as documented; MCP on 12105 |
| Session ports | Benchmark stub on 127.0.0.1:12303 |
| Time zone | Browser and host in Pacific/Honolulu (HST, UTC-10) |

Image IDs after the build (all built locally from the clone except Grafana and pgvector):

| Image | ID |
|---|---|
| `mymorningnews/server:local` | `sha256:dd3fe8b69833` |
| `mymorningnews/dashboard:local` | `sha256:ed1cb6adb265` |
| `mymorningnews/mcp:local` | `sha256:dcf885f2eb14` (rebuilt, see UT-015) |
| `mymorningnews/tts:local` | `sha256:bd9de0941f4e` |
| `mymorningnews/searxng:local` | `sha256:bec41ff8c9e1` |
| `mymorningnews/prometheus:local`, `tempo:local`, `loki:local` | `2924d5919366`, `a5c7fc84be9c`, `eeb89867bfc9` |
| `grafana/grafana-oss:13.0.2` | `sha256:5dad0df181cb` |
| `pgvector/pgvector:0.8.1-pg17` | `sha256:3e8b3adfd27b` |

### Deviations from the documentation, and why

1. **Compose project name.** Every `docker compose` command ran with `COMPOSE_PROJECT_NAME=mmn-usertest`, so the
   stack got fresh `mmn-usertest_*` volumes and a true first boot instead of the owner's `mymorningnews_*` data. The
   variable overrides the `name: mymorningnews` in `compose.yaml` (checked with `docker compose config` before
   anything ran). Container names are fixed (`mymorningnews-*`), which was fine because no such containers existed.
2. **No paid model calls.** No API key was put anywhere. Research, refinement, audio, and search ran against the
   benchmark stub (`dotnet run --project src/Test.Benchmark -c Release -- stub --stub-port 12303`), configured the way a
   user configures their own providers: through the dashboard, as an OpenAI-compatible chat endpoint
   (`bench-stub-chat`), an OpenAI-compatible embeddings endpoint (`bench-hash-384`), a SearXNG search provider pointing
   at the stub's heat pump corpus, and an OpenAI-compatible speech voice, with every Models assignment moved to the
   stub. The email provider stayed `File`. The default embeddings endpoint points at Ollama on the host, which was
   running natively, so a few local, free embedding calls may have reached it before the reassignment.
3. **Reaching the stub from Docker.** The stub listens on `127.0.0.1` only and serves its corpus pages under
   `127.0.0.1:12303` URLs, and its listener rejects any other `Host` header (`Host: host.docker.internal:12303` gets 404).
   To let the containerized server reach it unchanged, a `alpine/socat:1.8.0.3` relay ran inside the server
   container's network namespace (`--network container:mymorningnews-server`) forwarding `127.0.0.1:12303` to the host.
   It had to be recreated after each server restart. This is a test-harness workaround, not a product path.
4. **Settings workaround.** Opening public sign-up through Settings failed (UT-001), so `hosting.publicSignupEnabled`
   was added to `docker/server/mymorningnews.json` in the clone and the server restarted, as a determined user would.
   The factory reset later restored that file.
5. **Postman ordering.** The collection run as written stops working after its own logout request (UT-007), so a
   second pass ran every folder except Token with a fresh token.

### Time to first use

| Step | Elapsed |
|---|---|
| `git clone` from GitHub | 14 s |
| `docker compose up -d --build` (first build, every image) | 8 min 56 s |
| First dashboard sign-in | about 9 min 15 s after starting |
| First completed research run with findings, briefing, and email | about 27 min after starting |

Most of the time between sign-in and the first run went into configuring the stub and scripting the browser, not
into anything the product made hard. With a real Anthropic key in `.env`, a new user would go from sign-in to a run
in a few minutes, provided the wizard's model calls succeed.

## Users simulated

| User | Who they are | What they knew | What they did |
|---|---|---|---|
| **Maya, first-time evaluator** | Found the repository on GitHub, wants to see it work | The README only | Quick start with no key, the no-key first screen, then configured her own (stub) providers and ran the wizard, a run, the briefing, audio, a podcast feed, and the email links |
| **Riley, restricted member** | A colleague the admin added | Her own credentials | Every dashboard route including admin ones typed into the address bar, and API calls against the admin's topic, run, finding, and briefing by ID |
| **Sam, self-signup user** | A stranger on a server with public sign-up | The sign-up page | Sign-up, unverified sign-in, verification, first screen, wizard, data export, account deletion and cancellation, password reset |
| **Dev, integrating developer** | Building against the API | REST_API.md, MCP_API.md, sdk/README.md | curl through the documented auth schemes, signed requests, SSE, every MCP tool with bad inputs, MCP stdio, all three SDK harnesses, the Postman collection |
| **Ops, operator** | Runs the instance for a team | Everything above plus docker/ | Restart, `down` and `up`, Postgres outage, model outage, `update.sh`, Grafana, the email volume, the factory reset with the demo seed, build from source, mobile checks |

## Summary

**33 findings: 0 S1, 6 S2, 13 S3, 14 S4.** By category: 14 Defect, 9 Friction, 5 Documentation, 1 Gap, and 4 Question
(UT-019 is a Gap with a question attached).

The three that matter most:

1. **UT-001.** Saving any server setting returns 500 in the Docker stack, from the dashboard and the API alike, because
   the settings file is a single-file bind mount that the server replaces with a rename. Opening sign-up, the Postman
   settings request, and the sign-up case in all three SDK harnesses fail on it.
2. **UT-002.** The "one-click" rating links in every run email record nothing. They open a form with the rating
   preselected, and the rating is saved only if the reader also presses Submit. Nothing on the page says so.
3. **UT-005.** Any self-signup user (a tenant administrator of a personal tenant) sees the operator's Grafana,
   Prometheus, Tempo, and Loki addresses and the Grafana sign-in `admin / admin` on their Home page, plus a prompt to
   "Change the default password".

What worked well deserves saying too. The first screen with no key is clear and points to the fix. Tenant and member
isolation held under every probe (404s, never another user's data). The podcast feed is valid RSS, serves real MP3
with correct byte ranges and 416s, and its creation dialog warns that `127.0.0.1` is unreachable from a phone. Signed
requests, nonce replay, clock skew, and the one-scheme rule all behave exactly as documented. The MCP server's tool
list matches MCP_API.md exactly, and its input validation errors are clear tool errors. Health reports 503 when
Postgres is down and recovers without a restart. Data, sessions, and settings survived a restart and a full
`down`/`up`. The factory reset with `--demo` finished in 39 s and seeded everything it promised. Dark mode, a 390 px
phone width, and Arabic right-to-left showed no overflow or broken layout. `mobile` `npm run verify` passed, and the
README's build-from-source commands passed with zero warnings.

## Coverage matrix

| Surface | Item | Users | Status |
|---|---|---|---|
| Install | README quick start (`cp .env.example .env`, `docker compose up -d --build`) | Maya | Covered |
| Install | Build from source (`dotnet build`, dashboard `npm ci`, lint, test, build) | Ops | Covered (0 warnings; 457 dashboard tests pass) |
| Install | Hosted overlay (`docker/hosted`, Caddy, TLS) | | Not covered: needs a public domain, DNS, and Mailgun |
| Install | Ollama profile | | Not covered: downloads models; host Ollama already present |
| Install | `DOCKERHUB_README.md` getting started | Maya | Read; same commands as README minus `.env` |
| Helpers | `docker/update.sh` | Ops | Covered, fails (UT-003); `update.bat` read only |
| Helpers | `docker/factory/reset.sh --demo` | Ops | Covered under `mmn-usertest`; `--keep-ollama` read but deliberately not run (UT-006); `reset.bat` not run (Windows) |
| Dashboard | All 47 routes as admin on first run | Maya | Covered, no console or HTTP errors |
| Dashboard | All admin and user routes as a member, including admin URLs typed directly | Riley | Covered |
| Dashboard | Sign-up, verify, forgot and reset password pages | Sam | Covered (reset completed through the API with the emailed token) |
| Dashboard | Topic wizard, all seven steps, manual path, preview run, apply | Maya | Covered (stub cannot answer refinement prompts) |
| Dashboard | Topic page, Run now, Dry run, briefing reader, Make audio, Listen | Maya | Covered |
| Dashboard | Models (endpoints, assignments), Search providers (add, test), Voices (add, test) | Maya | Covered |
| Dashboard | Users (create with probes), Settings (save), Profile (feeds, export, delete) | Ops, Sam | Covered |
| Dashboard | Editions, Today, Compose now | Maya | Covered |
| Dashboard | Insights pages, Predictions, Suggestions, Sources, Feedback | Riley, demo user | Covered by navigation and demo data; not every chart control clicked |
| Dashboard | Templates, Prompts, MCP servers, API explorer, Request history, Audit | Maya | Pages load; editing a template, a prompt, and registering a remote MCP server not exercised |
| Dashboard | Dark theme, 390 px width, Arabic RTL | Maya, demo user | Covered on 12 to 13 representative pages |
| Dashboard | Keyboard: Escape closes dialogs, focus moves into dialogs | Maya | Covered on three dialogs; full tab-order pass not done |
| REST API | README sign-in, OpenAPI, Swagger | Dev | Covered |
| REST API | Session tokens, access keys (direct, signed, session exchange), admin key, `x-password` rule, two-scheme rule | Dev | Covered |
| REST API | Error shapes, validation, long and wrong-type inputs, injection-shaped text | Dev | Covered on users, topics, providers, voices, editions, endpoints, feed tokens, feedback |
| REST API | Member authorization and isolation by ID | Riley | Covered (34 probes) |
| REST API | Server-sent events for a run | Dev | Covered |
| REST API | Every route | Dev | Partly: Postman pass 2 reached 239 of 292 requests with a response; many hit empty or deleted IDs (UT-007) |
| REST API | Range downloads, retention | Dev | Ranges covered on podcast audio; retention not exercised (time-based) |
| MCP | Streamable HTTP: initialize, tools/list vs MCP_API.md, all 26 tools, resources, templates, prompts | Dev | Covered with a JSON-RPC client; not driven from Claude Code, to avoid changing the user's Claude configuration |
| MCP | Bad input: missing, wrong type, extra, out of range, unknown tool, unknown resource | Dev | Covered |
| MCP | `--describe` and `--stdio` from source | Dev | Covered |
| SDK | JavaScript harness | Dev | Covered twice: 89 pass, 1 fail, 8 skip; then 90, 1, 7 with the demo user |
| SDK | Python harness with `--with-run` | Dev | Covered: 81 pass, 3 fail, 4 skip |
| SDK | C# harness | Dev | Covered: 33 pass, 1 fail, 5 skip |
| SDK | SDK READMEs installed into a fresh outside project | | Not covered: the harnesses were used instead |
| Email | Run digest, verify, welcome, password reset, account deletion emails in the `emailout` volume | Maya, Sam | Covered |
| Email | Feedback links, "Tell us why", unsubscribe page | Maya | Covered |
| Podcast | Feed creation, RSS, episode, ranges, chapters, bad token | Maya | Covered |
| Observability | Grafana sign-in, 11 dashboards, data sources, Prometheus targets | Ops | Covered; MCP target down until the profile started, as expected |
| Operations | Server restart, `down`/`up` without `-v`, Postgres stopped, model down | Ops | Covered |
| Mobile | `npm ci`, `npm run verify` | Ops | Covered (passes in 35 s). No web export is documented, so none was run. No simulators. |
| Benchmarks | `stub` command | Ops | Used as the provider stand-in; the suite itself not run |

## Findings

Evidence paths are relative to `user-testing/evidence/` and are grouped by area rather than by finding id, because
many findings came out of the same journey. Request ids are quoted where the server logged a stack trace.

### S2: Major

### UT-001: No server setting can be saved in the Docker stack

- **Severity:** S2
- **Category:** Defect
- **Surface:** Dashboard (Settings), REST API (`PUT /v1.0/api/settings`), all three SDK harnesses, Postman
- **User:** Ops, then Sam
- **Oracle:** Crash (500); `docs/HOSTING.md` says every value can be changed under Server settings
- **Frequency:** Always (4 of 4 attempts across dashboard, curl, and harnesses)

**Steps**
1. Sign in as admin, open Settings, tick "Open public sign-up", press Save.
2. Or: `GET /v1.0/api/settings` with `x-api-key`, flip `hosting.publicSignupEnabled`, `PUT` the `settings` object back.

**Expected:** The setting saves and `GET /v1.0/api/client-config` reports `signupEnabled: true`.
**Actual:** 500 InternalError. The log says `Device or resource busy : '/app/config/mymorningnews.json'` at
`File.Move` in `SettingsService.UpdateAsync` (request `rqi_mwpl5lr1_7ftACVTnSlpVH1vFoYE`). `compose.yaml` mounts
`./server/mymorningnews.json` as a single file, and a rename onto a single-file bind mount fails. A
`mymorningnews.json.tmp` is left behind in `/app/config`. The other documented route,
`MYMORNINGNEWS_HOSTING_*` environment variables, does not work in the base stack either, because `compose.yaml`
passes only an explicit list of variables to the server and none of them are hosting settings.

**Evidence:** `dashboard/j16c-signup-on/`, `api/settings-put-500.txt`, `sdk/*-harness.log` (the sign-up case in each)
**Workaround:** Edit `docker/server/mymorningnews.json` by hand and restart the server.
**Suggested direction:** Mount the config directory instead of the file, or write in place when the target is a
mount; and mention the file-edit route in HOSTING.md.

**Outcome (server):** Fixed and verified (server), branch `worktree-agent-a1d500eac6b4d17a2`. A save that cannot replace the file with a rename (the single-file bind mount answers "Device or resource busy") now writes the file in place and leaves no `.tmp` behind; elsewhere it stays atomic (`Helpers/FileReplacer`). Rerun in Docker: the server published from this tree, run in `mcr.microsoft.com/dotnet/aspnet:10.0` (container `mmn-fix-ut001`, no shared image tag touched) with a copy of `docker/server/mymorningnews.json` bind mounted as a single file at `/app/config/mymorningnews.json`, as `compose.yaml` does; `PUT /v1.0/api/settings` flipping `hosting.publicSignupEnabled` returned 200, the mounted file on the host changed, and `client-config` reported `signupEnabled: true`. HOSTING.md now explains the file and environment routes. Test: `UserTesting.SettingsSaveWithoutRename`.

### UT-002: Email "one-click" rating links do not record a rating

- **Severity:** S2
- **Category:** Defect
- **Surface:** Email, public feedback page
- **User:** Maya
- **Oracle:** README ("an email after every run with one-click feedback links"); the user's expectation
- **Frequency:** Always (3 of 3)

**Steps**
1. Run a topic so a run digest lands in `/data/email`.
2. Open a "Relevant" or "Not relevant" link (`/v1.0/public/feedback/{token}?a=relevant`). Close the tab.
3. `GET /v1.0/api/feedback` as the recipient.

**Expected:** One click records the rating, and the page confirms it.
**Actual:** The page opens with the choice preselected and a Submit button; nothing is stored (feedback count stays
the same after two opens and a plain GET). Only after Submit does it say "We saved 'Not relevant'". A reader who
clicks the link and closes the tab, which is what a one-click link invites, gives no feedback at all.

**Evidence:** `email/journey/02-feedback-oneclick.png`, `email/journey2/01-submitted.png`, `email/oneclick-get-headers.txt`
**Workaround:** Press Submit.
**Suggested direction:** If the extra step is deliberate (link scanners prefetch URLs), say "Confirm your rating"
on the page and stop calling it one-click; otherwise record on open and offer Undo.

**Outcome (server):** Fixed and verified (server, per the owner decision), branch `worktree-agent-a1d500eac6b4d17a2`. The confirm step stays. The page says "Confirm your rating: “Relevant”. It is not saved until you press the button below.", highlights the chosen option, has one button named for it ("Save “Relevant”"), and explains that mail scanners open links on their own. The run and edition emails say rating links open a confirmation page. README, DOCKERHUB_README.md, REST_API.md, the Postman notes, and the plan no longer say one-click. Rerun: a GET of `?a=relevant` records nothing and shows the confirm page; Save records it. Test: `Email.FeedbackLinks`. The website (`~/Websites/mymorningnews.ai/index.html`, "in one click") is a separate repository and still needs the same change.

### UT-003: `docker/update.sh` fails at its first step and never rebuilds

- **Severity:** S2
- **Category:** Defect
- **Surface:** Helper scripts
- **User:** Ops
- **Oracle:** README ("`docker/update.sh` (or `.bat`) rebuilds and recreates the stack without touching data")
- **Frequency:** Always (1 of 1, deterministic)

**Steps**
1. With the stack running, run `./docker/update.sh`.

**Expected:** Images rebuild from the working tree and containers are recreated.
**Actual:** `docker compose pull` fails for every locally built image (`pull access denied for
mymorningnews/server`, and six more), `set -e` stops the script, exit 1. Even if the pull passed, the script runs
`up -d` without `--build`, so it would never pick up new code. `update.bat` has the same commands without stopping on
errors, so on Windows it would take the stack down and bring it back on the old images.

**Evidence:** `ops/update-sh.log`
**Workaround:** `docker compose up -d --build`.
**Suggested direction:** `docker compose pull --ignore-buildable` (or drop the pull), then `up -d --build`.

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. `update.sh` and `update.bat` run `docker compose pull --ignore-buildable` and then `docker compose up -d --build`, stopping on the first error (`update.bat` no longer takes the stack down first). Checked in a scratch copy under project `mmn-fixtest`: the pull passed (pgvector and Grafana only, exit 0), and `docker compose --dry-run up -d --build` showed every local image rebuilding. The owner stack was not touched.

### UT-004: Over-long names crash several create routes with 500

- **Severity:** S2
- **Category:** Defect
- **Surface:** REST API (and the dashboard forms that call it)
- **User:** Dev
- **Oracle:** Crash; REST_API.md says a value out of range is 400 ValidationFailed
- **Frequency:** Always

**Steps**
1. `POST /v1.0/tenants/default/users` with a 5,000 character `firstName`.
2. `POST /v1.0/api/model-endpoints` and `POST /v1.0/api/feed-tokens` with a 5,000 character `name`.

**Expected:** 400 ValidationFailed naming the field and its limit, as editions already do (`name must be 1 to 128 characters.`).
**Actual:** 500 InternalError on all three. The log shows Npgsql `22001: value too long for type character varying(128)`
(request `rqi_mwpl1syl_2gfeb7UjeFe9rLyjwZx`). Limits are also inconsistent: topics and search providers accept a
5,000 character name and store it.

**Evidence:** `api/user-create-probes.txt`, `api/long-name-probes.txt`
**Suggested direction:** Validate lengths in the services against the column sizes, and map Postgres 22001 to 400 as a backstop.

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. Request bodies are checked against their column sizes (`TextLimitAttribute` on the request classes, `RequestValidator` in `RouteBase`), so the three probes now answer 400 `ValidationFailed` ("firstName must be at most 128 characters.", "name must be 1 to 128 characters.") with the field in `details`; topics (200) and search providers (128) refuse instead of truncating. Postgres 22001 maps to 400 as a backstop. Rerun with the 5,000 character probes against a fresh server. Test: `UserTesting.LongTextIsValidation`.

### UT-005: Home shows operator infrastructure and Grafana credentials to sign-up users and members

- **Severity:** S2
- **Category:** Defect
- **Surface:** Dashboard (Home)
- **User:** Sam (self-signup, tenant admin of a personal tenant), Riley (member)
- **Oracle:** `AUTHENTICATION.md` principle of least exposure; convention for a public sign-up service
- **Frequency:** Always

**Steps**
1. Enable sign-up, sign up as a new user, verify, sign in.
2. Scroll Home to "Next steps" and "External services".

**Expected:** A sign-up user sees their own workspace. Operator links and credentials appear only for server operators.
**Actual:** Sam sees Grafana `http://127.0.0.1:12110` with "Credentials admin / admin", plus Prometheus, Tempo, and Loki
addresses, and a "Change the default password" next step that refers to the seeded admin account. Riley sees the
same service links without the credentials, plus "Set up a model endpoint", which she cannot do. On the hosted
service, this card would show the operator's internals to every customer.

**Evidence:** `dashboard/j17d-sam/` (`01-next-steps.png`), `dashboard/j18-member-home/`
**Suggested direction:** Show External services and the default-password step only to global administrators; drop
next steps a user's role cannot act on.

**Outcome:** Fixed and verified (dashboard). External services and their credentials show only to global administrators, never on a personal tenant or on a non-default tenant of a server with sign-up open. "Change the default password" shows only to the seeded (protected) administrator, and "Set up a model endpoint" only to administrators. Rerun on a fresh instance built from source: the admin sees the card; Riley and a sign-up user do not. Tests in `dashboard/src/views/UserTestingFixes.test.jsx`.

### UT-006: `reset.sh --keep-ollama` deletes volumes by name prefix, ignoring the project

- **Severity:** S2
- **Category:** Defect
- **Surface:** Helper scripts
- **User:** Ops
- **Oracle:** Data loss; the script's own description ("reset My Morning News") and the session's isolation rules
- **Frequency:** Found by reading the script; deliberately not run, because it would have deleted the owner's data

**Steps**
1. Run any stack under a project name other than `mymorningnews` (as this session, and the benchmark stack, do).
2. Run `docker/factory/reset.sh --keep-ollama`.

**Expected:** Only the current project's volumes are removed.
**Actual (from the script):** The `--keep-ollama` branch runs `down` without `-v`, then
`docker volume ls --format '{{.Name}}' | grep '^mymorningnews_' | grep -v '_ollama$'` and removes every match. Under any
other project name it removes the default project's volumes (here, the owner's eight `mymorningnews_*` volumes) and
leaves its own. The path without `--keep-ollama` is safe: it uses `docker compose down -v`, which was verified to
touch only `mmn-usertest_*`.

**Evidence:** `docker/factory/reset.sh` lines in the `KEEP_OLLAMA` branch; `ops/volumes-before-reset.txt` and `ops/volumes-after-reset.txt` for the safe path
**Suggested direction:** Derive the prefix from `docker compose config --format json` (`.name`), or list volumes by
the `com.docker.compose.project` label.

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. Both scripts read the project name from `docker compose config` and select volumes by the `com.docker.compose.project` label (keeping the one labeled `com.docker.compose.volume=ollama`); the confirmation shows the project. Rerun of `reset.sh --keep-ollama --yes` under `COMPOSE_PROJECT_NAME=mmn-fixtest` with a PATH shim that skipped every compose up and down: it removed `mmn-fixtest_pgdata` and `mmn-fixtest_media`, kept `mmn-fixtest_ollama` and a decoy `mmn-fixtest-other_pgdata`, and all eight `mymorningnews_*` volumes were still there. `reset.bat` has the same logic and was not run (no Windows host).

### S3: Minor

### UT-007: The Postman collection cannot be run end to end as documented

- **Severity:** S3
- **Category:** Documentation
- **Surface:** `assets/postman/`, REST_API.md "Postman collection"
- **User:** Dev
- **Oracle:** `SIMULATED_USER_TESTING.md` (run the whole collection as written); REST_API.md's instructions

**Steps**
1. `newman run assets/postman/MyMorningNews.postman_collection.json` with the default variables.
2. Second pass: all folders except Token, with `token` set to a fresh session.

**Expected:** A run that walks each resource from create to delete.
**Actual:** Pass 1: Token > "Revoke the current token (logout)" runs eighth, so 279 of 298 requests return 401.
Pass 2: each folder deletes its record before later requests use it (33 requests 404 on IDs that were just deleted),
69 requests go out with an empty path ID, "Create a tenant" overwrites `tenantId` so the control-plane folders
target a tenant that the next request deletes, "Restart the server" takes the server down mid-run (53 requests got no
response), "Update server settings" hits UT-001, and "Change my password" changes the admin password to the example
value. The empty-ID public routes answer 401 rather than 404 (UT-029).

**Evidence:** `postman/newman-cli.txt`, `postman/newman.json`, `postman/newman-pass2.json`
**Suggested direction:** Reorder so deletes come last and logout is at the very end, keep `tenantId` separate from a
created tenant, and move Restart and Change password into a clearly labelled destructive folder.

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. The generator moves every delete into a Cleanup folder (latest folder first, children before parents) and logout, account deletion (refused for the protected admin), and a restart with the administration key into a Run last folder; Create a tenant saves `newTenantId`; Change my password sends `{{password}}` as the new password; Update server settings sends back what Read server settings returned; list requests save the first item's ID; the access key example follows Credentials and uses a Hybrid credential. Rerun with newman 6.2.1 on a fresh database: 298 requests, 0 with 401, 0 with 500, 0 with no response, the restart last, and the admin still signs in with `password` afterwards. The 54 404s left are records a fresh stack has none of (findings, briefings, audio, predictions) and public links with no token.

### UT-008: Wizard "Preview run" gives no feedback at all

- **Severity:** S3
- **Category:** Defect
- **Surface:** Dashboard (topic wizard, Review step)
- **User:** Maya
- **Oracle:** The page's own text ("Preview a dry run, then apply")

**Steps:** On Review, press "Preview run" and wait two minutes.
**Expected:** Progress, then a preview of what the run found.
**Actual:** Nothing on the page changes. The dry run did start and completed in 17 s with 11 findings
(`run_mwpkmnrp_g7n5GcVdlT5iMhfCHcd`). The note under the button says a dry run "is not counted in history", but it
appears in Runs.
**Evidence:** `dashboard/j8-preview-apply/01-preview.png`

**Outcome:** Fixed and verified (dashboard). Preview run now follows the dry run on the Review step (queued, the running stage, then the result with its new findings and links, or the error that stopped it from starting), and the note no longer says a dry run stays out of history. Verified with Vitest against a controlled event stream; the same notice was exercised live on the topic page (UT-009).

### UT-009: Topic page never updates after "Run now"

- **Severity:** S3
- **Category:** Friction
- **Surface:** Dashboard (topic Overview)
- **User:** Maya

**Steps:** On a topic, press Run now and keep the page open.
**Expected:** Last run (and ideally a link to the live run) updates when the run ends.
**Actual:** A toast says "Run queued". The run finished in 2 s, and three minutes later the page still showed
"Last run -" while the API returned `lastRunUtc`. There is no link from the toast to the run.
**Evidence:** `dashboard/j9-runnow/`

**Outcome:** Fixed and verified (dashboard). Run now and Dry run show a live notice that follows the run over SSE (polling as the fallback), links to the run, and refreshes the topic and its runs when it ends. Rerun live: the notice went from Queued to "The run failed: Search failed: ..." without a reload.

### UT-010: Wizard Context step: Next stays disabled until a separate "Save context"

- **Severity:** S3
- **Category:** Friction
- **Surface:** Dashboard (topic wizard)
- **User:** Maya

**Steps:** When the model cannot draft the context, type a topic name, brief, and seed queries.
**Expected:** Next becomes available, as the page header promises "Every step is saved as you go".
**Actual:** Next stays disabled with no hint until "Save context", lower on the page, is pressed.
**Evidence:** `dashboard/j7h-wizard-finish/01-context-filled.png`, `dashboard/j7i-wizard-finish/`

**Outcome:** Fixed and verified (dashboard). Next is available once a brief is typed and saves the brief and settings before moving on; with no brief it says "Write or draft a brief to continue." A failed save keeps the user on Context with the error. Vitest.

### UT-011: Sign-up users with no model key get no warning, then an operator-only error

- **Severity:** S3
- **Category:** Defect
- **Surface:** Dashboard (Home, wizard)
- **User:** Sam

**Steps:** On a server with no Anthropic key, sign up, sign in, start a topic.
**Expected:** The same "model endpoint has no usable API key" banner the admin sees, with something Sam can do.
**Actual:** No banner anywhere. The wizard then fails with "The platform model key is not configured on this server.",
which only the operator can fix and which tells Sam nothing about what to do next.
**Evidence:** `dashboard/j17c-verify/03-first-screen.png`, `dashboard/j17d-sam/02-wizard.png`

**Outcome:** Dashboard part fixed and verified. The warning already detected a missing platform key after commit fa5dd79 (this session ran 6f0357b, where `platform:anthropic` was always treated as set). It now tells users other than global administrators that the server's shared key is not set up, and offers tenant administrators "Use your own API key for {name}" instead of naming the operator's environment variable. Rerun live as a sign-up user. The wizard's server error text is server-side and unchanged.

**Outcome (server):** Server part fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. The error now says "No model key is available for your account yet. Add your own: open Models, add an endpoint for your provider, and save your API key with it. Or ask whoever runs this server to set up its shared model key." Test: `UserTesting.PlatformKeyMessage`.

### UT-012: Account deletion messaging leaves the user guessing

- **Severity:** S3
- **Category:** Friction
- **Surface:** Dashboard (Profile, sign-in)
- **User:** Sam

**Steps:** Profile > Delete my account, type the email, confirm. Then sign in again.
**Expected:** The dialog says when the data goes; afterwards the user is told the deletion is scheduled; signing back
in says the deletion was cancelled.
**Actual:** The dialog says "after the grace period" without the length (7 days). The user lands on "Your session
expired. Sign in again to continue." Signing in again cancels the deletion silently. The AccountDeletion email does
give the date (November 27) and the cancel rule.
**Evidence:** `dashboard/j19-sam-export/02-delete-dialog.png`, `dashboard/j19b-sam-delete/`

**Outcome:** Fixed and verified (dashboard), with two server gaps. The dialog says what is deleted and that signing in cancels it. It gives the date when client config reports `deletionGraceDays`; the server does not send that yet, so for now it says the next page and the email give the date. After deleting, the user lands on a signed-out page with the date from the delete response and how to cancel (no sign-out request is sent, so no "session expired"). The next sign-in says the deletion was cancelled. The server does not report the cancel, so the dashboard remembers the deletion in that browser and also honors a `deletionCancelled` flag on the sign-in response if the server adds one. Rerun live end to end.

### UT-013: Admins see every user's identical "Morning News" edition

- **Severity:** S3
- **Category:** Friction
- **Surface:** Dashboard (Editions, Today)
- **User:** Ops

**Steps:** Create two users, sign in as admin, open Editions and Today.
**Expected:** The admin's own editions, or other users' editions labelled with their owner.
**Actual:** Three rows named "Morning News" with the same description and no owner column; Today's picker offers
"Morning News" three times. Editing or composing the wrong one is easy.
**Evidence:** `dashboard/j21-edition/01-editions.png`

**Outcome:** Fixed and verified (dashboard). Editions start with "Mine" (`mine=true`); administrators can switch to "Everyone's", which adds an Owner column (the email, or You). Today's picker lists only your own editions. Vitest.

### UT-014: Run email times are UTC with no time zone

- **Severity:** S3
- **Category:** Defect
- **Surface:** Email
- **User:** Maya

**Steps:** Leave the profile time zone blank (the dashboard says the browser zone is used), run a topic scheduled in
Pacific/Honolulu.
**Expected:** The email shows the reader's time, or labels the zone.
**Actual:** "Friday, November 20, 10:51 PM" for a run that finished at 12:51 PM local. The server cannot know the
browser zone, so the fallback is UTC, and the email does not say so.
**Evidence:** `email/eml_mwpkpwxk_MEqa5chnTaWPFLc6NqL.txt`
**Suggested direction:** Fall back to the topic's schedule zone, or print the zone.

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. The run email uses the reader's profile zone (then the topic owner's, the topic schedule's, the tenant's, then UTC) and prints the zone ("12:51 PM HST"; zones without a common abbreviation show "UTC+5:30"). Test: `UserTesting.EmailTimeZone` (owner in Pacific/Honolulu).

### UT-015: Fixed image tags make a stale MCP image easy to run

- **Severity:** S3
- **Category:** Friction
- **Surface:** README (MCP server), `compose.yaml`
- **User:** Ops

**Steps:** After building the stack, run the README's `docker compose --profile mcp up -d`.
**Expected:** An MCP server built from the current tree.
**Actual:** It started in about one second from an existing `mymorningnews/mcp:local` image built two hours earlier
by another checkout, because every service has a fixed `image:` tag shared by all compose projects and the README
command has no `--build`. This session's builds also re-tagged the owner's `server`, `dashboard`, and `mcp` images
(same commit, so no harm here). A user who pulls new code and follows the README gets old MCP code.
**Evidence:** `mcp/compose-up.log` (1.2 s), `mcp/compose-build.log`
**Suggested direction:** Add `--build` to the README command, and consider tags that include the project name.

**Outcome (server):** Fixed (documentation), branch `worktree-agent-a1d500eac6b4d17a2`. README and MCP_API.md use `docker compose --profile mcp up -d --build` and explain that the image tags are shared by every compose project and checkout. `update.sh` notes `COMPOSE_PROFILES` for profiles.

### UT-016: Python harness `--with-run` fails on its own one-time request date

- **Severity:** S3
- **Category:** Defect
- **Surface:** SDK (Python harness)
- **User:** Dev

**Steps:** `./sdk/python/run-tests.sh --with-run`.
**Expected:** The requests case passes.
**Actual:** "400 ValidationFailed: Requests can be scheduled up to a year ahead." The harness uses
`run_at_utc="2099-01-01T00:00:00Z"` (`sdk/python/test_harness.py` line 661). The other two failures in that run
are UT-001 and the stub's inability to answer refinement prompts (not a product problem).
**Evidence:** `sdk/python-harness.log`

**Outcome (server):** Fixed, branch `worktree-agent-a1d500eac6b4d17a2`. The harness schedules the request 30 days ahead (`_in_days(30)`). Not rerun against a stack (`--with-run` needs a model); the date is now inside the server's one year window.

### UT-017: Members can list every user in the tenant through the API

- **Severity:** S3
- **Category:** Question
- **Surface:** REST API, Dashboard
- **User:** Riley
- **Oracle:** Another surface (the dashboard denies the page)

**Steps:** As a member, `GET /v1.0/tenants/default/users`.
**Actual:** 200 with every user's email, names, admin flags, and last sign-in time. REST_API.md lists `User:Read`
for the route, and TenantMember evidently holds it, but the dashboard shows the same member "Access denied" on Users.
**Question for the owner:** Should members see the user directory? If yes, the dashboard could show a read-only list;
if no, TenantMember should lose `User:Read`.
**Evidence:** `api/member-lists-users.txt`, `api/member-isolation.txt`

**Outcome:** Dashboard part fixed and verified. Members reach Users and see the directory read-only with a note; only View JSON is offered. Rerun live as Riley.

### UT-018: A run whose only new page cannot be fetched ends as Failed

- **Severity:** S3
- **Category:** Question
- **Surface:** Research pipeline (via MCP `run_topic` and REST)
- **User:** Dev

**Steps:** Run a topic again after every relevant page has been seen, when the one unseen result is on an unreachable host.
**Actual:** `status: Failed`, "Fetch failed: None of the 1 selected pages could be read." Search worked and nothing
new existed, which a reader might expect to be "Completed, nothing new" (the result when the model is down, oddly,
is Completed). A Failed run sends a failure email.
**Question for the owner:** Should a fetch stage with zero readable pages fail the run, or complete it empty with a warning?
**Evidence:** `mcp/tool-calls.txt` (run_topic), run `run_mwplfo9d_JTiR5dqpXJoIqFxlr3Y` stages

**Outcome (server):** Fixed and verified (per the owner decision), branch `worktree-agent-a1d500eac6b4d17a2`. Fetch reading none of the selected pages is no longer a stage failure: the stage succeeds with "Nothing new could be read: none of the N selected pages could be fetched.", the later stages run, and the run ends Partially succeeded, which sends no failure push. Search failures still fail the run. Test: `UserTesting.FetchReadsNothing`. A model outage still ends Completed with fallbacks, as before; that path was not changed.

### UT-019: The mobile app still plans sign-up, password reset, and account deletion

- **Severity:** S3
- **Category:** Gap
- **Surface:** Mobile app
- **User:** Ops

**Steps:** `cd mobile && npm run verify`; read the parity output.
**Actual:** Verify passes (lint, i18n, parity, audit, 353 Jest tests, iOS and Android export). Parity reports six
planned routes: `/signup`, `/verify`, `/forgot-password`, `/reset-password`, `/account/delete`, `/account/export`. The
server ships all of them, and the README presents the app as store-ready. App Store review requires in-app account
deletion for apps that allow account creation.
**Question for the owner:** Is in-app deletion planned before the store submission?
**Evidence:** `mobile/verify.log` (evidence) and `mobile/parity.json` (repository)
**Outcome (2026-11-20):** Addressed. The app has in-app sign-up (shown only when client config has sign-up open, with
the terms and privacy links and the `minimumAge` box), "Check your email" with "Send a new link", verify email and
reset password screens opened by the emailed links, Forgot password, and "Send a new link" when sign-in answers
`403 EmailNotVerified`. With Turnstile on, sign-up opens the web page (the app has no WebView). Account deletion and
export already called the shipped routes and are now marked implemented: parity reports 34 implemented and 0
planned, and `npm run parity:strict` passes. `npm run verify` passes with 391 Jest tests. Still needed from the
server: `/verify*` and `/reset-password*` in `apple-app-site-association`, so the links open the app on iOS.

### S4: Polish

### UT-020: Error messages expose internal names

- **Severity:** S4
- **Category:** Defect
- **Surface:** Dashboard, REST API, MCP
- **Oracle:** `REST_API.md` (messages are for people); CHANGELOG ("Validation messages no longer end with the parameter name")

Examples: "...needs an API key. Add one to the vault and select it on the endpoint. (Parameter 'apiKey')"; "The model did
not return valid DimensionProposalResult output after 3 attempts." (also QuestionBatchResult, TopicContextResult);
"could not be converted to System.Nullable`1[MyMorningNews.Core.Enums.TtsProviderEnum]" and "to System.String" on
bad JSON types, with no list of valid values.
**Evidence:** `dashboard/j1-nokey-wizard/log.txt`, `dashboard/j7e-wizard-manual/`, `api/long-name-probes.txt`, `api/user-create-probes.txt`

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. No parameter suffixes (`ErrorText.Plain` in the pipeline, model gateway, settings, and routes), structured output failures read "The model's reply could not be used as a dimension proposal after 3 attempts...", a value of the wrong type reads "provider must be one of: OpenAiCompatibleSpeech, Disabled." or "email must be text.", and "Value cannot be null." names the value. Rerun with the report's probes. Test: `UserTesting.PlainErrorMessages`.

### UT-021: Duplicate name conflict does not name the field

- **Severity:** S4
- **Category:** Friction
- **Surface:** Dashboard (Add model endpoint)

Saving a second endpoint named "Stub chat" shows "A record with the same unique value already exists." The user
creation dialog does better ("A user with that email already exists in the tenant.").
**Evidence:** `dashboard/j3-stub-endpoints/02-saved-Stub_chat.png`

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. A duplicate now reads "A model endpoint with that name already exists. Choose a different name." with `details.name`. Test: `UserTesting.DuplicateNamesTheField`.

### UT-022: Model assignments are tedious to change

- **Severity:** S4
- **Category:** Friction
- **Surface:** Dashboard (Models > Assignments)

Moving every task to a new endpoint takes eleven selections and eleven separate Save buttons. Topic overrides ask for a
raw topic ID typed into a text box.
**Evidence:** `dashboard/j4-assign/01-assignments-after.png`

**Outcome:** Fixed and verified (dashboard). Assignments gain "Use one endpoint for every task" (confirms, keeps reasoning effort, clears model overrides) and "Save all changes (n)". Topic overrides pick the topic by name, preselected from the topic's Models tab link, and the overrides table shows topic names. Vitest.

### UT-023: Duplicate constraints and blank names are accepted

- **Severity:** S4
- **Category:** Defect
- **Surface:** Dashboard (wizard), REST API

The same blocked domain added twice is stored twice (Review shows "Blocked domains: 2"); a user with a first name of
three spaces is created.
**Evidence:** `dashboard/j7i-wizard-finish/04-after-Next_Review.png`, `api/user-create-probes.txt` (whitespace case)

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. Adding the same blocked domain again (case and spaces ignored) returns the existing constraint (Review would show 1), an update that would duplicate one is 409, blank names are refused, and a user first name of spaces is stored as unset. Test: `UserTesting.BlankAndDuplicateInputs`.

### UT-024: README lists Azure OpenAI; the Add model dialog has no such provider

- **Severity:** S4
- **Category:** Documentation
- **Surface:** README, Dashboard

The provider list offers Anthropic, OpenAI, OpenAI-compatible, Ollama, Gemini, xAI, and Ollama Cloud. If Azure works
through OpenAI-compatible, the README should say so.
**Evidence:** `dashboard/j2-add-model/`

**Outcome (server):** Fixed (documentation), branch `worktree-agent-a1d500eac6b4d17a2`. PolyPrompt has an Azure OpenAI client, but My Morning News has no Azure provider (`ModelProviderEnum`), so the README lists Anthropic, OpenAI, OpenAI-compatible, Ollama, Ollama Cloud, Gemini, and xAI, and the README and the server's hosted web search message no longer name Azure. The plan still describes Azure as a possible provider.

### UT-025: HOSTING.md has a wrong health URL and a wrong default

- **Severity:** S4
- **Category:** Documentation
- **Surface:** `docs/HOSTING.md`

"Check `https://app.mymorningnews.ai/v1.0/health`": that path returns 401; the health route is `/v1.0/api/health`.
`hosting.verificationHours` is documented as default 24; the setting is 48 and the email says 48 hours.
**Evidence:** `setup/health.txt`, `email/` (VerifyEmail)

**Outcome (server):** Fixed (documentation), branch `worktree-agent-a1d500eac6b4d17a2`. HOSTING.md checks `/v1.0/api/health` and gives the verification default as 48 hours, matching `Hosting.VerificationHours`.

### UT-026: SDK README says there is no run event stream

- **Severity:** S4
- **Category:** Documentation
- **Surface:** `sdk/README.md`

"Wait for a run by polling, because the server has no run event stream yet." `GET /v1.0/api/runs/{id}/events` works
(run, stage, and done events, as REST_API.md documents).
**Evidence:** `api/sse.txt`

**Outcome (server):** Fixed (documentation), branch `worktree-agent-a1d500eac6b4d17a2`. `sdk/README.md` and the C# README describe the event stream each SDK reads (`Runs.StreamEventsAsync`, `runs.events` and `runs.follow`, Python `runs.events`).

### UT-027: A used verification link says it is invalid or expired

- **Severity:** S4
- **Category:** Friction
- **Surface:** Dashboard (`/verify`)

Opening the link a second time shows "This link is not valid or has expired. Enter your email to get a new one." The
API knows the real reason ("This link was already used.") and the account is verified, so the user should be told to
sign in.
**Evidence:** `dashboard/j17c-verify/02-verify-again.png`

**Outcome:** Dashboard part fixed (Vitest). The verify page says "This link was already used. If your email is confirmed, sign in." when the server returns error code `LinkUsed`, and keeps the general message otherwise. Not verifiable live until the server sends `LinkUsed`.

**Outcome (server):** Server part fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. A used link answers 400 `LinkUsed` ("This link was already used, so your email address is confirmed. Sign in to continue."); expired is `LinkExpired` and unknown is `LinkInvalid`. Documented in REST_API.md and HOSTING.md. Test: `Account.SignupVerify`.

### UT-028: MCP stdio logs full payloads to stderr and crashes on a late signal

- **Severity:** S4
- **Category:** Defect
- **Surface:** MCP (`--stdio`)

Without `--verbose`, every protocol message, including full tool results, is written to standard error (MCP_API.md
says `--verbose` turns that on). When standard input closes, in-flight requests are dropped as "cancelled by the
client"; a signal after that ends in `Unhandled exception. System.ObjectDisposedException: The CancellationTokenSource
has been disposed.` at `Program.cs` line 48.
**Evidence:** `mcp/stdio.err`, `mcp/stdio2.err`

**Outcome (server):** Partially fixed, branch `worktree-agent-a1d500eac6b4d17a2`. Verified with the report's steps: without `--verbose`, standard error now carries only "[mcp] stdio, REST API ..." (the transport library's own payload logging is silenced in stdio mode), with `--verbose` the payloads appear once, and a SIGTERM after standard input closes ends cleanly with no `ObjectDisposedException`. Not fixed: when standard input closes, Voltaic still cancels requests that are in flight; that is the library's behavior.

### UT-029: Some status codes say less than they could

- **Severity:** S4
- **Category:** Friction
- **Surface:** REST API

An unmatched public route (for example `/v1.0/public/feeds//podcast.xml`) returns 401 "Authentication is required"
rather than 404. With Postgres stopped, every route returns 500 InternalError while health correctly says 503; a
503 with "database unavailable" would tell an operator more.
**Evidence:** `degraded/postgres-down.txt`, `postman/newman-pass2.json`

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. Unmatched `/v1.0/public/...` and `/.well-known/...` paths answer 404 `RouteNotFound`; with Postgres stopped, a route answered 503 `DatabaseUnavailable` with `Retry-After: 10` (health still 503 Unhealthy) and recovered when it came back. Test: `UserTesting.StatusCodes`.

### UT-030: Run email subject counts differ from the run

- **Severity:** S4
- **Category:** Question
- **Surface:** Email

The subject said "10 new (1 must-read)" for a run whose `newFindingCount` was 11; the eleventh was FYI tier and below
the email threshold. Is "new" meant to count only what the email shows?
**Evidence:** `email/eml_mwpkpwxk_MEqa5chnTaWPFLc6NqL.json`

**Outcome (server):** Fixed and verified (per the owner decision), branch `worktree-agent-a1d500eac6b4d17a2`. The subject already counted the findings the email shows; the lead no longer repeats the pipeline's count of every new finding, and an email that shows fewer adds "This email shows the top N of M new findings. The rest are in the full run." Test: `UserTesting.EmailTimeZone` (1 of 2).

### UT-031: Benchmark stub cannot serve a Docker stack and its use is undocumented

- **Severity:** S4
- **Category:** Documentation
- **Surface:** `benchmarks/README.md`

"`$B stub    # then point a server at 127.0.0.1:12503 by hand`" gives no paths (`/v1` for chat, embeddings, and speech;
`/search/<corpus>/r<round>` as a SearXNG endpoint), and the stub binds `127.0.0.1` with strict Host matching and
serves page URLs on `127.0.0.1`, so the documented Docker stack cannot use it without a relay. The same README says
the user-test stack uses 127xx ports, which nothing else documents.

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. `benchmarks/README.md` lists every stub URL with how to configure it, and the stub takes `--stub-bind` and `--stub-host`. Rerun: `stub --stub-bind '*' --stub-host host.docker.internal` answered a search from a container (`curlimages/curl`), and its results linked to `host.docker.internal` pages; no relay needed. The stray 127xx note is gone.

### UT-032: Health responses echo request-style headers

- **Severity:** S4
- **Category:** Defect
- **Surface:** REST API

`GET /v1.0/api/health` returns `Host: 0.0.0.0:8000`, `Accept: */*`, `Accept-Language`, and `Accept-Charset` as
response headers, which leaks the internal bind address and is not meaningful in a response.
**Evidence:** `setup/health.txt`

**Outcome (server):** Fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2`. Watson's request-style default headers (`Host`, `Accept`, `Accept-Language`, `Accept-Charset`) are removed from every response. Test: `UserTesting.NoEchoedHeaders`.

### UT-033: Members see server runtime details

- **Severity:** S4
- **Category:** Question
- **Surface:** Dashboard (Server info)

A member sees the operating system (Ubuntu 24.04.5), .NET version, and database schema version. Harmless on a
private install; worth deciding for the hosted service.
**Evidence:** `dashboard/member-crawl/summary.json` (server-info)

**Outcome:** Dashboard part fixed and verified. Server info shows database, schema, vector search, runtime, operating system, and pending restarts to global administrators only, and leaves out any value the server stops sending. Rerun live as Riley.

**Outcome (server):** Server part fixed and verified (per the owner decision), branch `worktree-agent-a1d500eac6b4d17a2`. Members get the product, version, start time, and uptime; the operating system, .NET, database, schema, pending restarts, and bundled tool links go only to global administrators. Rerun as a member and as the admin. Test: `UserTesting.ServerInfoForMembers`.

## Things that looked wrong but were not product problems

- The wizard could not propose dimensions, questions, or a context with the stub. The stub only answers the research
  pipeline's prompts and returns `ready` to anything else. The product handled the bad output well: three attempts,
  a clear error with a request ID, and a manual path through every step.
- The finding excerpts start with `# ` (a Markdown heading marker) because the stub quotes the page text it was given.
  A real model would likely not; not filed.
- Escape appeared not to close a dialog once; a direct test on three dialogs showed it does.

## Hygiene

- The test stack ran only as `mmn-usertest`. Before the factory reset, `docker compose ... config` confirmed its
  volumes were `mmn-usertest_*`; afterwards all eight `mymorningnews_*` volumes were still present
  (`ops/volumes-before-reset.txt`, `ops/volumes-after-reset.txt`).
- Containers seen on the machine that were not part of this session (`mmn-testdb`, `mymorningnews-bench-postgres`) were
  left alone.
- All test data used `example.test` addresses and passwords that say what they are.
- Evidence lives in `user-testing/evidence/` (ignored by `.gitignore`); the one captured session token was scrubbed.
- Teardown: `COMPOSE_PROJECT_NAME=mmn-usertest docker compose -f compose.yaml -f factory/compose.factory.yaml --profile mcp
  --profile ollama down -v --remove-orphans` removed every session container and the `mmn-usertest_*` volumes; the
  socat relay and the stub were stopped. `docker volume ls` afterwards still lists all eight `mymorningnews_*` volumes.
  The shared image tags `mymorningnews/server:local`, `dashboard:local`, and `mcp:local` now point at images built from
  commit 6f0357b by this session (UT-015).

## Decisions

Decided by the owner on 2026-11-20. Every finding is addressed in this round except where noted.

| Finding | Recommendation | Decision |
|---|---|---|
| UT-001 | Fix now || Address. Server outcome: fixed and verified (server), branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-002 | Fix now (or relabel the page and the README) || Address: keep the confirm step (it stops link scanners); say so on the page, in the email, and in the README. Server outcome: fixed and verified (server: confirm page, email, docs; the website still says one click), branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-003 | Fix now || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-004 | Fix now || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-005 | Fix now || Address. Outcome: fixed and verified (dashboard), branch `worktree-agent-a6c33e078972d88f5` |
| UT-006 | Fix now || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-007 | Fix later || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-008 | Fix now || Address. Outcome: fixed and verified (dashboard), branch `worktree-agent-a6c33e078972d88f5` |
| UT-009 | Fix later || Address. Outcome: fixed and verified (dashboard), branch `worktree-agent-a6c33e078972d88f5` |
| UT-010 | Fix later || Address. Outcome: fixed and verified (dashboard), branch `worktree-agent-a6c33e078972d88f5` |
| UT-011 | Fix later || Address. Outcome: dashboard part fixed and verified, branch `worktree-agent-a6c33e078972d88f5`. Server outcome: server part fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-012 | Fix later || Address. Outcome: fixed and verified (dashboard); the server should add `deletionGraceDays` to client config and `deletionCancelled` to the sign-in response, branch `worktree-agent-a6c33e078972d88f5`. Server outcome: server now sends `deletionGraceDays` in client config and `deletionCancelled` on sign-in, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-013 | Fix later || Address. Outcome: fixed and verified (dashboard), branch `worktree-agent-a6c33e078972d88f5` |
| UT-014 | Fix later || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-015 | Update the docs || Address. Server outcome: fixed (docs), branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-016 | Fix now (one line) || Address. Server outcome: fixed, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-017 | Owner decides || Won't fix the API: members keep the user directory; Address: show the Users page to members read-only. Outcome: dashboard fixed and verified, branch `worktree-agent-a6c33e078972d88f5` |
| UT-018 | Owner decides || Address: a run whose fetch reads nothing ends Partially succeeded with a warning and no failure push. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-019 | Owner decides before store submission || Address: add in-app sign-up, email verification prompt, and forgot password before store submission |
| UT-020 | Fix later || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-021 | Fix later || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-022 | Leave as is, or fix later || Address. Outcome: fixed and verified (dashboard), branch `worktree-agent-a6c33e078972d88f5` |
| UT-023 | Fix later || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-024 | Update the docs || Address. Server outcome: fixed (docs), branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-025 | Update the docs || Address. Server outcome: fixed (docs), branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-026 | Update the docs || Address. Server outcome: fixed (docs), branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-027 | Fix later || Address. Outcome: dashboard part fixed; the live check waits for the server `LinkUsed` code, branch `worktree-agent-a6c33e078972d88f5`. Server outcome: server part fixed and verified (`LinkUsed`), branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-028 | Fix later || Address. Server outcome: partially fixed (in-flight requests on stdin close are the library's), branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-029 | Leave as is, or fix later || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-030 | Owner decides || Address: the subject and summary count what the email shows. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-031 | Update the docs || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-032 | Fix later || Address. Server outcome: fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |
| UT-033 | Owner decides || Address. Outcome: dashboard part fixed and verified, branch `worktree-agent-a6c33e078972d88f5`. Server outcome: server part fixed and verified, branch `worktree-agent-a1d500eac6b4d17a2` |

(c) 2026 Lattice AI LLC
