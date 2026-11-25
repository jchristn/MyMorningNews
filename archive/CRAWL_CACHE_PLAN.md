# Temporary crawl cache implementation plan

Status: implemented and archived November 24, 2026. Original plan prepared against `b65735c`; the proposed design below is retained as the implementation record. Current setup and operating instructions are in [the crawl cache guide](../docs/CRAWL_CACHE.md).

Implementation notes:

- Migration 39 adds entries, immutable generations, and permanent namespace-to-storage bindings. Redirected responses bypass cache admission because CrawlSharp does not expose intermediate cookie/cache headers, so a redirect-hop table is not needed for this release.
- Blobject 6.1.3 disk and S3 adapters, system settings/environment overrides, cleanup, bounded capacity, storage integrity checks, and optional forced retrieval are implemented. Object-operation concurrency is configurable and requires restart.
- Pipeline, agentic search, administrative diagnostics, dashboard controls, all three SDKs, REST/Postman contracts, Docker examples, and regression tests are updated. Existing source/evidence text remains independently retained.
- Validation passed: full .NET 8 backend suite (293 tests); full .NET 10 suite before the final pipeline regression was added (292 tests), followed by all 10 final crawl-cache cases on .NET 10, including real local MinIO; zero-warning builds for both frameworks; dashboard tests/build/lint/localization, the added nested-settings UI regression, mobile tests (526 passed, six existing skips), and SDK wire/collection-contract checks.
- No production deployment is included. Cache defaults remain disabled. The deployment guide retains the AWS S3 bucket/credential smoke test as an account-specific rollout step.

Store crawl URLs, freshness, and object references in PostgreSQL. Store downloaded content and converted Markdown through Blobject, using either disk or S3 selected in the system settings JSON. A fresh, eligible cache hit supplies the existing fetch result without downloading the page or converting it again. Expired entries trigger a new crawl and are removed by a background worker.

## Existing behavior and integration points

`FetchStage.ReadOrFetchAsync` currently reuses a tenant's `SourceItem.ContentText` for seven days. That shortcut runs before `FetchService`, so it bypasses the current blocked-domain and robots checks. It also does not benefit agentic search or the fetch diagnostic endpoint.

Put the new cache in `FetchService.FetchAsync`, which is called by the pipeline fetch stage, `AgenticSearch`'s `fetch_url` tool, and `SearchRoutes`' fetch diagnostic. Remove the seven-day shortcut from `FetchStage`; all three callers must use the same freshness and eligibility rules.

Keep the existing artifact storage service and its retention policy independent. Generated briefings, audio, and evidence have a different lifetime from disposable crawl content. Existing tenant-owned source records and their extracted text remain available for evidence and source-detail views; they cease to act as a crawl cache. Moving historical source text out of PostgreSQL is a separate data migration, not required for this cache.

## Proposed behavior

- Enable the cache explicitly during rollout. Default freshness is 24 hours from the successful origin fetch, configurable in seconds. Reads never extend that deadline.
- Share eligible, anonymous public-page responses across topics, runs, and tenants within one deployment. Preserve caller-specific domain restrictions on every lookup. Never expose cache metadata or another tenant's activity to ordinary users.
- Cache only successful, supported, nonempty content. Do not cache failures, authentication responses, or requests with credentials, cookies, user information in the URL, or signed/token-bearing query parameters. Treat `Cache-Control: private`, `no-store`, and `no-cache`, `Set-Cookie`, and unsupported `Vary` dimensions as ineligible in the first implementation. Reject cookie-bearing redirect chains as well.
- Treat configured freshness as an upper bound. Honor a shorter origin freshness lifetime using `s-maxage`/`max-age`, `Date`, `Age`, and `Expires`; already-stale responses are ineligible. Extract these headers from CrawlSharp's response metadata. Conditional revalidation can be added later; initially expired entries receive a normal fetch.
- Recheck current domain, egress, and robots policy before returning cached content, including recorded redirect destinations. A cache hit avoids the page download and conversion; a robots policy refresh or DNS check may still perform network I/O.
- Serve no stale content when refresh fails. Preserve the current fetch failure/partial-success behavior.
- On cache database or object-store failure, perform a normal crawl within existing timeouts. A successful crawl remains successful if its cache write fails. Preserve caller cancellation rather than treating it as a cache miss.
- Keep `FetchDispositionEnum.Fetched` for successful hits. Add an additive cache-status field for diagnostics; preserve the original `FetchedUtc`, content hash, title, final URL, and byte count. `DurationMs` measures this call, including cache lookup. Update `FetchStage` to copy the original fetch timestamp instead of assigning the current time on a hit.

