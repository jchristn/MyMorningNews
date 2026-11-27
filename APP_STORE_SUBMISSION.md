# App Store and Google Play submission form

Release update (2026-11-25): approved build 11 adds the stale topic keep-or-delete banner, a one-tap notification card, and the TopicStale notification preference. iOS build 11 is VALID, in internal TestFlight testing, and attached to draft version 0.1.0; Android build 11 is released to Google Play internal testing. Push credentials are now configured for both platforms (APNs and FCM V1).

Release update (2026-11-24): approved build 10 includes the login logo, recent-briefings label, Editions default, Back navigation to the originating screen, and direct support reporting. iOS build 10 is VALID and in internal TestFlight testing, attached to draft version 0.1.0. Android build 10 is released to Google Play internal testing. Evidence is in [PLATFORM_IMPLEMENTATION_STATUS.md](docs/PLATFORM_IMPLEMENTATION_STATUS.md). Server/web billing is live; products, device purchase testing, store review metadata, and privacy declarations must be verified before a public native release. Older checked answers below are historical where superseded by this release.

Every field and data point needed to submit the My Morning News app (`mobile/`) to the Apple App Store and Google
Play, written as a form. For each field: the store, the field name as the console shows it, the limit or format,
our proposed value, and an `[ ] Owner:` line wherever only the owner can supply or confirm the value. Proposed
values are drafts: **every proposed text needs the owner's approval** before submission (item 0.6).

Related: `mobile/OWNER_ACTIONS.md` (ordered owner tasks), `mobile/README.md` (builds and environment variables),
plan sections 33.1 (sign-in, account deletion) and 35 (mobile app).

Rules for this file: no passwords, keys, or tokens here (they go into the consoles or EAS). Personal and account
fields (contact names and phones, developer address, account IDs, the reviewer password) live in the git-ignored
`mobile/store-private.local.md`. Values the app reads from code are marked with where they live, so a change is made
in one place.

**Owner decisions recorded 2026-11-20 (plan Q24 to Q28):** Android first through Google Play internal testing (the
Lattice AI Play account is ready); iOS follows when Apple approves the business enrollment. Bundle ID
`ai.latticetech.mymorningnews`. Countries: United States, Canada, United Kingdom, Australia, New Zealand. Ages 18 and
over. Category Productivity on both stores. Free download with optional auto-renewing subscriptions. Drafted listing text and answers approved, to
be re-checked against the release build at submission.

**Legend.** Store: **A** Apple App Store Connect, **G** Google Play Console, **A+G** both.

---

## 0. Gates before the first public submission

| # | Gate | Status |
|---|------|--------|
| 0.1 | **Minimum functionality (Apple 4.2, Google "Minimum functionality").** Every tab works against the server (briefings, topics, editions, audio, Insights, system briefings, account deletion). | `[x]` Met. Owner decision 2026-11-26: public submission waits for native purchases (gate 0.8); it does not wait for curated system briefings, and an empty catalog at launch is acceptable |
| 0.2 | **In-app account creation and deletion (Apple 5.1.1(v), Google account deletion policy).** The app creates accounts (sign-in > Create account, `POST /v1.0/public/signup`), so it must also delete them in the app: Account > Delete account calls `POST /v1.0/api/account/delete`, which the server ships. The web page stays as the second path (section 9.2). | `[x]` Both in the app (UT-019, 2026-11-20); check on the release build |
| 0.3 | **Hosted service live** at `https://app.mymorningnews.ai`, with the reviewer demo account (section 9). | `[x]` Live since 2026-11-24 (health checked at every release); refresh the demo account's briefings just before submitting |
| 0.4 | **Legal pages reviewed and live**: privacy, terms, support, account deletion (section 1). | `[x]` Wording approved 2026-11-20 (support `hello@latticetech.ai`; terms set to 18 and over) |
| 0.5 | **Interface review done**, then screenshots captured (section 8). No screenshots before the review. | `[ ] Owner: review the private review page` |
| 0.6 | **Every proposed text and answer in this file approved.** | `[x]` Approved 2026-11-20; re-check at submission |
| 0.7 | **`npm run parity:strict` passes** (nothing planned remains for the routes the release claims). | Passes 2026-11-26 (38 implemented, 0 planned, 23 not applicable); run again before submitting |
| 0.8 | **Native purchases validated.** Apple production Server API access (production probes return 401 today), Google Real-time Developer Notifications through Pub/Sub, and signed-device purchase, restore, refund, and wrong-account tests (see docs/PLATFORM_IMPLEMENTATION_STATUS.md). | `[ ]` Owner decision 2026-11-26: required before the first public submission |

