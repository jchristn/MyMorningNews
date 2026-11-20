# Mobile app: owner actions

Everything only the owner (Lattice AI LLC) can do for the iOS and Android apps, in the order it unblocks work. Store
metadata, privacy answers, age rating, screenshots, and review notes are a separate fill-in form:
[`APP_STORE_SUBMISSION.md`](../APP_STORE_SUBMISSION.md) at the repository root. Never paste passwords, private keys,
or tokens into this repository; they go into the consoles, EAS, or the password manager.

## Now (unblocks device testing and push)

1. **Decide the publisher accounts.** Confirm the apps publish under the Lattice AI LLC organization accounts (the
   same as BJJ with Joel and Mux: D-U-N-S on file, support `hello@latticetech.ai`). If the Apple Developer Program
   organization membership is still in enrollment, finish it; record the **Apple Team ID** (it goes into `eas.json`
   `submit.*.ios.appleTeamId` and the universal link file).
2. **Create the EAS project.** From `mobile/`: `npx eas-cli@latest login` (account `joel-christner`), then
   `npx eas-cli@latest init`. Copy the project ID into the EAS environment variable
   `MYMORNINGNEWS_MOBILE_EAS_PROJECT_ID` for the development, preview, and production environments (and your shell
   for local builds), or paste it as the default in `app.config.js` (the line marked PLACEHOLDER). Push tokens and
   over-the-air updates stay off until this exists.
3. **Generate the update signing key.** From `mobile/`: `npm run updates-certificate`. Commit
   `mobile/updates/certificate.pem`; store `~/.mymorningnews/keys/updates/private-key.pem` in the password manager.
   Never commit the private key (the script refuses to write it inside the repository).
4. **Free personal-team device testing (optional, before the paid team is ready).** Set
   `MYMORNINGNEWS_MOBILE_LOCAL_TEAM_ID` to your personal team ID and follow
   [`docs/MOBILE_DEVICE_TESTING.md`](../docs/MOBILE_DEVICE_TESTING.md). Push and universal links do not work with a
   personal team.

## Before the first TestFlight and Play internal build

5. **App Store Connect app record.** Create the app: name "My Morning News", bundle ID `ai.mymorningnews.app`
   (register it in Certificates, Identifiers & Profiles with Push Notifications and Associated Domains enabled),
   SKU `mymorningnews-ios`, primary language English (U.S.). Record the **App Store Connect app ID (ascAppId)** and
   the **Apple ID email** used for submissions; put both into `eas.json` `submit.preview.ios` and
   `submit.production.ios` (replace the `REPLACE_WITH_...` placeholders).
6. **APNs key.** Let `npx eas-cli@latest credentials` create (or upload) an APNs authentication key (.p8) for the
   team. Keep the .p8 out of Git (`mobile/*.p8` is ignored).
7. **Google Play Console app.** Create the app "My Morning News" with package `ai.mymorningnews.app` under the
   organization developer account. The very first AAB must be uploaded by hand (Testing > Internal testing > Create
   release) before `eas submit` can upload.
8. **Firebase and FCM V1 for Android push.** Create a Firebase project, add the Android app
   `ai.mymorningnews.app`, download `google-services.json` and upload it as the EAS file variable
   `GOOGLE_SERVICES_JSON` (local builds: `mobile/google-services.json`, git-ignored). Create an FCM V1 service
   account key and upload it in `npx eas-cli@latest credentials` (Android > Push notifications).
9. **Play service account for submissions.** Create a Google Cloud service account with "Release apps to testing
   tracks" in Play Console, keep its JSON key outside the repository, and set its path in `eas.json`
   `submit.*.android.serviceAccountKeyPath` (or upload it in `eas credentials`).
10. **Expo push access token for the server.** Create an Expo access token with push permissions and store it in the
    server's credential vault for the PushWorker (plan section 35.5). Enable "Enhanced push security" on the EAS
    project if you want the server to be the only sender.
11. **Universal links.** Give the backend agent the Apple Team ID and, after the first Play upload, the Play App
    Signing SHA-256 certificate fingerprint (Play Console > App integrity), so `app.mymorningnews.ai` can serve
    `/.well-known/apple-app-site-association` and `/.well-known/assetlinks.json` (plan section 33.3).
12. **Hosted service reachable.** `https://app.mymorningnews.ai` must be live (plan Phase 14 and `docs/HOSTING.md`)
    before TestFlight external testing or review: reviewers sign in against it.
13. **Reviewer demo account.** On the hosted service, create a demo user with a topic and at least one briefing
    with audio. Enter its credentials only in App Store Connect (App Review Information) and Play Console (App
    access).

## Before public release

