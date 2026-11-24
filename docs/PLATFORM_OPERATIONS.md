# Platform packages and operations

(c) 2026 Lattice AI LLC

## Account defaults

The settings JSON controls the policy captured for each **new** account:

```json
{
  "hosting": {
    "newUsersRequireSubscription": false,
    "nonBilledPlanKey": "catalog-only",
    "defaultPlanKey": "free"
  }
}
```

Production uses the example above. `catalog-only` allows five admin-designated free-eligible system subscriptions, no custom research, and shared catalog audio. Choose `complimentary-five` to grant any five system subscriptions, five custom topics, and unlimited audio without payment. Setting `newUsersRequireSubscription` to true uses `defaultPlanKey` for new accounts. Optional purchase availability is independently controlled by `billing.enabled`. Neither switch silently changes existing accounts or stops existing subscription reconciliation.

| Package | Custom topics | System subscriptions | Generated audio |
| --- | ---: | --- | --- |
| free / catalog-only | 0 | 5 free-eligible publications | Shared catalog playback |
| complimentary-five | 5 | Any 5 publications | Unlimited |
| paid-5 | 5 | Unlimited | 30 minutes per UTC day |
| paid-20 | 20 | Unlimited | 100 minutes per UTC day |
| byok | Unlimited | Unlimited | Own speech provider or operator-enabled local TTS |
| self-hosted / legacy unlimited | Unlimited | Unlimited | Operator-provided speech |

Paid prices are $10/month or $100/year for `paid-5`, and $30/month or $300/year for `paid-20`, without trials. Audio quotas meter **generation**, including voice previews, using the resulting duration; replay is free. Reserved time is included in admission checks and released or settled after work finishes. Daily windows reset at midnight UTC. Existing administrator cost/rate restrictions can still apply.

Use Admin, Plans to edit packages and assign a package. The user allowance editor shows inherited and effective values plus override sources. Each override requires a reason and can expire. `0` disables a numeric allowance; `unlimited` is explicit. Custom topics and system subscriptions are counted separately. Protected canonical catalog research is platform-owned, never charged to subscribers' topic/audio allowances.

## Publish system briefings

1. Open Admin, System briefings and create a draft publication with title, scope, category and language.
2. Configure its research topic, history policy, schedule, voice and automatic audio. Run and inspect an issue before publishing.
3. Select **Free eligible** for publications offered to free/catalog-only accounts. Publish with the current revision; stale edits return a conflict.
4. Members subscribe from System briefings and control email/push delivery. Global notification preferences also apply. Shared content remains in its canonical workspace; subscribers have private preferences and listening progress.
5. **Retired** stops new subscriptions/research while retaining existing authorized reads. **Retracted** revokes subscriber reads/audio. Unsubscribe stops delivery and frees a slot. Excess or ineligible subscriptions are suspended after an allowance change; content is not deleted.

The deployment does not publish existing private user topics. An administrator must curate and publish the launch catalog. Until then, new catalog-only accounts see the empty catalog explanation.

## Signup codes

Admin, Signup codes supports complimentary grants and paid offers as distinct choices. A paid offer selects a billing package, optional custom entitlement package, percentage discount and optional discount duration. Redemption atomically captures the offer and consumes its usage limit. A code alone never verifies a payment. Stripe checkout binds the captured benefit after verified purchase; retries preserve the original offer and provider idempotency key.

Native offers require a matching provider offer identifier and a verified transaction carrying that identifier. The current mobile UI disables a discounted purchase it cannot honor instead of charging full price. Arbitrary native signup-discount purchase flows are not released; use the web offer or complete provider-specific offer setup and client validation before enabling them. Revoking an entitlement benefit does not retroactively remove an already contracted Stripe discount or issue a refund; manage financial adjustments in the original provider.

## Private provider keys and history

Account, Your API keys stores encrypted Chat, Embedding and Speech credentials. Secrets are write-only; API responses expose validation status and a last-four marker. Test the chat connection, then explicitly choose personal funding. Invalid keys block personal generation without falling back to platform funds. A paid subscription/admin assignment can continue to determine effective access; cancel or change the assignment before switching a conflicting funding arrangement.

