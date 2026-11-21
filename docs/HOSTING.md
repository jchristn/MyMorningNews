# Hosting My Morning News

This guide runs the stack as the public hosted service at `https://app.mymorningnews.ai` (plan section 33). A
self-hosted install does not need any of it: the defaults keep sign-up closed, give everyone the Unlimited plan, and
allow private network targets.

(c) 2026 Lattice AI LLC

## What the hosted profile changes

| Setting | Self-hosted default | Hosted profile |
|---|---|---|
| `hosting.publicSignupEnabled` | false | true |
| `hosting.defaultPlanKey` | unlimited | free |
| `hosting.requireEmailVerification` | true | true |
| `hosting.restrictPrivateNetworks` | false | true |
| `hosting.usePlatformCredentials` | false | true |
| `models.allowAgentClis` | true | false (set by `MYMORNINGNEWS_MODELS_ALLOW_AGENT_CLIS` in `hosted/compose.yaml`) |
| `rest.corsAllowedOrigins` | dashboard on 127.0.0.1 | `https://app.mymorningnews.ai` |
| TLS | none (127.0.0.1 only) | Caddy on 80 and 443 with automatic certificates |

The settings live in `docker/hosted/mymorningnews.json`, mounted into the server container. Every value can also be
changed in the dashboard under Server settings (saved back to that file), by editing the file and restarting the
server (`docker compose -f compose.yaml -f hosted/compose.yaml restart server`), or with the `MYMORNINGNEWS_HOSTING_*`
environment variables. An environment variable reaches the server only when the compose file passes it: the hosted
overlay passes the Turnstile and Apple ones listed below, and the base `docker/compose.yaml` passes none, so add any
other one to the server's `environment:` list first. The overlay also sets `MYMORNINGNEWS_MODELS_ALLOW_AGENT_CLIS`
to `false` (see [Agent CLIs](#agent-clis)).

## Before you start

1. A Linux host with Docker and Compose, ports 80 and 443 open to the internet, and every other port closed.
2. DNS: an `A` (and `AAAA`, if you have IPv6) record for `app.mymorningnews.ai` pointing at the host.
3. A Mailgun sending domain, verified (SPF, DKIM, and DMARC records published). Sign-up cannot work without email.
4. An Anthropic API key for the platform. Hosted users run on it; they never see it.
5. S3-compatible storage for audio and briefing files (recommended; local disk works for a small service).
6. Optional: a Cloudflare Turnstile site key and secret key for the sign-up human check.
7. Optional: an Expo access token for push notifications to the mobile app.

## Steps

```bash
cd docker
cp .env.example .env
```

Edit `.env`:

- `MYMORNINGNEWS_AUTH_SIGNING_KEY` and `MYMORNINGNEWS_AUTH_ADMIN_API_KEY`: long random values (`openssl rand -hex 32`).
- `MYMORNINGNEWS_ANTHROPIC_API_KEY`: the platform key.
- `MYMORNINGNEWS_PUBLIC_BASE_URL` and `MYMORNINGNEWS_APP_BASE_URL`: both `https://app.mymorningnews.ai`.
- `MYMORNINGNEWS_EMAIL_PROVIDER=Mailgun`, plus `MYMORNINGNEWS_MAILGUN_DOMAIN` and `MYMORNINGNEWS_MAILGUN_API_KEY`.
- `MYMORNINGNEWS_STORAGE_*` for S3.
- `MYMORNINGNEWS_HOSTING_TURNSTILE_SITE_KEY` and `MYMORNINGNEWS_HOSTING_TURNSTILE_SECRET_KEY` if you use Turnstile.
- `MYMORNINGNEWS_HOSTING_APPLE_TEAM_ID` once the Apple Developer account is approved (turns on iOS universal links).
- Change the Postgres and Grafana passwords.

Then start the stack with the overlay:

```bash
docker compose -f compose.yaml -f hosted/compose.yaml pull
docker compose -f compose.yaml -f hosted/compose.yaml up -d
```

Caddy gets a certificate on first start. Check `https://app.mymorningnews.ai/v1.0/api/health`, then sign in with the
administrator account and change its password at once.

## Behind a load balancer

When a load balancer such as an AWS Application Load Balancer terminates TLS, use the self-contained deployment in
`docker/production/` (generated from `docker/compose.yaml` and `hosted/compose.elb.yaml`). It runs no Caddy, publishes
only the server and the dashboard on the host's interfaces, and needs no source code on the host: copy
`production/compose.yaml`, `production/update.sh`, `hosted/mymorningnews.json` (as `mymorningnews.json`), and a
filled-in `.env` into one folder, such as `~/mymorningnews`, and run `./update.sh`
(`docker compose pull && docker compose down && docker compose up -d && docker ps -a`). See
`docker/production/README.md`.

| Target group | Host port | Health check | Listener rule |
|--------------|-----------|--------------|---------------|
| API (server) | 8000 (HTTP, same port in the container) | `GET /v1.0/api/health`, 200 | Paths `/v1.0/*`, `/openapi.json`, `/swagger*`, `/.well-known/*` |
| Dashboard | 8080 (HTTP 80 in the container) | `GET /health`, 200 | Default (every other path) |

- Allow both ports only from the load balancer's security group. Every other service stays on `127.0.0.1`.
- The HTTPS listener forwards plain HTTP to both target groups. Redirect port 80 to 443 on the load balancer.
- Set the idle timeout to about 120 seconds. Run progress streams send a keep-alive every 15 seconds, and a model
  connection test on the Setup page can take up to a minute.
- `hosting.trustForwardedFor` (on in the hosted profile) takes the client address from the last `X-Forwarded-For`
  entry, which is the one the load balancer appends, so per-IP rate limits see real clients.
- The dashboard talks to the API on its own origin when it is served over HTTPS, so no dashboard setting changes.

## Sign-up and accounts

- `POST /v1.0/public/signup` creates a personal tenant and an unverified user, then emails a link to `/verify`.
  The same answer comes back whether or not the email already has an account, so the form cannot be used to find
  out who is a customer.
- Sign-in is refused (403 `EmailNotVerified`) until the link is used. `POST /v1.0/public/verify-email/resend` sends a
  new one. Links last `hosting.verificationHours` (default 48). A link that was already used answers 400 `LinkUsed`
  (the address is confirmed, so the reader can sign in); an old one answers `LinkExpired`.
- `POST /v1.0/public/password/forgot` and `POST /v1.0/public/password/reset` handle lost passwords. A reset signs out
  every session and sends a confirmation email. Reset links last `hosting.passwordResetMinutes` (default 60).
- Sign-up is rate limited per address (`hosting.signupsPerIpPerHour`, default 5), and addresses at
  `hosting.blockedEmailDomains` are refused. Users confirm they accept the terms (`hosting.termsVersion`) and are at
  least `hosting.minimumAge` (18).
- A promotional code entered at sign-up is redeemed after the account is created.
- Every API request counts against `hosting.requestsPerMinutePerIp` (600 on the hosted profile, off when 0); past
  it the server answers 429 with `Retry-After: 60`. Health checks are exempt.
- `hosting.trustForwardedFor` (on in the hosted profile) takes the client address from the last `X-Forwarded-For`
  entry when the direct peer is on a private network, which is Caddy on the Docker network. Caddy replaces any
  `X-Forwarded-For` a client sends, so the address cannot be forged through it. Leave it off when the server is
  reachable without the proxy.

## Plans and caps

Users without a plan assignment get `hosting.defaultPlanKey` (Free on the hosted profile). Limits are enforced as
402 `CapReached` with `key`, `limit`, and `resetUtc` in `details`:

| Limit | Where it is checked |
|---|---|
| `usage.monthlyCostUsd.max` | before every model call, every manual run, and every scheduled run (a scheduled run past the cap is recorded as Skipped with the reason) |
| `runs.daily.max` | manual and scheduled runs, counted since midnight UTC |
| `topics.max` | creating, duplicating, and starting the wizard for a topic |
| `schedule.minIntervalMinutes` | saving a schedule (400 with a message naming the limit) |
| `audio.enabled`, `audio.monthlyMinutes.max` | making audio for a briefing |
| `endpoints.custom` | adding a model endpoint (administrators are exempt) |

`GET /v1.0/api/account/usage` returns the usage meter the dashboard and mobile app show. Edit plan values in the
dashboard under Administration, Plans; changes apply at once.

## Account deletion and export

- `POST /v1.0/api/account/delete` signs out every session and schedules the purge after `hosting.deletionGraceDays`
  (default 7). Signing in again before then cancels it. The retention worker purges due accounts: a personal tenant
  goes with its last user.
- `GET /v1.0/api/account/export` downloads a ZIP of the profile, topics, findings, predictions, suggestions,
  feedback, learned preferences, and every briefing as Markdown.
- The website's delete-account page explains both for people without the app.

## Platform credentials

With `hosting.usePlatformCredentials` on (and always for personal tenants), a new tenant's Anthropic endpoint
refers to the credential `platform:anthropic`. It resolves from `MYMORNINGNEWS_ANTHROPIC_API_KEY` at call time and
is never copied into a tenant vault, so tenants cannot read or reveal it. Tenants cannot assign it to their own
endpoints, and an endpoint that uses it cannot be pointed at a different provider or URL.

`GET /v1.0/api/setup` reports such an endpoint with `usesPlatformKey` true, and `POST /v1.0/api/setup/model-key`
refuses it with 400 because there is nothing to enter. When the platform key is missing, the model check reads
Missing ("The hosted service's model key is not configured"), so the topic wizard and one-time requests answer 409
`SetupRequired` until the operator sets `MYMORNINGNEWS_ANTHROPIC_API_KEY`.

## Agent CLIs

A server can run Claude Code, Codex, Gemini CLI, and Mux inside the server container as model endpoints, so a
workspace can use an AI subscription (a `claude setup-token` token, a Codex `auth.json`) instead of an API key. The
hosted profiles allow them for global administrators only:

```yaml
MYMORNINGNEWS_MODELS_ALLOW_AGENT_CLIS: "true"
MYMORNINGNEWS_MODELS_AGENT_CLIS_ADMINS_ONLY: "true"
```

- The CLIs run as the server's own user. Each endpoint gets a private home folder and an empty working folder in the
  CLI's most restricted permission mode, but anyone can sign up on the hosted service, and sign-ups are administrators
  of their own workspace, so they must not be able to start programs on the server.
- The "Already signed in on the server" method uses the operator's own sign-in, which only operators should use.

For everyone else, `GET /v1.0/api/setup/options` lists the CLIs with `available` false, `turnedOff` true, and a reason,
and `POST /v1.0/api/setup/connect` and `POST /v1.0/api/model-endpoints` refuse them with 400; they use the platform key
or a provider API key. `MYMORNINGNEWS_MODELS_ALLOW_AGENT_CLIS=false` turns the CLIs off for everyone.

## Private network blocking

With `hosting.restrictPrivateNetworks` on, the server refuses outbound requests to loopback, private (10/8,
172.16/12, 192.168/16), link-local and cloud metadata (169.254/16, fd00:ec2::254), carrier-grade NAT (100.64/10),
unique-local IPv6, multicast, and unspecified addresses, and host names ending in `.localhost` or `.internal`. The
check runs when model endpoints, search providers, and MCP servers are saved (400 `EgressBlocked`) and again on every
connection after DNS resolution and redirects, so a host name that later resolves to a private address is still
refused. Page fetches to such addresses are recorded as Blocked.

The stack's own services (SearXNG, the text-to-speech server, Ollama) resolve to private addresses on the Docker
network, so their host names are listed in `hosting.allowedPrivateHosts` (default `searxng`, `tts`, `ollama`) and
are reached as usual. Add a name there only for a service you run yourself; anything on the list is reachable by
tenant-configured URLs too.

## Universal links

`/.well-known/apple-app-site-association` and `/.well-known/assetlinks.json` are served once
`hosting.appleTeamId` and `hosting.androidCertificateFingerprints` are set. Get the Android SHA-256 fingerprint from
Google Play Console, under Setup, App signing.

## Operations

- Grafana (`127.0.0.1:12110`, reach it over SSH) has dashboards for the pipeline, email, auth, workers, and MCP.
- Back up the Postgres volume daily (`docker compose exec postgres pg_dump -U postgres mymorningnews`).
- Update with `./update.sh` and the same `-f` flags.
- The support address is hello@latticetech.ai.
