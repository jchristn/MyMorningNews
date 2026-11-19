# Hosted deployment overlay

Runs the stack as the hosted service at `https://app.mymorningnews.ai` (plan section 33). Caddy terminates TLS on
ports 80 and 443 and serves the API and the dashboard on one origin. All other services stay on `127.0.0.1`.

```bash
cd docker
cp .env.example .env    # set real secrets: signing key, admin key, Anthropic key, S3 storage, Expo token
docker compose -f compose.yaml -f hosted/compose.yaml up -d --build
```

- `hosted/mymorningnews.json` is the server settings file for the hosted profile (CORS limited to the app origin).
  Public sign-up, caps, and egress restrictions are switched on here when Phase 14 adds them.
- Set `MYMORNINGNEWS_APP_DOMAIN` and `MYMORNINGNEWS_ACME_EMAIL` in `.env` to change the domain or the certificate
  contact.
- For artifact storage in S3, set the `MYMORNINGNEWS_STORAGE_*` variables in `.env`.
- Full instructions: `docs/HOSTING.md` (Phase 14).