BYOK speech requires a validated personal speech key unless the operator enables `tts.allowLocalForByok`. Set that only when the configured speech service is operator-provided and its capacity is available. Docker self-hosting needs no Stripe, Apple or Google configuration; its default unlimited package and operator-managed endpoints continue to work.

Topic history choices are Off, PreviousIssue, RecentIssues, SinceDate and SpecificIssue. The server selects only owned topic issues, bounds count/text, and freezes the baseline for each run so retries agree. Off produces standalone research and disables historical deduplication. Missing/deleted baselines do not authorize access to another user's content. Edition-specific selection and structured claim-by-claim delta provenance are additional planned work, not shipped API fields.

## Provider configuration

`scripts/billing/provision_stripe.py` creates app-specific products, four fixed recurring prices, an isolated portal configuration, and an initially disabled webhook. Supply the existing account key through the documented environment variable; write its output outside Git. Put the resulting price IDs in `billing.offers`, the secret key/webhook secret/portal configuration under `billing.stripe`, and use the fixed HTTPS dashboard origin. Never put credentials in Postman collections, SDK examples, or tracked JSON.

Provider callbacks:

- Stripe: `/v1.0/public/billing/stripe`; original request body and `Stripe-Signature` are required.
- Apple: `/v1.0/public/billing/apple`; signed V2 notifications, expected bundle and Apple app ID.
- Google: `/v1.0/public/billing/google`; Pub/Sub OIDC token with the exact audience and push service-account email, plus the expected package name.

Callbacks reconcile current authoritative state, with a durable retry inbox and periodic recovery. Checkout redirects never grant access. Keep `billing.allowSandbox` false in production. Sandbox purchases need an isolated test deployment. Disabling new purchases does not disable reconciliation or subscription management.

`scripts/billing/provision_native.py` creates unsubmitted Apple subscriptions and draft Google base plans, initially priced in the US. Store approval, native builds, review metadata/screenshots, RTDN setup and sandbox/device purchase testing remain separate steps. See [release status](PLATFORM_IMPLEMENTATION_STATUS.md). Apple requires the first subscription submission with an app version ([Apple instructions](https://developer.apple.com/help/app-store-connect/manage-submissions-to-app-review/submit-an-in-app-purchase)); Google requires Pub/Sub and Play Console RTDN configuration ([Google instructions](https://developer.android.com/google/play/billing/getting-ready)).

Account deletion closes pending Stripe checkouts and stops Stripe/Google renewal before ownership data is removed. The user must cancel Apple renewal in App Store settings; both clients display this notice. Deletion fails explicitly if a provider cannot confirm the required cancellation, allowing a safe retry. Billing/audit history must not be repurposed as subscriber content access.

## Verification and rollout

Run the solution build, PostgreSQL test suites, dashboard lint/test/build/i18n checks, mobile lint/Jest/export, SDK harnesses and generated API checks. The opt-in Stripe sandbox scenario only accepts a test key; it creates and cleans up its own test customer/subscription. The Postman dependency simulation proves caller recovery under faults, not provider compatibility. `assets/postman/PlatformReleaseSmoke.postman_collection.json` is restricted to local 12xxx ports and never makes a purchase.

Rehearse migrations against a private local backup with `scripts/validation/MigrationRehearsal`. It refuses non-loopback/non-rehearsal databases and runs migrations/seeds twice. Production deployment backs up the database/config, pins rollback images, and replaces only server/dashboard services. Verify captured existing policies, a new catalog-only account, custom-topic refusal, catalog/provider/usage reads, checkout without payment, checkout cleanup, and signed callback rejection/acceptance.

The 2026-11-24 deployment uses server `tiers-20261124-2` and dashboard `tiers-20261124-1`; backup `/home/ubuntu/mymorningnews/backups/tiers-20261124T022634Z`. Prefer a forward fix or disabling new purchases over restoring the old application: older binaries do not enforce these new policies. A database restore requires reconciling intervening payments and content. Never restore blindly after accepting purchases.
