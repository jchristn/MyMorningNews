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

- [ ] **Fill out `APP_STORE_SUBMISSION.md`** (repository root): supply or confirm every field marked `Owner:`.
  This is required before the first TestFlight and Play internal builds.
- [ ] **Apple Developer Program** (Lattice AI organization): confirm enrollment is complete and send me the Team ID
  (not secret).
- [ ] **App Store Connect app record** for bundle ID `ai.mymorningnews.app`; send me the App Store Connect app ID.
- [ ] **Google Play Console app** with package `ai.mymorningnews.app`.
- [ ] **Expo (EAS) project** under the `joel-christner` account (for example `@joel-christner/mymorningnews`);
  send me the project ID.
- [ ] **Push credentials:** an APNs key (`.p8`) uploaded to EAS for iOS, and a Firebase project with
  `google-services.json` (as the EAS file variable `GOOGLE_SERVICES_JSON`) and the FCM V1 service account key in
  EAS credentials for Android.
- [ ] **Live updates signing key** for `expo-updates` (I'll generate the certificate with the script; you keep the
  private key outside Git).
- [ ] **Reviewer demo account** on the hosted service for Apple and Google review.
- [ ] **Interface review session** with me before any screenshots are captured for the stores, the website, or the
  README.

## Phase 17: Final documentation

- [ ] Read the rewritten README and approve it.
