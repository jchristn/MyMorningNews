# Platform tiers release status

Updated November 24, 2026. **Server, dashboard and web billing are live. The full native-store rollout is not complete.** The [implementation plan](PLATFORM_TIERS_AND_SHARED_BRIEFINGS_PLAN.md) ends with the UX review; the [operator guide](PLATFORM_OPERATIONS.md) documents configuration and remaining restrictions.

## Confirmed policy and deployed behavior

- Free/catalog-only: five admin-designated free-eligible system subscriptions, zero custom research, shared audio. Production captures this non-billed policy for new accounts.
- Administrator-selectable `complimentary-five`: any five system subscriptions, five custom topics, unlimited audio, no required payment.
- Paid 5: $10/month or $100/year, five custom topics plus system subscriptions, 30 generated audio minutes/day.
- Paid 20: $30/month or $300/year, 20 custom topics plus system subscriptions, 100 generated audio minutes/day.
- No trial. UTC daily generation windows; replay is free. BYOK and self-hosted quantities are unlimited. BYOK uses personal model credentials and personal speech or the available operator-hosted Kokoro engine.
- Per-user overrides have reasons/expiry; existing accounts retain captured policies. JSON signup policy and the switch enabling optional paid purchases are independent.
- Signup codes capture complimentary grants or paid discount/custom-package offers. Paid benefits require verified purchase; a code/checkout redirect cannot grant paid access.

## UX deployment, 2026-11-24 UTC