---

## 1. App identity and URLs

| Store | Field | Format and limits | Proposed value | Owner |
|---|---|---|---|---|
| A+G | App name | 30 characters max on both | `My Morning News` (15). From `mobile/app.config.js` `name`. | `[x]` Approved; check the name is free when creating each app record |
| A | Subtitle | 30 characters | `Your research, briefed daily` (28) | `[x]` Approved |
| A | Bundle ID | Reverse DNS, immutable after the first build | `ai.latticetech.mymorningnews` (`MYMORNINGNEWS_MOBILE_BUNDLE_ID` overrides) | `[x]` Decided; register in the Apple Developer portal after enrollment |
| G | Package name | Immutable after the first upload | `ai.latticetech.mymorningnews` | `[x]` Decided |
| A | SKU | Internal, unique in the account | `mymorningnews-ios` | `[x]` Approved |
| A | Primary language | | English (U.S.) | |
| G | Default language | | English (United States), en-US | |
| A | Apple ID (app record) | Numeric, issued by App Store Connect | `6837840760` | `[x]` In `eas.json` submit `ascAppId` |
| A | Team ID | 10 characters | `U34NDPAH3K` | `[x]` In `eas.json` `appleTeamId` and the universal link file |
| A | Apple ID email used to submit | | | `[x]` Not stored in `eas.json`; EAS submit uses the App Store Connect credentials saved in the EAS account |
| G | Developer account | Organization account | Lattice AI LLC | `[x]` Ready; account ID in `mobile/store-private.local.md` |
| A+G | D-U-N-S number | Organization enrollment | On file for Lattice AI LLC | `[x]` Apple organization enrollment complete (TestFlight builds 9 to 11 delivered) |
| A+G | Version | Numbers only on the stores | `0.1.0` (`app.config.js` `version`); release label `0.1.0-alpha` in `extra.releaseVersion` | No change without owner approval |
| A | Build number | String | `BUILD_NUMBER` in `mobile/app.config.js` (11 is the latest delivered) | Each new build number needs owner approval |
| G | Version code | Integer | Same `BUILD_NUMBER` | Same |
| A | Copyright | Year and holder | `2026 Lattice AI LLC` | `[x]` Approved |
| A+G | Support URL | Public page with contact details | `https://mymorningnews.ai/support.html` | |
| A | Marketing URL | Optional | `https://mymorningnews.ai` | |
| A+G | Privacy policy URL | Required | `https://mymorningnews.ai/privacy.html` | |
| A | User privacy choices URL | Optional | `https://mymorningnews.ai/privacy.html` | |
| A+G | Terms of use | Apple: standard EULA or a custom one; link in the description | `https://mymorningnews.ai/terms.html` (keep Apple's standard EULA and link our terms in the description) | `[x]` Approved |
| G | Account deletion URL | Required; must work without the app and name the app and developer | `https://mymorningnews.ai/delete-account.html` | `[x]` Checked: the page names My Morning News and Lattice AI LLC |
| A+G | Contact email | Shown to users on Google Play; App Review contact on Apple | `hello@latticetech.ai` | |
| G | Contact phone | Optional | Same as the Play account holder | `[x]` In `mobile/store-private.local.md` |
| G | Website | | `https://mymorningnews.ai` | |
| G | Developer address | Public for organizations | The address registered for Lattice AI LLC in Play Console | `[x]` Use the console address |
| A | App Review contact | First name, last name, phone, email | Email `hello@latticetech.ai`; name and phone of the account holder | `[x]` Same as the account holder |

---

## 2. Store copy

All text in this section is proposed. No em-dashes, no claims the app does not support today, nothing about prices.

### 2.1 Apple promotional text (170 characters, editable without a new build)

> My Morning News researches the topics you care about on your schedule and briefs you with ranked findings, sources for every claim, and audio you can play anywhere.

(164 characters) `[x]` Approved

### 2.2 Google short description (80 characters)

> Tell it what you care about. Get a ranked, sourced briefing to read or hear.

(76 characters) `[x]` Approved

### 2.3 Full description (Apple 4000, Google 4000 characters)

> My Morning News is a research assistant for the things you want to stay on top of. Tell it what you care about,
> answer a few questions, and it researches the web on your schedule. Each run ranks what it found by how relevant
> it is to you, backs every claim with a quote and a link, and delivers a briefing you can read or listen to.
>
> In the app:
> - Today: your latest briefing, the findings behind it, and predictions that are due to grade.
> - Listen: every briefing as audio, with chapters, speed from 0.75x to 2x, a sleep timer, downloads for offline
>   listening, and controls on the lock screen.
> - Topics: the things you follow, and when each one runs.
> - Insights: whether the ranking is finding what matters to you, and what your research costs.
> - Feedback everywhere: mark anything relevant or not, and say why. The next run learns from it.
>
> Your account works on the web, by email, and in your podcast app too. Use the hosted service, or connect the app
> to a My Morning News server you run yourself.
>
> Subscribe to up to five selected system briefings for free. Optional paid plans add custom topics and daily generated audio. No trial. Sign in with email and password. Terms of use:
> https://mymorningnews.ai/terms.html. Privacy policy: https://mymorningnews.ai/privacy.html.

`[x]` Approved. Re-check each bullet against what the release build does (section 0.1); remove any line whose feature has not shipped.

### 2.4 Apple keywords (100 characters, comma separated, no spaces needed)

`news,briefing,research,podcast,daily,digest,summary,topics,curated,morning,audio,ai,predictions` (95)
`[x]` Approved

### 2.5 What's new (Apple 4000; Google release notes 500 per language)

First release: not shown on Apple for version 1. Google internal testing notes:
> First test build of My Morning News for iPhone, iPad, and Android.

`[x]` Approved

### 2.6 Categories and tags

| Store | Field | Proposed value | Owner |
|---|---|---|---|
| A | Primary category | Productivity | `[x]` Decided 2026-11-20 |
| A | Secondary category | News | `[x]` Decided |
| G | App category | Productivity (no Play news app declaration) | `[x]` Decided |
| G | Tags (up to 5) | Productivity, News, Podcasts, Research, Personalization (pick the closest from Play's list) | `[x]` Approved |

---

## 3. Pricing, availability, and distribution

| Store | Field | Proposed value | Owner |
|---|---|---|---|
| A+G | Price | Free download. Optional native subscriptions: 5 custom topics with 30 generated audio minutes/day, or 20 with 100 minutes/day. Monthly and annual offers; no trial. Exact localized prices come from the store. | `[x]` Confirmed |
| A+G | Countries and regions | United States, Canada, United Kingdom, Australia, New Zealand | `[x]` Decided |
| A | Pre-order | No | |
| A | Distribution | Public on the App Store | `[x]` Confirmed |
| A | iPhone and iPad apps on Apple silicon Macs | On | `[x]` Decided; test on a Mac before the first public release |
| A | Apple Vision Pro availability | Off | `[x]` Decided |
| A | Phased release for automatic updates | On (7 days) | `[x]` Confirmed |
| A | Version release | Manual release after approval | `[x]` Confirmed |
| G | Countries | Same list as Apple | `[x]` Confirmed |
| G | Contains ads | No | |
| A+G | Paid Applications Agreement, tax, banking | Required before subscription activation; verify agreement, tax and banking status in both stores | `[ ]` Owner: verify in both consoles before native subscription launch (gate 0.8) |
| A | Content rights: "Does your app contain, show, or access third-party content?" | Yes. Briefings quote short excerpts from public web pages with a link to each source, as summaries for the user's own research. | `[x]` Approved 2026-11-20 (short quoted excerpts with links, for the user's own research) |

### 3.1 Signup discounts and web checkout (store rules, checked 2026-11-26)

A captured signup discount is honored only by web (Stripe) checkout. The app never offers the regular store price in
its place: the native Subscribe button is hidden for that offer. What the app shows instead depends on the storefront
that StoreKit or Google Play Billing reports (`getStorefront`); an unknown storefront gets the most restrictive behavior.
The logic is `webCheckoutPolicy` in `mobile/src/billing/purchases.js`.

| Store and storefront | App behavior | Basis |
|---|---|---|
| App Store, United States | Explanation plus a "Subscribe on the web" button that creates the account's checkout on the server and opens it in the system browser | [App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/) 3.1.1(a) and 3.1.3: entitlements "are not required for developers to include buttons, external links, or other calls to action in their United States storefront apps" (Epic v. Apple injunction of April 30, 2025, contempt affirmed by the Ninth Circuit on December 11, 2025; Apple's Supreme Court appeal, No. 25-1311, is pending, so recheck before each submission) |
| App Store, any other storefront | Neutral notice that the discount does not apply to store offers; no link, no mention of the web | 3.1.1(a): outside the US, links and calls to action need a StoreKit External Purchase Link entitlement, which this app does not hold |
| Google Play, United States | Text saying the discount is available when subscribing on the web; no link or button | [US policy update](https://support.google.com/googleplay/android-developer/answer/15582165): Google does not prohibit telling US users about pricing outside Play. Links require enrolling in the [external content links program](https://support.google.com/googleplay/android-developer/answer/16470497) and integrating its APIs, which this app has not done |
| Google Play, any other country | Same neutral notice as above | [Payments policy](https://support.google.com/googleplay/android-developer/answer/9858738) section 4 bars messaging that leads users to other payment methods without an enrolled program |

Enrolling later in Google's external content links program (or holding an Apple entitlement in another storefront)
would allow the link there; change `webCheckoutPolicy` and this table together.

---

## 4. Export compliance (Apple)

| Question | Answer |
|---|---|
| Does your app use encryption? | Only exempt encryption: HTTPS (TLS) through the operating system, and the Keychain or Keystore for tokens. |
| `ITSAppUsesNonExemptEncryption` | `false`, set by `ios.config.usesNonExemptEncryption: false` in `app.config.js`, so App Store Connect does not ask on each build. |
| Proprietary or non-standard encryption | None |
| French encryption declaration | Not needed |

`[x]` Confirmed 2026-11-20

---

## 5. Age rating

The app shows research briefings about whatever topics the user chooses, drawn from public news and web sources.
There is no user-to-user content, no chat between people, no ads, no gambling, and optional subscriptions.

### 5.1 Apple (questionnaire with tiers 4+, 9+, 13+, 16+, 18+)

| Question | Proposed answer | Why |
|---|---|---|
| Cartoon or fantasy violence | None | |
| Realistic violence | Infrequent/Mild | News about conflict or crime can appear in briefings |
| Prolonged graphic or sadistic realistic violence | None | |
| Profanity or crude humor | Infrequent/Mild | Quoted source text is not filtered |
| Mature or suggestive themes | Infrequent/Mild | News topics |
| Horror or fear themes | None | |
| Medical or treatment information | Infrequent/Mild | Health news can appear; the app gives no medical advice |
| Alcohol, tobacco, or drug use or references | Infrequent/Mild | News topics |
| Sexual content or nudity | None | |
| Graphic sexual content and nudity | None | |
| Simulated gambling | None | |
| Contests | None | |
| Gambling (real money) | No | |
| Unrestricted web access | No | Source links open one page in the in-app browser; the app is not a general browser |
| User-generated content | No | Topics and feedback are private to the account |
| Messaging and chat | No | |
| Advertising | No | |
| Parental controls or age assurance | No | |
| Made for Kids | No | |
| Expected result | 13+ | |

`[x]` Approved 2026-11-20; if App Store Connect proposes a lower tier, keep the higher one

### 5.2 Google Play (IARC questionnaire, category "Reference, News, or Educational")

| Area | Proposed answer |
|---|---|
| Violence | References to real-world violence in news content, not graphic |
| Sexuality | No |
| Language | Occasional mild language in quoted sources |
| Controlled substances | References in news content |
| Gambling | No |
| Users can interact or exchange content | No |
| Shares personal information with other users | No |
| Shares current location with others | No |
| Digital purchases | Yes, auto-renewing subscriptions |
| Unrestricted internet | No |

`[x]` Approved

### 5.3 Google Play target audience and declarations

| Declaration | Proposed answer | Owner |
|---|---|---|
| Target age group | 18 and over (matches the terms of service) | `[x]` Decided 2026-11-20 |
| Appeals to children | No | |
| Ads | No ads | |
| News app | No: listed as Productivity, a research assistant for the user's own topics. Sources are still named and linked on every item. If Play review classifies it as news, switch to Yes (the app already meets the news requirements). | `[x]` Decided 2026-11-20 |
| Government app | No | |
| Financial features | None | |
| Health apps | None | |
| App access | Sign-in required; credentials in section 9 | |
| Advertising ID | Not used. Confirm after the first Play build that the merged manifest has no `com.google.android.gms.permission.AD_ID`; if a library adds it, remove it with `android.blockedPermissions`. | Check on first build |
| Foreground service permissions (Android 14 and later) | Declare `FOREGROUND_SERVICE_MEDIA_PLAYBACK`: "Plays briefing audio in the background with media controls while the user listens with the screen off or in another app." Play asks for a short video of the feature. | `[ ] Owner: record the video (play an episode, lock the phone, use the media notification)` |
| Notification permission | Asked only from Account > Notifications; no declaration needed | |
| Photo, video, location, contacts, calendar, microphone | Not used (the audio plugin's record permission is blocked in `app.config.js`) | |

---

## 6. Apple App Privacy (nutrition label)

**Tracking: No.** No advertising or analytics SDK, no data shared with data brokers, no App Tracking Transparency
prompt. For each type below: **Linked to the user: Yes. Used for tracking: No. Purpose: App Functionality** (and
Product Personalization where noted).

| Apple data type | Collected | What it is in this app |
|---|---|---|
| Contact Info > Email Address | Yes | Account email: sign-in, verification, password reset, briefing emails |
| Contact Info > Name | Yes (optional) | The name given at sign-up, and first and last name on the profile |
| Contact Info > Phone, Physical Address, Other | No | |
| User Content > Other User Content | Yes | Topic descriptions, refinement answers, feedback and the reasons given (also Product Personalization: ranking learns from it) |
| User Content > Audio, Photos or Videos, Emails or Text Messages, Gameplay, Customer Support | No | |
| Identifiers > User ID | Yes | Account and tenant IDs |
| Identifiers > Device ID | Yes | Expo push token for this device, with the device name and platform, only after the user turns notifications on |
| Usage Data > Product Interaction | Yes | Listening progress, feedback, model usage per account (cost reporting), request history for security (also Product Personalization) |
| Usage Data > Advertising Data, Other Usage Data | No | |
| Diagnostics | No | No crash reporting SDK; change if one is added |
| Purchases > Purchase History | Yes | Account-linked subscription identifiers and status, used for app functionality; no advertising or tracking |
| Location, Health and Fitness, Financial Info, Contacts, Browsing History, Search History, Sensitive Info, Other Data | No | |

Notes: in-app sign-up collects the email, a password (the server keeps only a hash), an optional name, and an
optional promotional code, sends the device time zone and language as settings, and records that the terms were
accepted and the minimum age confirmed (a yes, not a birth date); none of these is a new data type. Time zone and
language preferences are settings, not location. Topic text and fetched source text are sent
to the AI model provider the server uses (Anthropic by default) to do the research; that provider processes it on the
operator's behalf and does not use it for its own purposes.

`[x]` Approved 2026-11-20; re-check when crash reporting, purchases, or new data types are added

Privacy manifest: Expo SDK 58 merges the required-reason API declarations of its modules into `PrivacyInfo.xcprivacy`
at build time. After the first EAS production build, check the generated manifest and add `ios.privacyManifests` to
`app.config.js` if App Store Connect reports a missing reason.

---

## 7. Google Play Data safety

| Question | Proposed answer |
|---|---|
| Does your app collect or share any of the required user data types? | Yes |
| Is all user data collected by your app encrypted in transit? | Yes (the hosted service is HTTPS only; self-hosted servers on plain HTTP are the user's own choice and the app warns about them) |
| Do you provide a way for users to request that their data be deleted? | Yes: in the app (Account > Delete account) and on the web (`https://mymorningnews.ai/delete-account.html`) |
| Data shared with third parties | None. Expo and Firebase (push delivery), Apple, Google, and the AI model provider act as service providers, which Google does not count as sharing. |

| Category > type | Collected | Required or optional | Purposes |
|---|---|---|---|
| Personal info > Email address | Yes | Required | App functionality, Account management, Developer communications |
| Personal info > Name | Yes | Optional | App functionality, Account management (sign-up and profile) |
| Personal info > User IDs | Yes | Required | App functionality, Account management |
| Personal info > Address, Phone, Race, Political or religious beliefs, Sexual orientation, Other | No | | |
| App activity > App interactions | Yes | Required | App functionality, Personalization, Fraud prevention and security (request history) |
| App activity > Other user-generated content | Yes | Required | App functionality, Personalization (topics, feedback reasons) |
| App activity > In-app search history, Installed apps, Other actions | No | | |
| Device or other IDs | Yes | Optional (only with notifications on) | App functionality (push token) |
| App info and performance > Crash logs, Diagnostics | No | | |
| Location, Financial info, Health and fitness, Messages, Photos and videos, Audio, Files and docs, Calendar, Contacts, Web browsing | No | | |

`[x]` Approved 2026-11-20

---

## 8. Screenshots and previews

**Captured only after the owner's interface review (plan section 34.1).** Use a release build against the hosted
service with the demo account and clearly fictional data, light theme first (dark as an optional second set), clean
status bars (iOS: `xcrun simctl status_bar booted override --time 9:41 --batteryState charged --batteryLevel 100
--cellularBars 4`; Android: system UI demo mode). Never commit screenshots before approval; QA captures stay in the
git-ignored `mobile/qa/`. Approved images go to `assets/screenshots/mobile/<device>/<name>-<theme>.png`.

| Store | Device class | Required size (portrait) | Simulator or emulator | Count | Owner |
|---|---|---|---|---|---|
| A | iPhone 6.9-inch (required for iPhone) | 1320 x 2868 (or 1290 x 2796) | iPhone 18 Pro Max | 3 to 10 | `[ ] Owner: approve set` |
| A | iPhone 6.5-inch (only if App Store Connect asks) | 1284 x 2778 or 1242 x 2688 | iPhone 11 Pro Max class | 3 to 10 | |
| A | iPad 13-inch (required: the app supports iPad) | 2064 x 2752 (or 2048 x 2732) | iPad Pro 13-inch (M5) | 3 to 10 | `[ ] Owner: approve set` |
| A | App preview video (optional) | 15 to 30 seconds, H.264 or ProRes, per device class (for example 886 x 1920 on 6.9-inch) | Same devices | 0 to 3 | `[ ] Owner: decide` |
| G | Phone | 9:16, each side 320 to 3840 px; at least 1080 px for promotion | Pixel 9 class emulator | 2 to 8 (4 recommended) | `[ ] Owner: approve set` |
| G | 7-inch tablet | Each side 320 to 3840 px | Tablet emulator in a 7-inch profile | up to 8 | |
| G | 10-inch tablet | Each side 1080 to 7680 px | Pixel Tablet emulator | up to 8 (4 recommended) | `[ ] Owner: approve set` |
| G | Feature graphic | 1024 x 500, JPEG or 24-bit PNG without alpha | Designed from `assets/logo.png` | 1 | `[ ] Owner: approve` |
| G | App icon | 512 x 512, 32-bit PNG, up to 1 MB | From `mobile/assets/images/icon.png` (generated from `assets/glyph.png`) | 1 | `[ ] Owner: approve` |
| G | Promo video | YouTube URL, optional | | 0 or 1 | `[ ] Owner: decide` |

Screens, in order (the first three show in search results):

| # | Screen | Notes |
|---|---|---|
| 1 | Today with the latest briefing | Hero |
| 2 | Briefing reader with sources | iPad: briefings list beside the reader |
| 3 | Player with chapters and speed | Also the lock screen with the media controls |
| 4 | Topics and the refinement wizard | After Phase 4 |
| 5 | Insights: relevance and usage charts | |
| 6 | Account with notifications and delete account | Shows account deletion to reviewers |

---

## 9. Review notes and demo account

Create the demo account on the hosted service before submitting: a verified user with at least one topic, a few
briefings with audio, findings, and predictions. Put the credentials only into App Store Connect (App Review
Information > Sign-in required) and Play Console (App content > App access).

`[x]` Demo email `demo@mymorningnews.ai` (password in `mobile/store-private.local.md`, entered only in the consoles)

Notes template (App Store Connect "Notes" and Play Console "App access" instructions):

```text
My Morning News is a research assistant. Users describe topics they want to follow; the service researches them
on a schedule and delivers ranked briefings with sources, readable in the app and playable as audio.

Sign in with the demo account below. The app connects to https://app.mymorningnews.ai by default; leave the server
unchanged. Email: [demo email]  Password: [entered in this form only]

Where to look:
- Today: the latest briefing; tap it to read, tap Listen to play the audio.
- Listen: the player. Audio continues with the screen locked, with play, pause, back 15 seconds, and forward 30
  seconds on the lock screen (background audio is the reason for the audio background mode).
- Account > Notifications: notifications are optional and the permission is asked only there.
- Account > Delete account: deletes the account (type DELETE to confirm). It can also be requested on the web at
  https://mymorningnews.ai/delete-account.html.
- Create account (on the sign-in screen): in-app sign-up with email and password, the terms and privacy policy, and
  an 18 and over confirmation. The confirmation email's link opens the app. Reviewers can use the demo account
  instead. "Forgot password?" sends a reset link that also opens the app.

Sign-in uses email and password only, with no third-party or social login, so Sign in with Apple is not required
(Guideline 4.8). The app has no ads, no tracking, and optional subscriptions. "Change server" on the sign-in screen is for
people who run their own open-source My Morning News server; reviewers do not need it.
Contact: hello@latticetech.ai
```

`[x]` Approved 2026-11-20. `[ ] Owner: approve the "Create account" lines added for UT-019`

Before each submission: sign in with the demo account on a release build against production and make sure it has
fresh briefings with audio.

### 9.1 Sign-in and Guideline 4.8

- Email and password only (plus "Change server" for self-hosted installs). No Google, Facebook, or other third-party
  login, so Sign in with Apple is not required. If a third-party login is ever added, Sign in with Apple is added
  with it (plan section 35.3).
- In-app sign-up (`POST /v1.0/public/signup`): email, password (8 or more characters), an optional name and
  promotional code, links to the terms and privacy policy, and an age box using client config `minimumAge` (18 on
  the hosted service). "Create account" appears only when client config has `signupEnabled`. The verification and
  reset emails link to `https://app.mymorningnews.ai/verify` and `/reset-password`; universal links open the app's
  own screens for them, and sign-in offers "Send a new link" when an account is not confirmed yet.
- Cloudflare Turnstile: the app has no WebView, so when client config carries a `turnstileSiteKey` the sign-up
  screen sends people to the web sign-up page and back to sign in. With Turnstile on, account creation happens on the
  web, and in-app deletion is still required (Apple 5.1.1(v) applies to accounts created anywhere).

### 9.2 Account deletion

| Requirement | Where |
|---|---|
| In-app path (Apple 5.1.1(v), Google) | Account > Delete account. The user types `DELETE`; the app calls `POST /v1.0/api/account/delete`, then signs out. |
| Web path (Google requirement) | `https://mymorningnews.ai/delete-account.html` |
| What happens | Every session is signed out; the personal tenant is purged after a 7-day grace period (`Hosting.DeletionGraceDays`); a confirmation email is sent; signing in during the grace period cancels it (plan section 33.1). |
| Partial deletion (Google) | Users can remove devices (Account > Notifications), downloads (Listen > Downloads), and topics; data export is Account > Export data. |

---

## 10. Build and submit

Run everything from `mobile/`. One-time setup is in `mobile/OWNER_ACTIONS.md` (EAS project, APNs key, Firebase,
Play service account, update signing certificate).

### 10.1 Environment (EAS environments development, preview, production)

```bash
npx eas-cli@latest env:create --environment production --name MYMORNINGNEWS_MOBILE_EAS_PROJECT_ID --value <project id> --visibility plaintext
npx eas-cli@latest env:create --environment production --name GOOGLE_SERVICES_JSON --type file --value ./google-services.json --visibility secret
# Repeat for preview and development. Credentials (distribution certificate, provisioning profile, APNs key,
# Android upload keystore, FCM V1 key, Play service account) live in `npx eas-cli@latest credentials`.
```

### 10.2 Checks before a store build

```bash
npm ci
npm run verify          # lint, i18n, parity, audit, Jest, expo export
npm run parity:strict   # nothing planned for the routes this release claims
```

### 10.3 Build

```bash
npx eas-cli@latest build --profile preview --platform all      # TestFlight and Play internal testing
npx eas-cli@latest build --profile production --platform all   # store builds
```

`appVersionSource` is `local`: the version (`0.1.0`), build number, and version code come from `app.config.js` and
are raised only with the owner's approval.

### 10.4 Submit

```bash
npx eas-cli@latest submit --platform ios --profile preview --latest       # TestFlight
npx eas-cli@latest submit --platform android --profile preview --latest   # Play internal testing (draft)
npx eas-cli@latest submit --platform ios --profile production --latest
npx eas-cli@latest submit --platform android --profile production --latest
```

The first Android upload must be done by hand in Play Console (Testing > Internal testing > Create release) before
`eas submit` can upload. Fill the `REPLACE_WITH_...` values in `eas.json` first.

### 10.5 TestFlight and internal testing checklist

- [ ] Sign in against the hosted service, change server to a self-hosted one and back, sign out.
- [ ] Create account in the app with a new address, open the verification email on the device (the link opens the
      app), confirm, and sign in. Sign in before confirming and use "Send a new link". "Forgot password?", open the
      reset email on the device, choose a new password, and sign in with it.
- [ ] Today, briefing reader, Listen; audio with the screen locked; lock-screen and notification controls (play,
      pause, back 15, forward 30); speed; chapters; sleep timer; downloads offline.
- [ ] Notifications: turn on, receive the test notification, tap it and land on the right screen; Android channels
      (briefings, predictions, account) appear in system settings.
- [ ] Universal link from a briefing email opens the app.
- [ ] Account: profile, password, usage, language (including Arabic for right to left), appearance, app lock, about,
      export, delete.
- [ ] iPad and Android tablet: split views and the persistent mini player, both orientations.
- [ ] VoiceOver and TalkBack labels; largest text size.

### 10.6 Over-the-air updates after release

JavaScript-only fixes ship with `npm run publish-update -- --channel production --message "..." --publish`, signed
with the update key (`mobile/updates/README.md`). Native changes (new modules, permissions, config plugins) always
need a new build and review.
