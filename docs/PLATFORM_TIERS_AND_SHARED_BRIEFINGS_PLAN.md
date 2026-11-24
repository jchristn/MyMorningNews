# Platform tiers, shared briefings, and historical context

Status: implementation and release plan. Prepared November 23, 2026 against repository revision `f026959`; the implementation is tracked in [PLATFORM_IMPLEMENTATION_STATUS.md](PLATFORM_IMPLEMENTATION_STATUS.md). Release gates below remain explicit until verified.

This plan covers the PostgreSQL schema, server and background workers, dashboard, iOS and Android apps, C#/JavaScript/Python SDKs, MCP, OpenAPI, Postman, automated tests, billing, Docker, operations, documentation, and a final UX and styling review. The historical plan in `archive/MY_MORNING_NEWS_PLAN.md` remains unchanged.

## 1. Product contract and confirmed decisions

Use four product categories: Free, Self-hosted, Bring your own key (BYOK), and Paid. Paid has two packages. The final pricing decision supersedes the original proposal for a single paid plan with 10 briefings.

| Category / proposed internal key | Custom briefing topics per user | Active system subscriptions per user | Generated custom audio | Price |
|---|---:|---:|---|---|
| Free / `free` | 0 | 5, from the admin-selected free catalog | No custom generation; shared catalog audio included | Free |
| Self-hosted / `self-hosted` | Unlimited | Unlimited within that deployment | Unlimited, using deployment resources | No platform subscription; operator pays infrastructure/provider costs |
| BYOK / `byok` | Unlimited | Unlimited | Unlimited with the user's speech key or available local TTS | No platform subscription proposed; user pays provider charges |
| Paid 5 / `paid-5` | 5 | Unlimited, separate from the custom-topic cap | 30 generated minutes per day | $10/month or $100/year |
| Paid 20 / `paid-20` | 20 | Unlimited, separate from the custom-topic cap | 100 generated minutes per day | $30/month or $300/year |

Prices are interpreted as USD. Final public package names and non-US store prices are release configuration. There is no trial period. Web purchases use Stripe; mobile includes native Apple App Store and Google Play subscriptions. All channels grant the same product entitlements.

System administrators publish system-wide topics and independently select which are free-eligible. Every signed-in user can discover the system catalog; free users can subscribe only to its free-eligible subset, up to their limit. Paid and BYOK users can subscribe to all published system topics. Availability is within the connected deployment; a self-hosted installation does not automatically receive the hosted service's catalog or credentials.

### 1.1 Terminology and counting

- A **topic** is a recurring briefing definition, including its research brief, schedule, sources, context policy, and output settings.
- A **briefing issue** is one generated result. Historical issues do not consume topic/subscription slots.
- A **system subscription** links a user to an administrator-managed topic. It does not clone the topic or cause a new research run.
- An **edition** assembles several topics. Its constituent custom topics still consume their own slots. Editions cannot bypass generation or audio limits.
- Use “custom briefings” and “catalog subscriptions” in purchase and usage screens, with a short explanation of what is counted. Keep existing topic API names.
- Count non-archived custom definitions, including drafts and paused topics, to prevent draft/pause bypasses. Archived definitions retain history but cannot run. Restoring one must acquire a slot. Add an explicit archive state/action if the existing status model cannot express this distinction.
- An active catalog subscription consumes a free slot even when email or push is muted. Unsubscribing releases the slot. Re-subscribing reacquires it atomically.
- Scope entitlement ownership to the existing authenticated account identity, `(tenantId, userId)`. A billing purchase binds to exactly one such account. Do not match purchases by email alone or silently extend a purchase to unrelated tenant memberships.

### 1.2 Proposed defaults, distinct from confirmed requirements

These are implementation defaults, configurable where indicated, rather than additional commercial commitments:

| Decision | Proposed behavior |
|---|---|
| Daily audio boundary | Calendar day at 00:00 UTC, with reset shown in the user's local time; no rollover |
| System subscription limit outside Free | Unlimited; configurable per plan and per user |
| Free custom operations | Disable custom topic creation, refinement, one-time research, custom edition generation, model calls, and custom audio |
| BYOK activation | Explicit choice plus a successfully validated personal model connection; merely saving a key does not change plan |
| New topic history default | Off, with a visible comparison option during creation |
| Existing topic history | Migrate to enabled comparison with up to five recent usable briefings; record legacy origin and fallback behavior |
| Subscription history | Newly subscribing grants retained published issues for that topic; browsing previews does not grant the full archive |
| Downgrade | Preserve content and show a resolution flow; stop disallowed future generation at the effective downgrade time |
| Provider cost caps | Track platform-funded and user-funded calls separately; do not apply old free-tier cost caps to unlimited BYOK use |
| Hosted operational protection | Rate, concurrency, per-job size/time, retention, and abuse controls remain separate from commercial quantity limits |

No unresolved product question blocks writing this plan. Live product IDs, billing credentials, tax configuration, localized store pricing, and operating safety budgets must be configured and verified before launch.

## 2. Repository baseline and impact

The implementation should extend the current mechanisms rather than introduce a second entitlement system.

| Existing area | Observed behavior | Required change |
|---|---|---|
| `src/MyMorningNews.Core/Entitlements/EntitlementService.cs` | Built-in Free, Complimentary, Unlimited; plan features and time-bounded assignments; missing feature values can mean unlimited/allowed | New packages, typed validation, explicit capability defaults, per-user overrides, deterministic assignment resolution |
| `EntitlementService.MeterAsync` | Counts recurring topics; computes monthly audio from up to 5,000 media rows | Separate custom topics/subscriptions and durable daily audio accounting without paging truncation |
| `EntitlementService.CheckAudioAsync` | Checks current consumption before queuing; no atomic future-duration reservation | Transactional reservation, reconciliation, retry/crash recovery |
| `Models/Topic.cs`, `TopicRoutes.cs` | Topic has tenant/user ownership, novelty preference, voice and audio options | Publication scope, context settings, archive behavior, subscriber projection |
| `Pipeline/Stages/AssembleContextStage.cs` | Automatically loads summaries from up to five completed/partially successful runs and injects `history.recent` | Explicit context selection, actual briefing baselines, snapshot provenance, full off behavior |
| `Defaults/Prompts/research.*.md` | Query, synthesis, and agentic-search prompts consume history | Consistent history/delta instructions across the complete pipeline |
| `Services/ModelGateway.cs` | Topic/tenant model assignment and platform/shared-model fallback | Account-specific funding resolution; BYOK cannot silently use platform credentials |
| `Services/VaultService.cs` | Encrypted credentials; existing reveal and tenant-admin capabilities | Personal ownership/usage scopes; restricted hosted BYOK reads and writes |
| `Routes/PromoRoutes.cs`, `PlanSourceEnum` | Plan administration and promotions; Billing enum exists | Real billing lifecycle, product mapping, explicit precedence, preserve promotion behavior |
| `Briefings/BriefingService.cs`, `Audio/AudioService.cs` | Outputs and delivery associated with owners | Generate shared content once, authorize subscriber reads, fan out delivery separately |
| Dashboard `PlansView`, `UsersView`, `TopicsView`, `TopicWorkspace`, `PlanCard` | Existing administration, topic workspaces and plan presentation | Catalog, customer limits, history control, BYOK onboarding, billing and override UX |
| Mobile `src/screens`, `src/api`, `parity.json` | Broad member features; operator administration intentionally web-only | Full member feature parity, personal provider connections, native purchases and restoration |
| `src/Tools/apidocs`, `assets/postman` | OpenAPI and route metadata generate REST reference and collection v2.1 | Extend generator inputs and executable scenarios; preserve generated-artifact ownership |
| `sdk/`, `src/MyMorningNews.McpServer` | Three SDKs and MCP cover current API | Add every new member/admin contract and typed errors appropriate to each surface |
| `docker/hosted`, `docker/production` | Hosted overlays allow agent CLIs for global administrators only | Preserve that restriction; reconcile older README/hosting text that says they are disabled |