## Optional forced retrieval

Add a per-request `forceRefresh` boolean, defaulting to `false`, to the internal fetch options and the fetch diagnostic request (`FetchTestRequest.ForceRefresh` in C#, serialized as `forceRefresh`). Existing callers that omit it retain normal cache behavior. This is a request option, not a global system setting.

When `forceRefresh` is `true`:

- Skip cached response bodies and perform a new origin retrieval and conversion, even if a fresh cache entry exists. Do not satisfy the request from an existing in-flight fetch or from a generation published while waiting for a lease.
- Continue enforcing domain restrictions, egress checks, robots rules, per-domain delays, timeouts, cancellation, and content-size limits. The flag bypasses only content-cache reuse.
- After a successful, cache-eligible retrieval, publish a new generation through the normal lease, fencing, and capacity checks. Its freshness starts at the new origin fetch time; superseded content is queued for cleanup. If caching is disabled, the response is ineligible, or publication cannot complete, return the newly fetched result without claiming the cache was updated.
- On origin retrieval or conversion failure, return the normal failure result. Never fall back to a cached response, even if the previous entry is still fresh. A transient failure does not extend or delete the previous generation; ordinary callers may continue using it while it remains eligible and fresh.
- Record the forced retrieval separately from a cache hit, and expose whether cache publication succeeded through the planned diagnostic metadata.

Forced retrieval uses the same bounded lease wait as other fills, but always performs its own origin request afterward. If it cannot acquire a publishing lease, it may retrieve without publishing; it must neither steal a live lease nor overwrite a newer generation without the current fencing token. Normal callers can continue using a fresh active generation during refresh.

## URL identity and isolation

Introduce a dedicated `CrawlCacheKey` helper. Do not use `UrlCanonicalizer.Hash`: its existing deduplication identity merges HTTP with HTTPS and removes `www.`, while its canonicalizer also changes query order and trailing slashes. Those transformations can identify different origin responses as equivalent.

For the crawl key, normalize scheme/host casing and default ports, omit fragments, and preserve path casing, trailing slashes, query order, repeated parameters, and query values. Do not strip tracking parameters in the first release. Hash the normalized URL together with a versioned fetch profile containing the user agent, supported representation headers, and conversion version. Compare the stored normalized URL/profile after hash lookup as a collision guard.

Key by requested URL; store the final URL and ordered redirect hops separately. Do not create aliases from a final URL automatically. A direct request to that URL can have different request context. Future authenticated crawling must use an isolated tenant/user/credential scope or bypass this cache.

Include a configured storage namespace in keys and database uniqueness constraints. All replicas using the same S3 bucket/prefix or genuinely shared disk volume use the same namespace. Replicas with separate disks must use different namespaces. Changing the provider/location requires a new namespace and retirement of the old namespace after cleanup.

## System settings JSON

Add `ServerSettings.CrawlCache` and typed settings classes with the existing `[Setting]` metadata, validation, environment overrides, secret masking, and restart indicators. The proposed section below belongs in `mymorningnews.json`; `Enabled` defaults to `false` until deployment storage is configured.

```json
{
  "CrawlCache": {
    "Enabled": false,
    "StorageNamespace": "crawl-disk-1",
    "FreshnessSeconds": 86400,
    "MaxEntryBytes": 8388608,
    "MaxTotalBytes": 10737418240,
    "MaxEntries": 100000,
    "CleanupIntervalSeconds": 300,
    "CleanupBatchSize": 100,
    "OperationTimeoutMs": 5000,
    "LeaseSeconds": 180,
    "WaitForFillMs": 5000,
    "Storage": {
      "Provider": "Disk",
      "Directory": "./crawl-cache",
      "S3Endpoint": "",
      "S3Region": "us-west-1",
      "S3Bucket": "",
      "S3Prefix": "mymorningnews/crawl-cache/",
      "S3AccessKey": "",
      "S3SecretKey": ""
    }
  }
}
```

For S3, select `Provider: "S3"`, use a new namespace such as `crawl-s3-1`, set bucket/region/prefix, and supply credentials through secret environment overrides. An empty endpoint selects AWS; a custom endpoint selects compatible storage. Cache objects must remain private. Disk uses a dedicated persistent container volume with server write permissions, outside any directory served by the dashboard or static-file routes.

Proposed validation: freshness 60 seconds to 7 days; per-entry limit 64 KiB to 100 MiB; total byte budget at least one entry; entry count 1 to 1,000,000; cleanup interval 30 to 3,600 seconds; batch size 1 to 1,000; operation timeout 100 to 30,000 ms; lease 30 to 600 seconds; fill wait 0 to 30,000 ms. Validate provider-specific required fields, paired credentials, safe namespace/prefix, and bounded concurrent storage operations. Keep origin download limits governed by `Search.MaxFetchBytes`; an otherwise valid oversized cache entry is returned to the caller without being cached.

Provider, namespace, directory, connection, and credential changes require restart. Freshness and capacity settings apply live; effective expiry is the earlier of stored expiry and original fetch time plus the current freshness setting. Disabling the cache bypasses reads/writes while cleanup continues for the configured namespace. Environment overrides take precedence and are not written back as secrets.

The generic dashboard settings editor should surface this group, including descriptions and secret fields. Verify its nested-group rendering and SDK settings serialization rather than assuming they update automatically.

## Blobject storage adapter

Add matching pinned NuGet references to `Blobject.Core`, `Blobject.Disk`, and `Blobject.AmazonS3`. The local Blobject source currently declares 6.1.3 and targets both application frameworks; verify published package availability and dependency compatibility before pinning. Blobject documents separate disk and S3 providers and common storage operations in its [upstream repository](https://github.com/jchristn/Blobject).

Implement `ICrawlContentStore` with a Blobject adapter using `DiskBlobClient(new DiskSettings(...))` or `AmazonS3BlobClient(new AwsSettings(...))`. Use `WriteAsync`, `GetStreamAsync`, and `DeleteAsync` with cancellation and bounded reads. Keep clients long-lived, and dispose clients and returned streams at the proper ownership boundary.

Important dependency detail: the inspected `AmazonS3BlobClient` constructor uses anonymous credentials when access/secret keys are absent. It does not inherit the default AWS credential chain used by the existing artifact store. The initial S3 configuration must require an explicit key pair. If IAM role/session credentials are required, add and verify that capability in Blobject first; do not silently fall back to anonymous access or claim role support.

Store each generation under opaque, immutable keys such as `<prefix>/<namespace>/<entry-id>/<generation-id>/raw` and `content.md`. Raw bytes preserve the downloaded content; Markdown avoids repeating conversion. Store their separate checksums and lengths in PostgreSQL. Never use URLs as filesystem paths, public links, or metric labels. Enforce the combined entry-size limit while reading/writing and do not overwrite a published generation in place.

## PostgreSQL model and publication protocol

Add the next available migration using the existing `DatabaseDriverBase`, interfaces, PostgreSQL implementations, and query-class structure. Use typed columns with lowercase names and IDs from `IdGenerator`; no catch-all JSON columns and no response bodies in cache tables.

| Table | Purpose and principal fields |
|---|---|
| `crawlcacheentries` | Unique `(storagenamespace, cachekey)`; normalized requested URL, profile version/hash, active generation ID, lease owner/token/deadline, created/access times |
| `crawlcachegenerations` | Entry ID, state (`Writing`, `Ready`, `Deleting`), final URL, title, HTTP status, content type, original fetch time, expiry, raw/Markdown object keys, separate hashes and lengths, reserved bytes, cleanup retry time |
| `crawlcacheredirects` | Generation ID, hop order, source/target URL and status for policy checks |

Index lookup identity, expiry/state/ID for bounded cleanup, and lease expiry for recovery. Retain the existing tenant-owned source-item relationships; cache deletion must never cascade into sources, findings, evidence, or briefings.

1. Check eligibility and current policy. Unless `forceRefresh` is true, read the active generation by indexed identity. Accept it only when fresh, profile-compatible, within current limits, and both referenced objects can be verified. Load only the Markdown object for normal hits; validate its length/hash and required metadata. A missing raw object is detected during integrity reconciliation and invalidates the generation.
2. On a miss or forced retrieval, atomically acquire a short database lease for that key, with a monotonically changing fencing token. A local per-key task can reduce database contention for ordinary requests; the database lease coordinates separate server instances. Do not hold a transaction open across network calls.
3. A competing caller waits a bounded time and rechecks. Ordinary callers may reuse the newly published result; forced callers must perform their own origin retrieval. If the lease still exists after its wait budget, a caller may fetch independently without publishing. Thus normal overlapping requests share one fetch, while a stalled owner cannot indefinitely block research. Cancellation by one waiter must not cancel another caller's work.
4. The owner fetches and converts through the current crawl path. Recheck response eligibility. Reserve a generation and capacity in a short transaction before writing objects. Reservation accounting includes active, writing, and pending-deletion generations. If the budget is exhausted, return the fetched result without caching and request cleanup.
5. Write raw and Markdown objects with unique generation keys. Only after both writes finish, atomically mark the generation ready and switch the active pointer, conditional on the lease fencing token still being current. A late owner cannot overwrite a newer result. Renew leases during long operations.
6. Queue the superseded generation for deletion. A failed write, lost lease, or process crash leaves a recorded generation for retryable cleanup. Return a fresh result to the caller even if publication fails. Verify persisted content on subsequent reads, and invalidate corrupt/missing content before refetching.

Keep bounded per-key coordination state and release completed entries. Test cleanup racing a reader: a disappearing object becomes a safe miss/refetch, not a broken briefing. Do not reuse any object key across generations.

## Retention and capacity

Add a crawl-cache cleanup worker alongside existing workers. Claim bounded batches with database coordination so multiple replicas cannot race destructive work. Use keyset traversal internally, not an unbounded materialized list.

- Mark expired generations, abandoned writes after their lease expires, and superseded generations as deleting. Detach active pointers transactionally before deletion; fresh readers must not discover a deleting generation.
- Delete only the exact recorded raw/Markdown keys in the configured namespace. Treat missing objects as already deleted. Delete generation/redirect rows and release reserved capacity only after both objects are removed; preserve failed deletions with retry/backoff.
- Remove empty entry/lease rows once no generation or live lease remains. Count metadata and pending deletion toward limits to prevent failed deletes or unique URL traffic from growing the database indefinitely.
- Under pressure, remove expired data first, then least-recently-used fresh generations. Throttle access-time updates to avoid a database write on every hit. Reserve capacity transactionally so concurrent fills cannot overrun the configured admission budget.
- Schedule bounded reconciliation of old `Writing` generations and ready-object integrity. Avoid a full bucket listing in ordinary request or cleanup paths. Record generation keys before uploads so crashes leave known cleanup candidates. Document recovery for objects orphaned by a database restore, and optional S3 lifecycle expiry for the dedicated prefix as a secondary backstop.
- Account for S3 versioning, noncurrent versions, and incomplete multipart uploads in operational lifecycle rules; deleting the current key alone may not reclaim physical storage. Use disk quotas/storage alarms as physical backstops. Logical byte limits do not include provider overhead or unrelated objects.

Storage migration is an operational step: disable fills, drain/purge the old namespace while its credentials remain configured, then switch namespace/provider and restart. Never delete old-store objects using newly configured credentials or paths.

## Contracts and observability

No new public cache listing endpoint is needed. Add cache provenance to `FetchResult` and update its REST/OpenAPI examples, Postman response examples, C# model, JavaScript/Python documentation, and diagnostic UI where used. Any future cache collection route must accept `EnumerationQuery` and return `EnumerationResult`, with the existing bounded GET and POST `/enumerate` conventions. Never expose Blobject enumeration directly as a REST array.

Document the optional `forceRefresh` request field in REST/OpenAPI and Postman, propagate it through the C#/JavaScript/Python fetch diagnostic clients, and add a default-off "Force fresh retrieval" control to the dashboard fetch diagnostic. Preserve the endpoint's existing authorization. Internal pipeline and agentic-search fetch callers can explicitly set the option; scheduled calls default to false. Validate non-boolean API values using the existing request-validation conventions.

Record hit/miss/expired/bypass/error counters, avoided page downloads, lookup/write latency, stored/reserved bytes, generation counts, fill contention, eviction counts, and cleanup failures. Use fixed low-cardinality outcome/provider labels. Keep full URLs, credentials, content, and tenant activity out of metrics. Distinguish retrieved body bytes from actual network bytes on a hit.

## Implementation sequence and acceptance tests

1. **Settings and Blobject adapter:** package compatibility, validated JSON/environment fields, secret masking, disk/S3 contracts, persistent disk volume, private S3 configuration. Verify read/write/delete and cancellation against temporary disk and a local S3-compatible service, plus a dedicated AWS S3 test prefix before enabling AWS production storage.
2. **Database and cleanup:** migration/rehearsal, typed models, indexed lookups, leases/fencing, generation reservations and deletion state machine. Test restart recovery, concurrent capacity reservations, missing objects, failed deletes, and storage namespace isolation.
3. **Fetch integration:** wrap `FetchService`, remove the seven-day source shortcut, add optional `forceRefresh` handling, update original timestamp handling, and exercise pipeline, agentic search, and diagnostic callers. Preserve existing source/evidence content and normal failure behavior.
4. **Contract and operational documentation:** settings editor, additive fetch metadata, SDK/OpenAPI/Postman synchronization, telemetry, compose examples, hosting/runbook instructions, and cache-disable rollback.
5. **Validation and staged enablement:** run the tests below, build .NET 8 and 10 with zero warnings/errors, and run the full backend and relevant SDK/dashboard/mobile contract suites before a separate deployment task. This planning change does not deploy or publish builds.

Required regression cases:

- First fetch downloads/converts once; the next eligible fetch, including from another caller and after process restart, returns equivalent content without another page download or conversion.
- Omitted or false `forceRefresh` uses a fresh cache entry. True performs a new origin request and conversion, returns changed content and a new fetch timestamp, and replaces the cached generation when publication succeeds; the following ordinary call receives that replacement.
- A forced retrieval failure returns failure even with a fresh cached copy available, without extending that copy's lifetime. Forced retrieval still obeys all policy, rate, and size limits, and works when caching is disabled or storage is unavailable.
- Forced callers never receive a preexisting or concurrently published cached response. Concurrent forced/ordinary calls, bounded lease-wait fallback, failed publication, and cancellation preserve fencing and report cache-update status accurately.
- REST/Postman examples and all three SDKs pass `forceRefresh` correctly; the diagnostic UI defaults it off, and malformed non-boolean values are rejected.
- Freshness boundary and shorter origin TTL force refresh. Repeated hits do not renew freshness. Refresh failure never serves expired content. Original `FetchedUtc` survives hits and source-item updates.
- Distinct scheme, host, trailing slash, query ordering/repetition, profile, and namespace do not collide. Fragments share a key. Sensitive or authenticated URLs bypass storage.
- A previously cached page cannot bypass newly applied blocked-domain, egress, robots, or redirect rules. Private/no-store/no-cache responses, cookies, unsupported `Vary`, non-success statuses, empty results, unsupported media, and oversized bodies are not admitted.
- Normal concurrent requests across two service instances produce one published generation. A crashed lease owner recovers; a late owner is fenced out; bounded wait fallback and individual cancellation work as specified.
- Missing/truncated/corrupt Markdown, object-store/database timeouts, failure between the two writes, failure before/after pointer publication, and cleanup/read races safely refetch or preserve the existing fetch error behavior.
- Cleanup removes both objects and metadata, retries deletion failures, and leaves briefing sources/evidence intact. Expired/writing/deleting records, concurrency maps, storage reads, and cleanup batches remain bounded under load.
- Disk and S3 produce the same results. Provider changes cannot delete another namespace. Disabled caching uses the original live fetch path, and credentials never appear in settings responses, logs, generated docs, or source control.
- No retrieval contract returns a bare application array; existing enumeration validation remains green. Any additive diagnostic fields are compatible across dashboard, mobile, SDKs, MCP projections, and generated documentation.

Completion criterion: eligible repeated crawls use the configured Blobject cache until expiry unless explicitly forced to retrieve again, with measured avoidance of page downloads and conversion, bounded database/object storage, safe recovery from partial failures, and unchanged access to existing briefing evidence.

Copyright (c) 2026 Lattice AI LLC.
