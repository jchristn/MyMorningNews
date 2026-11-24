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
- C#/JS/Python SDK resources, MCP member tools, regenerated REST/OpenAPI documentation, 350-request Postman reference collection and a separate executable local release smoke collection.

## Verification evidence

| Check | Result and scope |
| --- | --- |
| .NET solution | Both supported frameworks built with zero warnings/errors |
| Backend regression | 281/281 passed, including concurrency, isolation, catalog delivery/feed and provider scenarios |
| Stripe real sandbox | Checkout/retry, real paid invoice, account portal, cancellation, pending-checkout expiration and refund access removal passed; test objects cleaned up |
| Native provider protocols | 15/15 synthetic cryptographic/HTTP tests passed; not a substitute for store/device purchases |
| Dashboard | 652/652 tests; lint, production build and all 12 locale keys passed |
| Mobile | 514 passed, six existing skips; lint, native JS export and strict route parity passed; 37 implemented routes, 23 web-only/not-applicable mappings |
| SDKs | C# live harness 40 passed, six research-dependent skips; JS/Python wire tests and live platform reads passed |
| Postman | Ten local HTTP release requests, six assertions passed; dependency simulations exercised ambiguous checkout, outages and account isolation |
| UX | 20 English phone/desktop/light/dark views plus 18 Arabic RTL 320/768/1024px views: zero reported accessibility violations and no document overflow. Eight marketing pricing/terms views also passed |

Internal visual fixes include navigation/badge contrast, page spacing, separate daily usage/subscription counts, catalog delivery controls, suspended unsubscribe access, mobile allowance labels and deletion disclosures. Automated audits are scoped evidence, not full accessibility certification. No native-device visual pass is claimed: the enabled computer-use surface could not select Simulator.

## Native distribution and external blockers

- iOS build 7 (`49391176-362f-4787-88e7-e4c157a0bace`) finished, uploaded through EAS submission `b2c7d4cc-abfb-4615-b742-0a8a14c1ec0b`, and processed as VALID in App Store Connect (`8c7138bb-c409-4ef9-8a40-63f0fc25a2b6`). It is attached to the unsubmitted 0.1.0 version. This is not App Review approval or public availability.
- Android build 7 (`fd210823-36a4-4d7a-a358-0336b7acacde`) is being built. Fresh build 8 is requested for both platforms to include the final allowance-label corrections: iOS `ce8384f3-20db-4e97-88d3-fd9ff6c13176`, Android `9f1d777f-1da9-4485-891b-57a0d0116bd3`. Build artifacts and submission outcomes must be verified before public release.
- Four Apple subscription products and four Google base plans are prepared with exact US prices. Apple products need remaining review metadata and first-version submission; Google plans remain draft until native purchase validation and notification setup are complete.
- Apple V2 production notification URL is configured. The configured keys authenticated against the sandbox API (an unknown transaction returned a structured error), but production Server API probes returned HTTP 401. Resolve/verify production access before native purchases launch; no successful real Apple callback or store purchase is claimed.
- Google Play API access works. Pub/Sub setup is blocked: the available Play service account returned 403, and local `gcloud` requires reauthentication as `joel@latticetech.ai`. The user was asked to renew that login. RTDN must then be configured in Cloud and Play Console and tested.
- Signed EAS Update publication was rejected because the Expo account is on Starter; code signing requires Production/Enterprise. No account upgrade was made and signing was not disabled. Fresh native builds are the release path for the final JS changes.
- Native purchase, restore, refund, account-switch and tablet/accessibility journeys still require device/store validation. Arbitrary signup-discount purchases on native stores remain blocked when the client cannot honor the captured offer; web discounts are implemented. Server-side native benefit binding requires a matching verified offer identifier.

## Additional planned design work

The plan includes edition-specific comparison, structured claim/evidence delta provenance and baseline-preview reader UI, system-issue inputs to custom editions, and a broader downgrade retention-selection flow. These are not shipped by the topic-history/catalog release and must not be represented as completed. A complete release against every item in the full plan also needs the remaining native workflows and full signed-device UX review above.

(c) 2026 Lattice AI LLC