Also audit `RouteBase`, `RequestPipeline`, RBAC operation maps, setup gating, scheduler/run/briefing/media/email/retention workers, sharing, feeds, exports, user deletion, notifications, and cache keys. These paths can otherwise bypass a route-level entitlement check.

## 3. Domain model, schema, and migrations

Keep PostgreSQL and the existing database-driver interfaces, implementations, and query files. Use PrettyId identifiers, lowercase column names without separators, explicit columns, and normal foreign keys. Do not put the new business model into unvalidated JSON blobs.

### 3.1 Entitlements and deployment

1. Add an explicit operator-controlled deployment mode: Hosted or SelfHosted. Derive it from server configuration, never an account request, hostname guess, or mobile preference.
2. Keep `Plan`, `PlanFeature`, and `PlanAssignment`; add a schema-backed feature registry defining type, units, bounds, unlimited support, description, and default. Validate edits before saving.
3. Add `UserEntitlementOverride` keyed by account and feature, with value/mode, optional expiration, reason, author, and audit timestamps. Separate “inherit,” “finite value,” and “unlimited”; zero is a real limit.
4. Add plan revision/default provenance so later migrations can distinguish a seeded default from an administrator edit. The current “old value equals prior default” heuristic cannot prove that a value was never intentionally customized.
5. Add an entitlement revision for cache invalidation and optimistic concurrency. Propagate changes across server instances, workers, sessions, and clients.
6. Use a typed effective-entitlement response with resolved limits, capabilities, source of each value, expiry, revision, and reset boundaries. Return unlimited explicitly, never as a huge integer.

Proposed features include `customTopics.max`, `systemSubscriptions.max`, `customResearch.enabled`, `oneTimeResearch.enabled`, `catalog.audio.enabled`, `audio.generation.enabled`, `audio.dailySeconds.max`, `models.personalConnections.enabled`, and `models.fundingMode`. Continue existing configurable run/day, schedule interval, retention, email, storage, and pinned-artifact settings where meaningful. Keep compatibility mappings for old `topics.max` and monthly audio fields until client migration is complete; do not relabel a daily limit as monthly.

### 3.2 Catalog and subscriptions

Use a protected system workspace and service principal to own canonical system research topics. Add explicit topic scope and publication metadata; avoid nullable owners or arbitrary cross-tenant reads.

| Entity / addition | Required data and constraints |
|---|---|
| Topic scope and configuration revision | Personal/System, owner, archive state, history policy, version |
| Catalog publication | System topic ID, stable slug, title, description, category, language, sort order, publication state, free eligibility, publication revision, attribution, published/retired timestamps |
| Topic subscription | Subscriber account, system topic, status, delivery preferences, subscribed/unsubscribed timestamps, version; unique account/topic relationship |
| Published issue projection | Canonical issue/version and allowlisted readable fields/evidence/media; publication status and retention policy |
| Subscriber interaction state | Read/listen progress, bookmarks, private feedback, notification state, keyed by subscriber and issue |
| Delivery outbox | Publication/issue, subscriber, channel, delivery status and retry key; uniqueness prevents duplicate sends |

Keep system subscriptions separate from private topic ownership. Dedicated catalog queries may read the protected system workspace and return safe projections. Normal topic/finding/run queries retain tenant restrictions. A raw `isSystem` flag must never disable tenant isolation globally.

### 3.3 Usage and billing

- Add an append-only `UsageLedger`, with account, funding source, metric, integer quantity, UTC period, logical operation, outcome, and correction references.
- Add `UsageReservation` and an atomically updated period balance for active holds and committed usage. Uniqueness on operation/metric makes retries idempotent. Derive/rebuild balances from the ledger.
- Add billing account mapping, provider subscription, product/price mapping, verified purchase binding, provider event inbox, and reconciliation cursor/state. Include provider and sandbox/live environment in unique keys.
- Separate subscription lifecycle from entitlement projection. Keep billing events and plan-assignment history traceable without making “last webhook wins” the entitlement rule.
- Add immutable run-context snapshots and child baseline references containing issue IDs/revisions, timestamps, hashes, selection policy, truncation information, and prompt/model versions. Store any bounded context artifact under the existing protected artifact mechanism.

### 3.4 Database correctness

Use account-level transactional locking or equivalent serializable updates for create/restore/subscribe and usage reservations. Lock subscription changes against publication eligibility/version changes. Add indexes for account/status counts, published catalog filters, subscriber fan-out, due events, reservation recovery, and context lookup. Use unique constraints as the final defense against duplicate subscriptions, purchases, deliveries, and charges.

Provide versioned additive migrations, backfill jobs with resumable checkpoints, fresh-install seeds, upgrade fixtures, and a migration report. Take a restorable backup before production migration. Do not overwrite customized plans, publish existing private topics, or erase existing research to fit new caps.

## 4. Entitlement evaluation and enforcement

### 4.1 Resolution order

Resolve authorization and commercial access separately. A role does not make an account paid, and a paid account does not confer administration rights.

1. Resolve deployment mode and account identity.
2. Determine the active funding/category choice: self-hosted, free, BYOK, or an entitled paid package.
3. Resolve verified billing entitlement and existing administrative/promotional assignments using documented priority and effective dates. Prefer an explicit temporary administrator plan assignment, then active paid entitlement, then applicable promo grant, then selected eligible BYOK/free default. A promo must not silently downgrade an active paid account. Preserve existing queued promo periods with a migration rule and tests.
4. Apply active per-user feature overrides on top of the chosen plan.
5. Intersect resulting capabilities with role permissions, resource ownership, provider health, and deployment capabilities. Hosted users cannot grant themselves self-hosted behavior or platform credentials through an override.
6. Apply operational controls independently. Report operational throttling separately from paid plan caps.

Administrator overrides require a reason and explicit unlimited controls. Show “Plan default: 5; override: 8; effective: 8,” plus author and expiry. Removing an override returns the account to the current plan default. Plan edits show impacted accounts and take effect through a revision, not an unannounced bootstrap rewrite.

### 4.2 Enforcement matrix

| Action | Required server behavior |
|---|---|
| Create, clone, import, restore custom topic | Require custom capability; acquire custom-topic slot atomically |
| Convert one-time request to recurring | Same slot acquisition; preserve request if acquisition fails |
| Subscribe/reactivate catalog subscription | Check published/free eligibility; acquire subscription slot atomically |
| Change delivery preferences | Does not create a slot or authorize custom research |
| Refine, test model, run now, retry stage, schedule | Resolve payer and owner, check capabilities and relevant run/provider budgets |
| Edition composition | Check access to each constituent and composition capability; do not launch system research per subscriber |
| Audio generation/regeneration | Reserve daily audio allowance before costly work; record correct funding source |
| Listen, replay, seek, range request, download | Authorize asset; no generated-audio debit |
| SDK/MCP calls or old client direct requests | Same service-layer checks as dashboard/mobile |
| Worker starts queued job | Recheck live eligibility and resource access; release reservations if no longer allowed |
| User downgrade or override expiration | Recompute, pause disallowed future work, preserve history, notify clients |

For shared tenant operations, authorize the caller but attribute limits and provider spend to the resource's owner. Do not accidentally charge an administrator merely because they clicked Run. System-topic generation uses the system service principal and operator budget.

### 4.3 Audio accounting