Server and dashboard run `mobile-ux-20261124-1` from `7415c6f`. [Tests](https://github.com/jchristn/MyMorningNews/actions/runs/45450916934), [Dashboard](https://github.com/jchristn/MyMorningNews/actions/runs/45450916974), and [Mobile](https://github.com/jchristn/MyMorningNews/actions/runs/45450916757) CI all passed, including the benchmark gate and SDK harnesses. Local checks passed 283 server, 655 dashboard, and 526 mobile tests (six existing mobile skips). The images include the direct support-report endpoint and admin plan navigation; the mobile source includes the login logo, Editions default, shorter label, and shared navigation history. Production health, login, bounded collection retrieval, support-route protection, and matching dashboard assets passed. Schema remains 38. Store distribution is recorded below.

## Production release

At 02:26 UTC, server/dashboard were replaced with `tiers-20261124-1`. Migrations 27 through 38 applied successfully; both containers and the public health endpoint are healthy. The final metadata-only server correction is deployed as `tiers-20261124-2`; its live OpenAPI callback schemas were verified. The MCP image is published; this EC2 stack did not run an MCP service before this release, and no new public MCP service was exposed.

Backup: `/home/ubuntu/mymorningnews/backups/tiers-20261124T022634Z`, including database, settings, Compose/environment files and pinned old images. A migration/seed rehearsal against a private local production backup passed twice without changing the five existing users or captured policies between runs.

A disposable production canary verified catalog-only policy, five system slots, zero custom topics, enforced refusal of custom creation, catalog/provider/usage reads, all four approved USD offers, live Stripe checkout creation without payment, unchanged access after checkout, and expiration of that checkout during account deletion. Canary accounts were removed. No live charge was made.

Stripe test/live products, prices, app-specific portal and webhook are provisioned. The live webhook is enabled. A real checkout-expiration callback received HTTP 200; Stripe reported no pending delivery for the corresponding events. This proves live callback transport/signature acceptance; paid/refund lifecycle behavior was validated in Stripe test mode rather than charging a live card. Sandbox payments are rejected in production.

The operator-hosted speech service answered its voices/models endpoints, and `tts.allowLocalForByok` is enabled. The marketing site's pricing, terms and privacy disclosures are updated and published. Existing private topics were not published into the catalog: an administrator must curate and publish the system-wide briefings before new readers have catalog content.

## Implemented coverage

- Atomic custom-topic admission, plan resolution/overrides, captured signup policy, catalog caps/suspension, durable billing accounts/checkouts/callback inbox, and immutable generated-audio reservations/ledger.
- Isolated canonical catalog topics; administrator research/schedule/publication controls; member subscriptions, safe issue projections, shared audio, private progress, aggregate issue lists, podcast feeds and durable email/in-app/push fanout. Retirement/retraction and delivery eligibility are enforced server-side.
- Topic history modes Off, PreviousIssue, RecentIssues, SinceDate and SpecificIssue. Each run freezes bounded owned briefing context used throughout research and composition; Off disables historical suppression.
- Encrypted write-only Chat/Embedding/Speech keys, validation invalidation on edits, explicit personal funding, private model accounting, repair onboarding, and no fallback to platform keys.
- Stripe signed/current-state lifecycle handling, invoice verification, refunds/disputes, account-bound checkout/portal, retry/recovery and deletion cancellation. Apple JWS/Server API and Google Publisher/OIDC verification with account binding, native replacement lineage and recovery.
- Dashboard/member mobile screens for catalog, history, usage, billing and private providers; dashboard administration for packages, user allowances, publications and signup offers. Mobile listeners verify before finishing transactions, restore pending purchases and reject wrong-account bindings.
- C#/JS/Python SDK resources, MCP member tools, regenerated REST/OpenAPI documentation, the generated Postman reference collection and a separate executable local release smoke collection.

## Verification evidence

| Check | Result and scope |
| --- | --- |
| .NET solution | Both supported frameworks built with zero warnings/errors |
| Backend regression | 282/282 automated cases and 283/283 xUnit cases passed on both .NET 8 and .NET 10; includes enumeration, concurrency, isolation, catalog delivery/feed and provider scenarios |
| Stripe real sandbox | Checkout/retry, real paid invoice, account portal, cancellation, pending-checkout expiration and refund access removal passed; test objects cleaned up |
| Native provider protocols | 15/15 synthetic cryptographic/HTTP tests passed; not a substitute for store/device purchases |
| Dashboard | 654/654 tests; lint and production build passed |
| Mobile | 516 passed, six existing skips; lint, native JS export and strict route parity passed; 37 implemented routes |
| SDKs | Live harnesses: C# 41 passed / 5 conditional skips, JavaScript 93 / 13, Python 81 / 15; enumeration wire contract tests passed; CI passed all three harnesses |
| Postman | 77 local HTTP requests and 77 assertions passed for the enumeration release |
| UX | 20 English phone/desktop/light/dark views plus 18 Arabic RTL 320/768/1024px views: zero reported accessibility violations and no document overflow. Eight marketing pricing/terms views also passed |

Internal visual fixes include navigation/badge contrast, page spacing, separate daily usage/subscription counts, catalog delivery controls, suspended unsubscribe access, mobile allowance labels and deletion disclosures. Automated audits are scoped evidence, not full accessibility certification. No native-device visual pass is claimed: the enabled computer-use surface could not select Simulator.

## Native distribution and external blockers

- UX mobile build 10 (approved 2026-11-24) uses version 0.1.0 from commit `79531e5`. All [Tests](https://github.com/jchristn/MyMorningNews/actions/runs/45470862263), [Dashboard](https://github.com/jchristn/MyMorningNews/actions/runs/45470862243), and [Mobile](https://github.com/jchristn/MyMorningNews/actions/runs/45470862452) CI workflows passed. Local mobile verification and strict parity passed. iOS [EAS build](https://expo.dev/accounts/joel-christner/projects/mymorningnews/builds/1c5484de-5187-42d0-abe3-1ece274706af) uploaded through submission `7a70cfaf-a3d9-4b6e-892a-97d8eb765965`; Apple build `a2b89826-3dbe-4f64-974f-8f282c9c1fc7` is VALID, IN_BETA_TESTING, and attached to draft version 0.1.0. Android [EAS build](https://expo.dev/accounts/joel-christner/projects/mymorningnews/builds/8852fee3-c0ea-4374-977e-aaceb5a7993c) uploaded through submission `0c23ceca-6679-426e-b287-c016eb17dffc`; Google Play internal testing is verified as version code 10 with status `completed`. Test the login hold/fade, Editions filter, cross-tab Back, and support reporting on installed devices using `docs/MOBILE_DEVICE_TESTING.md`.

- Enumeration release (2026-11-24 UTC): all application collection retrievals use `EnumerationQuery`/`EnumerationResult`; 61 GET collections have POST `/enumerate` counterparts. Source commit `14a8ddd` passed every [CI job](https://github.com/jchristn/MyMorningNews/actions/runs/45355708300). Server, dashboard, and MCP images are published as `enumeration-20261124-1` for amd64/arm64. Server and dashboard were deployed and healthy for that release; production paging checks passed. MCP remains disabled on the host. The protocol exception for Android asset links is preserved; production still lacks its certificate-fingerprint configuration.
- iOS build 9 ([EAS build](https://expo.dev/accounts/joel-christner/projects/mymorningnews/builds/83ba9416-2f28-40ef-8653-95b5ec8dce8b)) uploaded through submission `366bcd80-8ba4-4a06-91e0-98fc0b7e99bc`. App Store Connect build `1b19a0d5-0ceb-4ac8-8f28-e3fe395b3808` was VALID and IN_BETA_TESTING, and was attached to the unsubmitted 0.1.0 version before build 10 replaced it. Public App Review remains pending the listing, privacy/declarations, approved screenshots, and native purchase/device validation.
- Android build 9 ([EAS build `40271b7f-6e57-4ae5-ab44-d03966924b15`](https://expo.dev/accounts/joel-christner/projects/mymorningnews/builds/40271b7f-6e57-4ae5-ab44-d03966924b15)) finished and uploaded through submission `67da9d19-31e8-4f70-a679-750e6c285a0a`. Google Play internal testing was released with version code 9 and status `completed`, verified through the Play API before build 10 superseded it. Production, beta, and alpha tracks have no releases. Keep the matching ignored Firebase `google-services.json` locally so local and EAS native fingerprints agree.
- Four Apple subscription products and four Google base plans are prepared with exact US prices. Apple products need remaining review metadata and first-version submission; Google plans remain draft until native purchase validation and notification setup are complete.
- Apple V2 production notification URL is configured. The configured keys authenticated against the sandbox API (an unknown transaction returned a structured error), but production Server API probes returned HTTP 401. Resolve/verify production access before native purchases launch; no successful real Apple callback or store purchase is claimed.
- Google Play API access works. Pub/Sub setup is blocked: the available Play service account returned 403, and local `gcloud` requires reauthentication as `joel@latticetech.ai`. The user was asked to renew that login. RTDN must then be configured in Cloud and Play Console and tested.
- Signed EAS Update publication was rejected again on 2026-11-24 UTC because the owning Expo account `joel-christner` is on Starter; code signing requires Production/Enterprise. No account upgrade was made and signing was not disabled. Fresh native builds are the release path for the pagination changes.
- Native purchase, restore, refund, account-switch and tablet/accessibility journeys still require device/store validation. Arbitrary signup-discount purchases on native stores remain blocked when the client cannot honor the captured offer; web discounts are implemented. Server-side native benefit binding requires a matching verified offer identifier.

## Additional planned design work

The plan includes edition-specific comparison, structured claim/evidence delta provenance and baseline-preview reader UI, system-issue inputs to custom editions, and a broader downgrade retention-selection flow. These are not shipped by the topic-history/catalog release and must not be represented as completed. A complete release against every item in the full plan also needs the remaining native workflows and full signed-device UX review above.

(c) 2026 Lattice AI LLC