14. **Decide whether the first public release waits for content.** Today and Topics now work against Phases 4 to 7
    (briefings, topics, the wizard, requests, runs, findings, predictions, suggestions, editions), and audio, feedback,
    Insights, learned preferences, plans and codes, email preferences, and podcast feeds work against Phases 8 to 10.
    Account deletion and export (Phase 14) are what still show "This arrives in a later server update"; both stores
    require in-app deletion. Recommendation: TestFlight and Play internal testing now, public submission after Phase 14.
15. **Account deletion route.** In-app deletion (`POST /v1.0/api/account/delete`, contract E) is required by both
    stores; until it ships, the app links to `https://mymorningnews.ai/delete-account.html`. Confirm that page works
    without the app and names the app and the developer (Google requirement).
16. **Legal pages.** Review the privacy policy, terms, and support pages on `mymorningnews.ai` (drafts from Phase 15)
    and confirm they describe the app's data (account email, usage data, push token).
17. **Interface review, then screenshots.** Review the app with the agent on an iPhone, an iPad, an Android phone,
    and an Android tablet. Only after you approve the interfaces are screenshots captured (plan section 34.1); none
    are committed before that.
18. **Approve every proposed store text and answer** in `APP_STORE_SUBMISSION.md` (name, subtitle, descriptions,
    keywords, categories, age rating, privacy label, data safety, review notes), and fill the `[ ] Owner:` lines:
    pricing and availability countries, App Review contact phone, tax and banking status (needed only if paid
    features arrive), content rights.
19. **Version approval.** The store version is `0.1.0` (build 1) and the release label `0.1.0-alpha`. Any change to
    these needs your explicit approval.
20. **Store badges on the website.** After the apps are live, add the App Store and Google Play badges to
    `mymorningnews.ai` (plan section 34).

## Research screens (Phases 4 to 7)

21. **Add a model key on the server you test against.** Without one, the wizard's generation steps and one-time request
    planning answer `502 ModelError`; the app says so and offers the manual path (add dimensions, skip questions, write
    the brief), but the full experience needs a key in the vault selected on the Refinement endpoint.
22. **Review the new screens on devices** as part of item 17: Today with Compose now, the briefing reader (sections,
    citations, share .md and .html), the topic workspace tabs, the wizard, run detail, one-time requests, predictions,
    suggestions, and editions, on an iPhone, an iPad (split views), an Android phone, and an Android tablet. The
    automated checks cover phone layouts in Jest and one iOS simulator pass; tablets and Android were not run.
23. **Decide what stays web-only for members.** The app leaves these to the web dashboard: feed and seed-page
    suggestions and the preview run in the wizard, the delivery step (recipients, template, model overrides), creating,
    duplicating, and deleting editions, choosing edition templates and voices, deleting and duplicating topics, cron,
    monthly, and every-N-weeks schedule editing, and retry from stage. Confirm, or ask for any of them in the app.

## Audio, feedback, insights, plans (Phases 8 to 10 and section 33.2)

24. **Check audio on devices.** On an iPhone and an Android phone, signed in to a server with the Kokoro TTS server
    running: Make audio on a briefing, wait for it, Listen, lock the screen (title, artwork, play and pause, back 15,
    forward 30 on the lock screen and in the Android media notification), switch apps for more than three minutes,
    seek from the chapter list, and check that the section being read is marked in the reader. Then Download, turn on
    airplane mode, and play it from Listen > Downloads. Jest covers the flow with a fake player; no device or
    simulator was run for this change.
25. **Podcast app handoff.** Create a feed in Account > Podcast feeds against the hosted HTTPS server and tap Open in
    Podcasts on iOS (`podcast://`) and Android (`pcast://`; Pocket Casts, AntennaPod, or Podcast Addict must be
    installed). Feeds on `http://127.0.0.1` or a LAN address show a warning, since podcast apps may fetch from their
    own servers. Decide whether Android should name a specific app instead.
26. **Promotional code links.** Decide where `https://app.mymorningnews.ai/redeem?code=...` links appear (email, the
    website, social posts). The app maps it, and `mymorningnews://redeem?code=...`, to Account > Redeem a code with the
    code filled in (the user still taps Redeem). Android has a `/redeem` intent filter; the
    `apple-app-site-association` file from item 11 (the server does not publish one yet) must list `/redeem*` too.
27. **Review the new screens** as part of item 17: Account > Plan and codes, Redeem a code, Email, Podcast feeds; the
    briefing reader's audio panel and inline ratings; Listen; Insights > Relevance, Accuracy, Feedback (with the
    history), and Learned preferences; and the rating chips and why sheet on findings, predictions, and suggestions.
28. **QA promotional codes stay disabled.** The live contract test (`src/__tests__/live.test.js`) creates a one-use
    `QA-MOBILE-...` code, redeems it as the demo user, then revokes the redemption and disables the code (codes with
    history cannot be deleted). Delete or ignore those rows in `/admin/promo-codes` on shared servers.