Meter generated output duration, not playback. Store seconds or finer integer units, round only for display. Daily limits are 1,800 seconds for Paid 5 and 6,000 seconds for Paid 20. Shared catalog audio is generated once at platform expense and never debits individual subscribers' custom-audio allowances.

Algorithm:

1. Resolve a stable logical generation ID, payer, content revision, voice/script settings, and period. Reuse an existing matching ready asset without charging again; an explicitly requested new rendition is new generation.
2. Reserve an explicit requested maximum duration atomically against `committed + reserved`. Automatic generation allocates from the remaining budget using the topic's configured target duration. Reject an oversized request before generation or offer a shorter rendition.
3. Generate a bounded script, synthesize in bounded segments, and measure output duration. If a segment exceeds the remaining allowance, do not publish or debit an over-limit result. Finish at an appropriate sentence/chapter boundary, or leave text available and report that audio could not fit. Record discarded provider work as operational cost, not delivered user minutes.
4. Commit actual published duration once and release unused reservation. Failed jobs with no published playable asset release the hold. Operational retries reuse the same logical operation; provider-side retry costs remain observable.
5. Recover expired holds only after checking job leases and generation state. Reconcile crashes between media upload, ledger commit, and asset publication; publish only after committed entitlement accounting.
6. Queue intent may be recorded earlier, but choose the daily period when execution acquires its hold. A bounded job crossing midnight settles against that reserved period. An expired/restarted job re-reserves for the current period; retain already-generated work only under a documented reconciliation rule.
7. Deleting media never refunds historical generated usage. Explicit administrative corrections use ledger adjustments with reasons. Voice previews and health checks use a small separate rate-limited operational budget and cannot become arbitrary long synthesis endpoints.

At the cap, show a precise explanation and next reset time; text remains readable. Do not silently enqueue tomorrow's work or discard the user's request. A downgrade reduces the limit without resetting today's usage. Upgrades expand remaining allowance without giving a second daily balance. Switching billing intervals, devices, time zones, or funding modes cannot reset usage.

## 5. System-wide topics and subscriber lifecycle

### 5.1 Administration and publishing

Provide a dedicated System catalog area for global administrators and the system-administration identity. Tenant administrators have no publication authority. New permissions must use explicit global checks even though existing RBAC contains broad tenant-admin bypasses.

Administrators can create a system topic, edit its research definition, schedule, history mode, template, language, and voice, preview a sample, publish/unpublish, choose free eligibility, reorder catalog entries, inspect generation health and cost, and retire a topic. Creating a topic and publishing it are separate operations. Publishing requires a complete readable definition and a valid generation configuration.

Converting private content to system content must be an explicit reviewed copy: show what will be published, remove private prompts/notes/preferences, and never automatically republish old private issues. Ordinary users cannot alter publication fields through topic updates or forged SDK payloads.

System generation produces one canonical issue and permitted audio rendition per configured variant. Subscribers cannot change the canonical prompt, schedule, sources, history policy, or voice. Paid/BYOK users may clone a system definition into a private custom topic if enabled; the clone consumes a custom slot and uses their own funding and context history. Do not copy platform credentials or subscriber data.

### 5.2 Reading and delivery

- Catalog browse includes category/language filters, search, cadence, latest publication time, sample, audio availability, free eligibility, and subscribe state.
- My subscriptions contains subscribed entries, latest issues, mute options, and unsubscribe. Today, Briefings, Listen, search, and notifications merge eligible system issues with private issues, with stable pagination and no duplicates.
- Scope subscriber progress, feedback, and bookmarks to the subscriber. Do not feed one subscriber's private preferences into a canonical system briefing. System editorial feedback can use separately permissioned aggregate reporting.
- Use a durable outbox to fan out email/push only after publication commits. Unsubscribe and account deletion must be checked again before delivery. Store delivery outcomes per subscriber without exposing recipient lists.
- Podcast feeds use the subscriber's feed token and authorized shared assets. Range requests, cache/CDN URLs, exports, and media downloads require the same eligibility checks; revoke tokens on account deletion as today.
- Editions can consume already published system issues for entitled members. They cannot reschedule canonical research. Custom edition composition and its new audio use the requesting owner's funding and quotas.
- Preserve existing deliberate public-sharing behavior, but add a system-publication sharing policy. Subscription alone does not authorize a user to mint public links to administrator-owned content. Global unpublish/retraction invalidates related online share access.

### 5.3 Changes and removal

Unsubscribe stops future access/delivery through that subscription; retain private interaction metadata according to retention policy. Re-subscribe restores access to still-published retained issues. Retiring a topic stops new subscriptions and new generation, while existing subscribers may read retained published history. Emergency unpublish/retraction removes online access and invalidates caches/links; previously downloaded copies cannot be remotely guaranteed erased.

Changing free eligibility shows the admin how many free users are affected. Default to an announced grace period before suspending those free subscriptions; the subscription itself remains restorable and no longer consumes an active slot once suspended. If immediate retraction is required, provide a separate explicit action. The same lifecycle rules must hold in feeds, mobile offline refresh, push deep links, and exports.

## 6. Historical briefing context and delta generation

### 6.1 User-facing choices

Add “Compare with past briefings” to topic creation and Settings > Context, available to custom-topic owners and system-topic administrators. Explain: “Highlight what changed since your earlier briefings.” Show the selected baseline and estimated context size before running where feasible.

| Mode | Behavior |
|---|---|
| Off | No past briefing/run summaries supplied as comparison context; produce a standalone briefing |
| Previous issue | Compare against the latest usable published/ready issue before the current run cutoff |
| Recent issues | Compare against the latest N usable issues; configurable bounded count and token budget |
| Since date | Compare against usable issues in a bounded window starting at a selected timestamp |
| Specific issue | Compare against one accessible issue of the same topic, or same edition for edition-level comparison |

Store explicit history settings, with validated count/window/token bounds, a baseline ID when relevant, and a configuration revision. Selecting history does not mean blindly inserting the entire archive. A fixed selected issue is validated on save and again on execution. Ordinary subscribers see the system topic's comparison mode as read-only; a personal clone can choose its own.

### 6.2 Pipeline changes

1. Introduce a context resolver used by runs, edition composition, one-time reports when explicitly given an accessible baseline, and stage retry. Select baselines deterministically before planning.
2. Use prior ready briefings and relevant structured claims, not only run summaries. Exclude the current run, future-dated issues, unpublished/private issues outside scope, failed outputs, and unavailable artifacts. A partially successful run is usable only if it produced a valid ready briefing.
3. Snapshot the policy, selected issue revisions, comparison cutoff, hashes, bounded context, and omission reasons. Include those in `ContextSnapshotHash`. A retry reuses the snapshot unless the user explicitly requests a new run/rebase. Do not silently pick a later issue on retry.
4. When Off, clear `history.recent`, comparative baseline fields, and comparison instructions in query planning, agentic search, synthesis, final composition, and audio scripting. Audit prompt overrides and tools so historical text is not fetched indirectly as comparison context.
5. Keep fetching cadence, duplicate-fetch avoidance, and ordinary user preferences distinct from briefing comparison. Off must not leave novelty/history scoring silently suppressing information needed for a standalone report. Repeated URLs may contain updated facts and need change-aware fetching.
6. Introduce structured deltas: new development, changed fact, resolved item, correction, unchanged material, and unavailable evidence. Every asserted change links to baseline and current evidence. Previous AI prose is comparison material, never fresh supporting evidence.
7. Show “Compared with [issue/date]” in the reader, with a collapsible baseline explanation. Render a meaningful “No material changes found” result when appropriate. Audio and email accurately reflect the same conclusion.
8. First run or missing historical data produces a standalone briefing with “No previous briefing available” metadata. A missing explicitly pinned baseline must produce an actionable error or explicit user-selected fallback, never silently switch baseline.
9. Enforce context token budgets per provider and deterministic truncation. Summaries of prior material are versioned and traceable. Surface omitted/truncated context in run diagnostics.
10. Treat historical content as untrusted input, keep the existing scope guardrail, and prevent embedded historical instructions from overriding the research task.

