# Owner Actions

Everything on this list needs you: accounts, credentials, legal and store decisions, and reviews that code can't
produce. Items are grouped by the plan phase they unblock (see `MY_MORNING_NEWS_PLAN.md` section 29). Check items
off as you finish them, or tell me and I'll update the list.

**Handling secrets:** never paste keys, passwords, or private key files into chat, commits, or issues. Put them in
`docker/.env` (git-ignored, see `docker/.env.example`), in EAS secrets for mobile builds, or in your password
manager. Tell me only that a value is in place.

The publisher for every store listing and the copyright holder for every property is **Lattice AI LLC**
("(c) 2026 Lattice AI LLC"). Existing Lattice AI accounts (Apple Developer organization, Google Play Console,
Expo, Mailgun on latticetech.ai) are reused; only the app-specific items below are new.

---

## Now

- [ ] **Anthropic API key** for the default model (`claude-opus-5-5`): set `MYMORNINGNEWS_ANTHROPIC_API_KEY` in
  `docker/.env`, or add it in the dashboard (Configuration, Stored credentials) and select it on the Anthropic
  endpoint. Without it, model calls fail with "needs an API key".
- [ ] **Review the website legal pages** on https://mymorningnews.ai (`privacy.html`, `terms.html`). They are drafts
  written for the alpha.
- [ ] **Confirm the support address** `hello@latticetech.ai` for the website, store listings, and emails, or give
  me a different one.
- [ ] **Website HTTPS:** in the `jchristn/mymorningnews.ai` repository, Settings, Pages, tick "Enforce HTTPS" once
  GitHub has issued the certificate for `www.mymorningnews.ai` as well.
- [ ] **Decide the website's sign-up buttons** until the hosted service exists (Phase 14): keep them, point them at a
  "Get notified" email link, or point them at GitHub for self-hosting.

## Phase 8: Audio and podcasts

- [ ] **Podcast artwork:** Apple Podcasts wants square artwork from 1400 to 3000 pixels (3000 recommended, JPEG or
  PNG, RGB). Feeds use `assets/glyph.png` (1254 pixels) for now. Drop a 3000 by 3000 version at
  `assets/podcast-artwork.png` and tell me, and I'll switch the feed to it.
- [ ] **Listen to a sample** once the stack is up: in the dashboard, Voices, press Test on "Kokoro (local)". Choose
  the narrator and second-host voices you like (Kokoro lists them at http://127.0.0.1:12104/v1/audio/voices).
- [ ] **Reachable feed address:** podcast apps on your phone need a URL they can reach. Set
  `MYMORNINGNEWS_PUBLIC_BASE_URL` in `docker/.env` to an address your phone can open (a LAN address or the hosted
  domain), or subscribe from a desktop app on the same machine.

## Phase 9: Email delivery

- [ ] **Mailgun sending domain** for My Morning News. Suggested: send from `no-reply@mymorningnews.ai` through a
  Mailgun domain such as `mg.mymorningnews.ai` (DMARC alignment means the From domain must match the sending
  domain). Add the DNS records Mailgun shows, then put the API key in `docker/.env`.

## Phase 14: Hosted service

- [ ] **A host** for `app.mymorningnews.ai` (for example the existing AWS account, us-west-2), with Docker installed,
  ports 80 and 443 open, and DNS for `app.mymorningnews.ai` pointing at it.
- [ ] **S3 bucket** for reports and audio (or keep local storage on the host volume). If S3: create the bucket and an
  IAM role or access key limited to it, and set the `MYMORNINGNEWS_STORAGE_*` values in the host's `docker/.env`.
- [ ] **Production secrets** in the host's `docker/.env`: a new signing key, a new admin API key, a new SearXNG
  secret, the Anthropic key, and the Mailgun key. Never reuse the development defaults.
- [ ] **Free-tier cap**: confirm the default monthly cost cap per user (proposed $5.00) and the other Free plan
  limits in plan section 33.2.
- [ ] Optional: a **Cloudflare Turnstile** site key and secret for the sign-up form.

## Phase 16: Mobile apps (iOS and Android, phone and tablet)

The app foundation is built and runs on the iPhone and iPad simulators and an Android emulator. The full, ordered mobile
checklist with exact commands is in [`mobile/OWNER_ACTIONS.md`](mobile/OWNER_ACTIONS.md). The short version:

- [ ] **Fill out [`APP_STORE_SUBMISSION.md`](APP_STORE_SUBMISSION.md)** (repository root). Every field lists the store, the
  console field name, its limits, and a proposed value; supply or confirm each `Owner:` line (57 of them). Needed before
  the first TestFlight and Play internal builds.
- [ ] **Publisher accounts:** confirm the Lattice AI LLC Apple Developer organization enrollment is complete and send
  me the Apple Team ID (not secret).
- [ ] **EAS project:** from `mobile/`, `npx eas-cli@latest login` (account `joel-christner`), then `npx eas-cli@latest init`;
  send me the project ID (it goes in `MYMORNINGNEWS_MOBILE_EAS_PROJECT_ID`).
- [ ] **Live update signing key:** from `mobile/`, `npm run updates-certificate`; commit only `certificate.pem` and keep
  the private key outside Git.
- [ ] **App Store Connect app** for bundle ID `ai.mymorningnews.app`; fill the `eas.json` submit placeholders (Apple ID
  email, App Store Connect app ID, Team ID).
- [ ] **APNs key** for iOS push, through `npx eas-cli@latest credentials`.
- [ ] **Google Play Console app** with package `ai.mymorningnews.app`; upload the first build by hand.
- [ ] **Firebase and FCM V1** for Android push: `google-services.json` as the EAS file variable `GOOGLE_SERVICES_JSON`,
  and the FCM V1 service account key in EAS credentials.
- [ ] **Play service account** for automated submissions.
- [ ] **Expo push access token** (only if the Expo project enforces authenticated push): set
  `MYMORNINGNEWS_PUSH_EXPO_ACCESS_TOKEN` in the server's `docker/.env`.
- [ ] **Universal links:** send me the Apple Team ID and, after the first Play upload, the Play App Signing SHA-256
  fingerprint, so the server can publish the association files.
- [ ] **Reviewer demo account** on the hosted service, with a topic and at least one briefing.
- [ ] **Hold the first public release** until briefings, audio, and in-app account deletion exist (App Review is
  likely to reject an app whose main tabs say "arrives in a later update"). TestFlight and Play internal testing can
  start earlier.
- [ ] **Play foreground service video:** Google Play requires a short video showing background audio playback for the
  media playback foreground service declaration.
- [ ] **Interface review session** with me before any screenshots are captured for the stores, the website, or the
  README.

## Phase 17: Final documentation

- [ ] Read the rewritten README and approve it.