Edition comparison occurs at composition time against prior editions; topic research keeps its own independent policy. One-time reports default Off. Retention either preserves an explicitly pinned baseline within the account's storage/pinning limits or surfaces its unavailability. Deletion/export must handle snapshot artifacts and references without leaking another user's material.

Migration should preserve the intent of existing automatic history, while acknowledging that actual briefing baselines are not identical to old run summaries. Backfill available prior issues; record a bounded legacy-summary fallback only where needed for old data, visibly identify it in diagnostics, and phase it out as new issues become available. New topics default Off.

## 7. BYOK, setup, and provider routing

Build personal provider connections on the existing vault and PolyPrompt gateway. Expose provider labels such as Anthropic/Claude, OpenAI, and Google Gemini, distinguishing provider API keys from agent CLI sign-ins. “Codex” is not assumed to be a universal API-key type. Verify supported models/authentication against the installed provider adapters before advertising them.

- Personal connection lifecycle: add, validate with a small bounded call, choose default/task assignments, inspect masked status, rotate, disable, delete. Never return raw secrets in list/detail, logs, telemetry, request history, exports, screenshots, or SDK fixtures.
- Add owner-scoped credentials and connection references. Member A cannot resolve or reveal member B's key in the same tenant. Review the existing tenant-admin reveal policy for the new personal-key class; use separately audited global support access only if explicitly designed.
- Resolve funding for every model task, including refinement, search planning, extraction, ranking, embeddings, synthesis, prediction, learning, edition composition, and speech-script generation. Handle background work with the owner's identity, not whichever user last edited a topic.
- BYOK private generation uses an eligible personal connection. A missing/expired/revoked key blocks dependent generation and yields a repair action; it must not silently fall back to the platform key or another tenant's connection.
- Paid generation uses platform-funded models by default. Saving a personal key does not cancel billing or change tier. Switching to BYOK is explicit; explain the remaining subscription and cancellation date. Avoid an unannounced hybrid of unlimited BYOK topics and unlimited platform-funded paid generation.
- BYOK speech is a separate capability: personal speech-provider key or operator-enabled local TTS. Reuse shared catalog audio freely. If neither is available, retain text and show speech setup guidance.
- Classify shared search, embeddings, email, storage, local TTS, and crawl capacity as operator-provided infrastructure, with transparent operational limits and cost visibility. “Unlimited” topic/audio quantities cannot imply infinite concurrency or free third-party services.
- Hosted ordinary users select approved provider hosts; custom endpoints require explicit policy and existing private-network/SSRF defenses. Keep global administrator-only agent CLI behavior in hosted deployments. Self-hosted operators retain configurable local/private endpoints and CLI support.
- Split setup readiness into deployment readiness, personal research readiness, and optional speech readiness. Free readers must not be trapped behind a tenant model setup gate. BYOK users need a personal repair/setup flow on both dashboard and mobile.

## 8. Billing across Stripe, Apple, and Google

### 8.1 Products and ownership

Create four purchasable offers per channel: Paid 5 monthly/yearly and Paid 20 monthly/yearly. They map to two entitlement packages. Store prices in provider configuration, never trust client-supplied amounts or price IDs. Stripe's USD prices are $10/$100 and $30/$300. Display store-localized prices returned by the native purchase system; do not hard-code dollar prices globally. Do not enable trial or introductory-free offers.

Persist a verified binding between provider customer/subscription/transaction lineage and the platform account. Validate package/bundle ID, environment, product allowlist, ownership, and status server-side. Restore must not let an already-bound purchase migrate to an arbitrary account. Provide a support-assisted account-link resolution path with audit evidence.

Prevent duplicate purchases by checking the account's active channel before checkout. Show where an existing subscription is managed. Do not automatically cancel a subscription on another channel, grant double quotas, or silently stack two paid packages. If duplicates nevertheless occur through external purchases, preserve records, resolve effective access deterministically, and offer support instructions.

### 8.2 Stripe

Use server-created Checkout subscription sessions and the Stripe customer portal. Allow only configured offer IDs and allowlisted return URLs. Bind requests to the authenticated account and use idempotency keys. The success page polls verified account state; a redirect alone never grants paid access. Stripe documents fulfillment as a server-side process and supports the portal for subscription/payment management. [Checkout fulfillment](https://docs.stripe.com/checkout/fulfillment), [customer portal](https://docs.stripe.com/billing/subscriptions/customer-portal).

Implement a raw-body webhook endpoint with signature verification, replay protection, durable event inbox, deduplication, and asynchronous processing. Reconcile current subscription/payment state when events are duplicated, delayed, or out of order. Cover checkout completion, subscription updates/deletion, invoice payment success/failure, refunds and disputes, with a documented entitlement policy for each. Pin and test a supported Stripe API version. [Stripe subscription webhooks](https://docs.stripe.com/billing/subscriptions/webhooks), [webhook handling](https://docs.stripe.com/webhooks).

### 8.3 Native purchases

Add a maintained native purchase bridge compatible with the installed Expo/React Native versions; verify compatibility in a development build before choosing the package. Configure App Store subscription grouping and Google subscription products/base plans to represent both packages and intervals.

Apple integration verifies transactions server-side and consumes App Store Server Notifications V2 for renewals, expiration, refunds, and lifecycle changes. Include restore purchases, account binding, pending purchase handling, and sandbox/production separation. [Apple server notifications](https://developer.apple.com/documentation/appstoreservernotifications).

Google integration verifies purchase tokens through the server, consumes authenticated real-time notifications, retrieves authoritative subscription state, and acknowledges qualifying purchases promptly. Support renewal, grace, hold, expiry, cancellation, replacement and restoration. Google documents `purchases.subscriptionsv2.get` as the subscription-state source and requires acknowledgement of new subscription purchases within three days. [Google subscription lifecycle](https://developer.android.com/google/play/billing/lifecycle/subscriptions).

Expose native purchase, restore, pending, canceled, already subscribed, manage subscription, and repair-account-link states. A lost network response after payment must recover through verification/restoration rather than prompting another purchase. Native purchase code requires an appropriate signed/development binary; do not assume an OTA update alone supplies native modules.

Store payment and external-link rules differ by platform and storefront. Review launch flows against the current rules, including BYOK onboarding, promotional grants, subscription management links, and account creation. Do not assume the app qualifies for a reader-app exception or that a Stripe link is allowed everywhere. [Apple review guidelines, section 3.1](https://developer.apple.com/app-store/review/guidelines/), [Google Play payments policy](https://support.google.com/googleplay/android-developer/answer/9858738).

### 8.4 Normalized lifecycle and transitions

Normalize provider states into pending, active, grace, suspended, canceled-but-entitled-until, expired, and revoked, retaining original provider state. No trial state is offered for these products. Provider-recognized grace periods are handled explicitly; Stripe grace duration is operator-configurable with a documented launch value.

Upgrades grant the higher package only after authoritative confirmation. Downgrades and annual/monthly transitions follow the provider's effective date and proration rules, clearly shown before purchase. Cancellation preserves access until the verified entitlement end unless a refund/revocation says otherwise. A provider outage does not immediately revoke a previously verified active account; use bounded reconciliation tolerance and alert operators.

On Paid 20 to Paid 5, let the user choose five custom topics to keep enabled before the change takes effect. At expiry, pause excess topics deterministically if no choice was made and offer a resolution screen. On paid/BYOK to Free, pause all custom generation, retain private history under retention policy, and let the user keep up to five eligible system subscriptions. Paid expiry returns to Free by default; continued BYOK requires an explicit selection and valid connection.

Retained excess definitions may temporarily leave the account over its new storage-of-definitions count. Only the selected in-plan subset can generate; creating, cloning, importing or restoring another definition stays blocked until the user archives enough excess definitions or upgrades. Pausing alone does not free a slot. This grandfathered read-only state must be explicit in the count API and UI, rather than deleting data or pretending the account is within its limit.

Account deletion requests cancel renewable subscriptions through supported provider actions or clearly guide required store management, stop future work, revoke delivery tokens, and purge personal data after the existing grace period. Explain any minimal billing record retention separately from research retention. Billing reconciliation and missed-webhook recovery must work after server downtime.

## 9. API contract, SDKs, MCP, and Postman

### 9.1 REST additions and extensions

The paths below are proposed contracts. Final naming must follow the existing Watson route registrars, `/v1.0/api` member/admin conventions, control-plane user routes, `Doc(...)`, request-body metadata, query schemas, paging, and RBAC operation maps.

| Contract area | Proposed operations |
|---|---|
| Public configuration | Extend `GET /v1.0/api/client-config` with deployment/capability versions and available purchase channels, excluding secrets |
| Effective plan and usage | Extend `GET /v1.0/api/account/plan` and `/account/usage`; add explicit daily windows, used/reserved/remaining, custom/system counts and field provenance |
| Offers and billing | `GET /v1.0/api/plans`; `GET /account/billing`; `POST /account/billing/checkout`, `/portal`, `/verify-purchase`, `/restore-purchases`; provider management instructions |
| Provider callbacks | Dedicated Stripe/Apple/Google callback routes authenticated by provider verification, excluded from user bearer/setup gates |
| Catalog read | `GET /v1.0/api/catalog/topics`, `/{id}`, `/{id}/briefings`; safe publication/preview schemas |
| My subscriptions | `GET /v1.0/api/subscriptions`; idempotent `PUT /subscriptions/{systemTopicId}` and `DELETE`; delivery settings in the subscription update |
| Catalog administration | CRUD draft publications, preview, publish/unpublish/retire, eligibility changes and impact preview under `/api/admin/catalog/...` |
| Personal providers | CRUD and bounded verify operations under `/api/account/provider-connections`; task/speech assignments and masked health |
| User overrides | Read/replace/remove feature override and inspect effective state under existing `/v1.0/tenants/{tenantId}/users/{userId}/...` control-plane routes; global-admin guard |
| Topic history | Extend topic create/update/read and refinement completion DTOs; baseline choices/preview and archive/restore operations |
| Editions and reports | Extend edition/report context fields and accessible system-topic inputs; reject unsupported/free custom composition |
| Reader/media | Extend existing issue/media reads for subscription-authorized projections, comparison provenance and subscriber progress |

Document null/clear behavior, limits, units, enums, idempotency, optimistic concurrency, pagination, ownership, and error examples. Validate counts and dates on the server; never accept client-supplied user IDs as billing authority.

Preserve the existing `402 CapReached` envelope for quantity limits, extending it with feature, used, reserved, limit, period/reset, and available remediation. Use `403` for denied capability/administration, `404` for inaccessible resources, `409` for stale version/purchase binding/conflicting state, and existing `429 QuotaExceeded` for operational throttling. Provider-key repair has a stable typed error; never include provider secrets or raw authentication responses. Audit decisions and return correlation IDs.

### 9.2 SDK and MCP parity

- C#: add resources/methods and XML-documented DTOs; update `sdk/csharp/tools/generate_models.py` roots, generated models, enum parsing, cancellation support, and typed limit/provider/billing errors.
- JavaScript: update `mymorningnews-sdk.js` and `index.d.ts` together, including nullable/unlimited representations and request/response shape examples.
- Python: add equivalent resources, models where applicable, optional arguments, error fields, pagination, and harness cases.
- All SDKs cover catalog subscription lifecycle, personal providers, context settings, effective limits, billing verification/status, and administrative overrides/publication where authorized. Browser/mobile store SDK code remains in its native integration layer; REST SDKs do not collect card details.
- MCP adds catalog discovery/subscription, effective usage, and context settings to existing topic/read flows. Respect caller permissions and avoid exposing secrets or purchase tokens in tool output. Customer-initiated purchase flows return a proper UI action rather than making financial commitments through research tools.
- Update MCP resources/prompts, tool schemas, auth/permission tests, `MCP_API.md`, SDK READMEs, and examples. Unknown new fields/enums should have documented compatibility behavior.

### 9.3 OpenAPI and Postman

Preserve the current OpenAPI/route-metadata to REST/Postman generation workflow; do not hand-edit only the generated collection or introduce an unrelated collection as a second source of truth. Update `src/Tools/apidocs/supplement.json`, generator captures/examples, OpenAPI schema factory, and route metadata as needed.

Add repeatable Postman folders/scenarios for Free signup/catalog limits; Paid 5 and Paid 20 usage boundaries; personal BYOK connections; system admin publishing; tenant isolation; overrides; history modes; billing lifecycle; and legacy-client behavior. Include positive and negative assertions, state capture, idempotency replay, pagination, cleanup and environment-specific base URLs. Use separate deterministic integration fixtures for concurrency and worker races that a sequential collection cannot prove.

Generate `assets/postman/MyMorningNews.postman_collection.json` and `REST_API.md`, and run their existing `--check` gates. Execute the collection in CI against an isolated seeded server with provider stubs; add a dedicated runner compatible with the committed collection version. Use masked/local secrets and sanitized signed webhook fixtures. Public callback testing during implementation must verify actual deliveries and replay, not merely successful outbound requests.

Planning investigation used the repository contracts and official Stripe/store documentation. The installed Postman CLI does not support `postman status`. Orbit's attempted Stripe discovery returned third-party callback examples rather than suitable authoritative Stripe endpoints, so those results were not reused. No connected Postman workspace or Context Graph dependency claim is made.

## 10. Dashboard implementation

1. **Onboarding:** Hosted Free opens catalog selection, with a 0/5 counter and optional delivery setup. Paid returns from verified checkout to custom-topic creation. BYOK connects and validates a personal model, then offers speech setup. Self-hosted keeps operator setup and clearly identifies the server. Free catalog reading works without a personal model.
2. **Primary navigation:** Provide Today, My briefings, Discover, Listen, and Account entry points using existing navigation hubs. Preserve old URLs/deep links. Distinguish member capabilities from operator menus; do not expose unusable setup/admin flows to free members.
3. **My briefings:** Separate Custom and Subscribed views with counts, useful status, cadence and last issue. Free defaults to Subscribed. Custom creation, archive/restore and upgrade actions reflect server capabilities.
4. **Discover:** Searchable/filterable catalog, clear Free/Paid eligibility, Subscribe/Unsubscribe, preview, cadence, and audio availability. At five subscriptions, offer management of current choices without losing browsing position.
5. **Topic creation/settings:** Add comparison options near the research scope, not hidden in advanced prompts. Include baseline preview and first-run explanation. System topics show a read-only subscriber view with delivery controls rather than owner editing tabs.
6. **Reader/Today/Listen:** Show System or Custom origin and comparison baseline, private progress, consistent shared audio, and text fallback when custom audio is capped. Existing evidence and accessibility behavior remains available.
7. **Account > Plan and usage:** Show package, price/interval, renewal/cancel date, purchase channel, custom topics, system subscriptions, daily audio used/reserved/remaining, reset time, and override explanation when relevant. Preserve promo redemption without presenting it as a trial.
8. **Account > AI connections:** Personal credentials, validation status, funding explanation, provider/task selection, speech connection, rotation/deletion, repair actions. Show masked values only and preserve form drafts on failures.
9. **Billing:** Web plan comparison, monthly/annual choice, verified checkout return, portal management, cross-channel management guidance, pending payment and failed renewal recovery.
10. **Admin:** Extend Plans and Users with typed default/override editors and impact previews. Add System catalog editing/publishing, subscriber and operational metrics, billing reconciliation diagnostics and support actions. Distinguish global from tenant administration visibly and server-side.

Update API adapters, auth/capability context, route guards, navigation, shared components, localization catalogs, loading/empty/error states, and component tests together. Never use a hard-coded plan name as the authorization source.

## 11. Mobile implementation

Deliver every member-facing workflow on iOS and Android, including tablets: catalog discovery/subscription, custom-topic limits, context setup/editing, personal AI/speech connections, usage, native purchases, restoration, cancellation guidance, and downgrade resolution.

- Extend `mobile/src/api`, auth/setup gates, topic wizard/screens, Today/Listen/reader, Account and notification deep links. Refresh effective entitlements after purchase, restore, resume, server switch, and relevant server responses.
- Add Discover within the existing tab/navigation structure; retain a small number of top-level destinations. Use tablet split views for catalog/detail and briefing browsing.
- Store only application/session credentials in the established secure storage as needed. Send provider secrets once over TLS; do not persist their plaintext in async storage, logs, form analytics, downloads, or crash reports. Clear credential fields when leaving the flow.
- Namespace cached data by server, tenant, user, publication version and resource. Account/server changes must clear private entitlement and provider state. Self-hosted server selection must not transfer hosted purchases or credentials.
- Support offline reading/listening of already authorized downloads, with last-known entitlement shown as stale where necessary. Subscription changes, purchasing, key verification and new generation require connectivity. Revalidate publication availability on reconnect without claiming remote erasure of downloaded files.
- Handle push/universal links to shared issues, unsubscribed/retired entries, billing return, context baseline, and unavailable media. Preserve playback progress across devices without charging playback minutes.
- Add native purchase modules/build configuration, store product identifiers, account binding, and restore/retry UX. Handle pending approval, interrupted checkout and duplicate callbacks without duplicate entitlement.
- Keep operator-only catalog publishing and plan override administration on the web, consistent with `mobile/parity.json`; record explicit not-applicable reasons. Personal BYOK setup is now a member feature and must not remain excluded with operator model configuration.
- Update all 12 locales, RTL layouts, large text, screen reader labels, account deletion/export, minimum supported capability handling, EAS configuration, store privacy/product disclosures, owner-action lists, and device test instructions.

## 12. Test strategy and acceptance criteria

Use existing `Test.Shared` suites exposed through `Test.Automated` and xUnit, dashboard Vitest, mobile Jest/device checks, SDK harnesses, MCP tests and benchmark infrastructure. Add tests where behavior or isolation is at risk, with deterministic clocks and isolated data. Do not rely only on UI hiding or mocked entitlement helpers.

| Area | Required acceptance cases |
|---|---|
| Free | Subscribe to five eligible topics; sixth denied; duplicate subscribe idempotent; non-free topic and custom creation/refinement/one-time bypass denied; shared audio replay unaffected |
| Paid 5 | Five custom definitions allowed; sixth rejected; system subscriptions do not reduce five; 1,800 daily audio seconds enforced |
| Paid 20 | Twenty custom definitions allowed; twenty-first rejected; 6,000 daily audio seconds enforced |
| Unlimited categories | BYOK/self-hosted exceed paid quantity limits without commercial cap errors; operational controls remain distinct; hosted account cannot self-assign self-hosted mode |
| Overrides | Inherit, zero, finite, unlimited, expiry, clear, revision conflict, audited reason, plan change, and cross-server cache invalidation |
| Concurrency | Two requests for last topic/subscription slot; concurrent last audio reservation; multi-worker restart; expired holds; exactly one debit/publish; no 5,000-row accounting truncation |
| Audio | Fail/cancel/retry/regenerate, real measured duration, oversized script, midnight, upgrade/downgrade, deletion, replay/download/range access, no system-generation charge per subscriber |
| Catalog isolation | Global admin publish succeeds; tenant admin/member denied; forged IDs/scopes denied; direct issue/finding/evidence/media/share/feed/export access checked; private fields never projected |
| Subscriber lifecycle | Subscribe/unsubscribe/reactivate, muted delivery, retirement, emergency retraction, eligibility grace, account deletion, independent progress/feedback, deduplicated fan-out |
| BYOK | Different keys for same-tenant members; rotation/revocation; provider 401/429/5xx/timeouts; no platform fallback; background task identity; speech capability mismatch; SSRF restrictions; no secret output |
| History | Every mode, Off at every model stage, first run, same-URL update, no change, correction, partial run, deleted baseline, cross-user baseline, token truncation, deterministic snapshot/retry, edition behavior |
| Stripe | Four offers/no trials; spoofed return cannot activate; valid/invalid signatures; replay/out-of-order events; renewal/failure/cancel/upgrade/downgrade/refund; portal ownership; recovery after missed events |
| Apple/Google | Purchase/restore, pending/cancel, renewal/grace/hold/expire/refund, account binding conflict, duplicate channel, sandbox separation, wrong app/product, Google acknowledgement and provider reconciliation |
| Promotions | Existing assignments, future periods, expiry/revocation and precedence with paid accounts; no unintended downgrade or quota reset |
| Clients | Same entitlement decisions in dashboard/mobile/SDK/MCP; stale/offline state; actionable structured errors; old DTOs and unknown enums |
| Migration | Fresh install; current production-shaped data; customized defaults; historical usage; old Unlimited/Complimentary; resumable backfill; rollback/recovery |

Security tests must enumerate both read and mutation routes, not just the new endpoints. Include direct database assertions proving no unauthorized write, duplicate debit, secret disclosure, or cross-tenant projection occurred.

Extend benchmarks with one system topic and many subscribers to prove one canonical generation, batched delivery with bounded memory, and no per-subscriber model calls. Measure quota-lock contention, catalog pagination, merged feed performance, and daily ledger aggregation. Add history-quality fixtures with known changes/corrections/unchanged facts; compare evidence quality and novelty against the existing benchmark baseline.

Provider simulation tests must run the application against failing model, speech and billing dependencies. Then run Stripe test-mode and native-store sandbox/device tests for real signature/purchase protocols. Keep live secrets and real charges out of CI. Record clearly which results are simulated, sandbox, device or production smoke checks.

Required existing checks during implementation include:

```text
dotnet build src/MyMorningNews.sln
dotnet run --project src/Test.Automated -f net10.0 -- --provider postgresql
dotnet test src/Test.Xunit3
dashboard: npm run lint && npm test && npm run i18n:verify && npm run build
mobile: npm run verify && npm run parity:strict
./sdk/csharp/run-tests.sh
./sdk/js/run-tests.sh
./sdk/python/run-tests.sh
python3 src/Tools/apidocs/generate_rest_api.py --check
python3 src/Tools/apidocs/generate_postman.py --check
python3 src/Tools/mcpdocs/generate_mcp_api.py --check
```

Also retain the existing net8.0/net10.0 CI coverage, benchmark gate, add executable Postman scenarios, and verify native purchases on signed iOS/Android builds. Baseline generator checks require the documented server/database setup. No application tests are claimed as run for this planning-only change.

## 13. Deployment, operations, documentation, and migration policy

### 13.1 Deployment and self-hosting

Update base, hosted, ELB and production Compose variants, `.env.example`, settings metadata/UI, startup validation, health checks and factory/demo seeds. New configuration covers deployment mode, plan revisions/defaults, catalog owner, enabled purchase channels, provider product IDs, webhook verification, reservation leases, context bounds and operational budgets.

Self-hosted clone/build and published-image paths must both work. The base Compose file currently uses published images, so document exact local image build/override commands for a source clone. Self-hosted defaults to unlimited quantities and billing disabled, requires no Stripe/store account or license service, and supports local TTS/model choices. Test Linux amd64/arm64 and persistent-volume upgrades. Never publish real secrets in sample settings.

Maintain generated/derived deployment files consistently. Ensure reverse proxies preserve raw callback bodies and route all billing callbacks correctly; protect administration and private network services. Readiness should distinguish public reading, background generation, billing processing, and optional provider health.

### 13.2 Observability and support

Add dashboards/alerts for effective-plan distribution; custom and subscription counts; quota blocks by cause; reservation leaks; audio committed/reserved/provider cost; system generation and subscriber fan-out lag; key failures; payment event/reconciliation lag; purchase verification failures; and baseline-resolution/truncation rates.

Carry account, topic, publication, job, operation and billing event IDs through traces without putting secrets or high-cardinality personal data into metric labels. Keep user-funded spend and platform-funded spend distinct. Support tools can explain effective limits, replay/reconcile a verified billing event, release a provably abandoned reservation, and correct usage through an audited adjustment.

Add runbooks for payment-provider outage, missing notifications, invalid BYOK key, unavailable local TTS, stuck audio reservation, leaked publication, catalog generation failure, user downgrade and account-link conflict. Include backup/restore drills for encrypted keys, ledger and billing event state.

### 13.3 Existing-account migration

Produce an admin-readable dry-run report before enabling enforcement: account/category mapping, custom counts, future promo periods, overrides, credentials, proposed pauses, and daily usage estimates.

- Self-hosted `unlimited` maps to the self-hosted default without commercial caps. Preserve the legacy key as a compatibility alias while assignments are migrated.
- Hosted Free with existing private topics retains content and receives a clearly announced transition window. At its end, pause custom generation unless the user upgrades or selects verified BYOK. Do not publish private material to the catalog.
- Existing Complimentary/custom plans and active promo assignments retain their promised terms until a deliberate migration/expiry rule applies. Do not silently map every complimentary grant to a paid billable subscription.
- Hosted legacy Unlimited is reviewed as an administrative/custom grant, never interpreted as proof the account is self-hosted.
- Backfill generation usage from trustworthy ready media with timestamps and duration; record uncertainty if older deleted media makes the current day incomplete. Start enforcing new daily accounting at a disclosed boundary if accurate backfill is impossible. Never invent historical charges.
- Validate historical context backfill and baseline availability. Preserve old issue URLs and existing authenticated share behavior unless publication/eligibility explicitly changes.

### 13.4 Documentation and external surfaces

Update README, `docs/HOSTING.md`, `DOCKERHUB_README.md`, REST/MCP references, SDK READMEs, Postman examples, mobile README, store submission documents, owner actions, device-testing guide, localization guide, CHANGELOG, factory/demo accounts, user-testing scripts, support FAQs and privacy/account-deletion copy.

The marketing website is a separate repository at `~/Websites/mymorningnews.ai`. Include a coordinated pricing/features/FAQ/legal-link change there, using that repository's own instructions when implementation begins. All pricing tables must agree on 5/20 custom topics, separate system subscriptions, 30/100 generated minutes per day, no trial, and the actual BYOK cost responsibilities. Keep copyright notices and the existing version until an explicitly approved version change. Product screenshots for public documentation wait for owner review as required by repository guidance.

## 14. Delivery sequence and release gates

| Phase | Work package | Dependency and completion gate |
|---|---|---|
| 0 | Contract and design baseline | Freeze terminology, tier matrix, defaults, transitions, API schemas, screen flows and migration report format; inventory all entitlement entry points |
| 1 | Schema and entitlement foundation | Add typed registry, per-user overrides, explicit deployment mode, durable ledger/reservations, revisions, additive migrations and compatibility DTOs; pass concurrent boundary and migration tests |
| 2 | Catalog and subscription domain | System owner/publication projection, subscriber state, safe reader/media access, outbox and retirement rules; pass cross-tenant and fan-out tests |
| 3 | Context and BYOK services | Snapshot resolver and pipeline changes, personal provider/speech routing, setup-gate split; pass Off/On, snapshot, key-isolation and no-fallback tests |
| 4 | Stripe and native billing backend | Product mapping, purchase bindings, verified callbacks, lifecycle/reconciliation, transition handling; pass simulated and provider-sandbox flows |
| 5 | Dashboard workflows | Member catalog/history/BYOK/billing/usage plus admin catalog/overrides; complete component/integration/localization tests |
| 6 | Mobile workflows and purchases | Full member parity, native purchase/restore, offline/deep-link behavior; verify signed phone/tablet builds |
| 7 | Contract consumers and evidence | Complete all SDK/MCP surfaces, generated docs/Postman, CI scenarios, benchmarks, deployment examples and operator runbooks |
| 8 | Staged rollout | Migration rehearsal, shadow evaluation, canary accounts, provider callbacks, catalog seed and multi-day quota/reset observation |
| 9 | Final UX/styling review | Execute Section 15 in full, repair findings, rerun affected checks, obtain product review of finished screens, then release |

These phases describe dependencies, not permission to defer SDKs/tests/documentation until the end. Each backend work package includes its contract, SDK/MCP implications, tests and migration; phases 5 through 7 finish the integrated consumer experience. Avoid calendar estimates until team capacity and provider/store setup are known. Billing, cross-tenant projection and concurrency-safe accounting are the principal schedule risks.

Roll out additive schema and compatibility readers first; seed reviewed catalog content before onboarding Free users. Run entitlement evaluation in shadow mode to compare intended outcomes with existing use, but never shadow or weaken authorization. Then enable staff/test accounts, a canary cohort, and general access. Keep independent feature controls for catalog, personal providers, context, purchases and enforcement, with coherent client-config advertisement.

Rollback disables new purchase initiation and affected generation, while retaining verified callbacks, billing reconciliation, read access where authorized, and usage records. Do not roll back to a binary that ignores newly required authorization or loses billing events. Prefer a forward fix or a known-compatible release; restore a backup only with an explicit reconciliation plan for external payments and generated artifacts.

Release acceptance requires demonstrated Free five-subscription behavior, both paid packages and intervals on every purchase channel, unlimited BYOK/self-hosted quantities, user override parity, no credential fallback/leak, correct daily audio accounting, historical comparison correctness, system generation reuse, recoverable migration, and the UX gate below.

## 15. Final full UX and styling review

This is a mandatory final work package after the integrated features exist, with early design checkpoints during implementation. The original design review was based on source structure. The rendered review completed during implementation is recorded in Section 15.6; native-device and store-purchase checks remain explicit outstanding gates. Observed foundations include dashboard light/dark tokens in `dashboard/src/index.css`, matching mobile palette intent in `mobile/src/theme/palette.js`, grouped topic workspace tabs, and existing member/operator navigation metadata. The dashboard's 32px default controls and 34px navigation rows require explicit touch-target review; token presence alone does not establish usability or contrast.

### 15.1 Information architecture and task efficiency

Review the entire dashboard/mobile navigation for each role and tier, not only newly added pages. Free users should reach reading and subscriptions immediately. Paid/BYOK users should find custom creation, provider health and remaining audio without opening administrator screens. Operators should find publication health and account overrides without navigating consumer billing flows.

Use one clear primary action per screen. Keep advanced model/task/source/prompt controls progressively disclosed. Preserve old deep links while grouping related settings. Show custom-topic and catalog-subscription counts separately everywhere. Do not show an “unlimited” progress bar or a fake zero cost for user-funded/unpriced calls.

Target flows to validate with representative users:

| Persona / task | Target experience |
|---|---|
| New Free user | Discover eligible briefings, subscribe, and read/play a preview without encountering a model setup requirement |
| Free user at five | Understand the cap, replace a subscription, and retain reading position without deleting history |
| Paid subscriber | Compare 5/20-topic packages and monthly/annual pricing, buy once, and see verified access on another device |
| Returning mobile buyer | Restore the existing purchase and handle wrong-account binding without buying again |
| BYOK member | Connect and test a personal model, understand speech requirements, create a topic, repair an expired key |
| Topic owner | Choose standalone or comparison mode, preview the baseline, and recognize new/changed/unchanged results |
| User at audio cap | Understand used/reserved/remaining, exact reset, text availability and shorter-audio options |
| User downgrading | Select retained topics/subscriptions and see what pauses and when; retain access to personal history |
| System administrator | Draft, preview, publish and mark a free-eligible topic; inspect subscriber impact before retirement |
| Support administrator | Explain a user's default/override/usage and correct a verified problem with an audited action |

Record task completion, hesitation, misclicks, misunderstood labels and unnecessary steps. As initial targets, a returning user should subscribe from Discover in two purposeful actions, reach context settings from a topic in two navigation actions, and understand a quota failure without opening support documentation. Revise layouts where observation shows friction.

### 15.2 Layout and visual hierarchy

- Retain the existing blue, sunrise-gold and neutral brand palette. Use restrained semantic color; reserve emphasis for the primary action, current state and actionable errors.
- Align title, description, toolbar, filters and content to a consistent grid. Normalize spacing, border, radius, field height and icon treatment through existing design tokens. Avoid one-off CSS values for each new feature.
- Make catalog cards readable and comparable: title, short scope, cadence/language, latest issue, audio indicator, eligibility, subscription action. Keep cards uniform without forcing large empty areas for short descriptions.
- Keep pricing comparable in a two-package table/card layout. Show the actual annual charge, monthly equivalent if used, no-trial wording and daily audio allowance. Do not hide the billed total or rely on tiny footnotes for limits.
- Use a compact Account summary followed by detailed limits and provider/billing management. Administrative override tables show inherited/effective values and provenance in distinct columns rather than raw feature-key dumps.
- Keep the briefing reader focused: comfortable line length, clear headings/citations, compact comparison callout, stable audio controls and an accessible evidence panel. Changes should be recognizable without overwhelming the article with badges.
- Keep the mobile mini-player clear of bottom navigation, keyboards, safe areas and sticky actions. Verify tablet split views and portrait/landscape transitions.

### 15.3 Responsive and accessibility review

Review narrow phones around 320/390 CSS pixels, larger phones, tablets around 768/1024, and desktop around 1280/1440, in both themes. Verify long topic names, large counts, all supported scripts and Arabic RTL. Horizontal scrolling must be intentional and limited to data surfaces that require it.

Use [WCAG 2.2 AA](https://www.w3.org/TR/WCAG22/) as the web acceptance target and native accessibility conventions on mobile. Measure text/icon/control contrast in actual states; test keyboard-only operation, focus order and visibility, skip links, dialog trapping/return focus, screen-reader names, heading hierarchy, validation announcements, and non-color status indicators. Increase or pad interactive targets for reliable touch use; aim for at least 44px on touch-oriented web/iOS interfaces and 48dp on Android. Verify 200% zoom, large/dynamic text, reduced motion, and no clipped labels or hidden billing disclosures.

### 15.4 State and content consistency

Inspect loading, empty, success, failure, stale, offline, pending purchase, pending verification, exhausted quota, missing baseline, failed provider, retired publication and access-denied states. Use inline explanations near the affected action, preserve valid form input, offer a clear next step, and avoid repeated blocking dialogs or simultaneous duplicate toasts.

Standardize language across dashboard, mobile, emails, push, feeds, SDK examples and pricing pages: “custom briefing,” “catalog subscription,” “generated audio,” “compare with past briefings,” and “resets at.” Avoid ambiguous “30 minutes included” or “5 briefings” without the period/type. Format durations, currency, dates and plural forms per locale. Never present internal feature keys, provider stack traces or billing event IDs as the primary user explanation.

### 15.5 Review execution and completion evidence

1. Prepare deterministic fixtures for every tier, admin/member role, quota boundary, long/empty content, billing state and provider state.
2. Walk every journey above in dashboard and signed iOS/Android phone/tablet builds, with light/dark, keyboard/screen reader and RTL/large-text variants.
3. Capture internal review images and annotated findings, keeping provider secrets and personal data out of artifacts. Public README/marketing screenshots wait for owner review.
4. Track each finding with screen/state, severity, expected behavior, reproduction, owner and fix evidence. Prioritize blocked tasks, wrong charges/limits, inaccessible actions, confusing navigation and visual inconsistency.
5. Repair findings, rerun affected functional/accessibility tests, and repeat representative task walkthroughs. Run visual regression checks on stable layouts, while using human inspection for readability and information hierarchy.
6. Deliver a final review report with before/after evidence and explicit pass/fail for every journey. Release only when no blocking/high-severity usability or accessibility defects remain, both themes and all target layouts are coherent, member parity is verified, and product review accepts the finished experience.

### 15.6 Implemented UX review, November 24, 2026

The delivered dashboard uses separate System briefings, Plans and billing, and Your API keys destinations. Catalog-only users can browse and subscribe without model setup; custom creation is disabled with a route to the catalog or paid plans. Administrator publication controls and per-user allowance tables remain separate from member billing.

Internal rendered checks covered catalog, billing, private providers, the new-publication form and account usage at 390px and 1440px, both light/dark (20 page combinations), followed by catalog/billing/providers in Arabic RTL at 320px, 768px and 1024px in both themes (18 more combinations). The final automated accessibility checks reported zero WCAG 2 A/AA and 2.1 AA violations in those views, and no document-level horizontal overflow. These automated checks do not establish full WCAG conformance or replace screen-reader/device testing.

Review fixes: increased disabled-navigation contrast; corrected light success/warning/danger/info badges and dark accent badges; aligned billing/provider page spacing; kept daily generated/reserved audio and system-subscription counts separate; added catalog email/push controls; made suspended subscriptions removable; and added account-deletion renewal disclosures. The marketing pricing/terms pages passed eight phone/desktop/theme accessibility checks after fixing forecast-label contrast and keyboard access to the horizontally scrollable Docker example. No new public product screenshots were published.

Functional evidence includes 652 dashboard tests, 514 mobile tests (six existing skips), provider purchase-state tests, catalog isolation/retirement/retraction/delivery tests, quota concurrency tests, and live SDK/Postman contract checks. A production canary confirmed catalog-only onboarding, custom-topic refusal, correct prices, and unpaid-checkout cleanup.

**Still required for the complete design acceptance gate:** signed iOS/Android device purchase/restore and wrong-account tests; phone/tablet dynamic text and screen-reader checks; end-to-end native signup discounts; baseline-preview/provenance UI; and the broader edition-specific comparison workflow. The mobile Simulator could not be selected by the enabled computer-use surface, so no native visual pass is claimed. These open items are recorded in the release status; the web/server rollout does not imply that native purchasing is publicly released.

(c) 2026 Lattice AI LLC
