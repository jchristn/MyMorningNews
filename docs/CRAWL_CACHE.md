# Temporary crawl cache

The fetch service can reuse anonymous public-page content across research runs, topics, tenants, agentic `fetch_url` calls, and the administrative fetch diagnostic. PostgreSQL migration 39 stores URLs, leases, generations, checksums, and expiry. Blobject 6.1.3 stores raw bytes and converted Markdown on disk or S3. Existing source text and briefing evidence retain their independent lifetime.

Caching defaults to **disabled**. Configure `crawlCache` in `mymorningnews.json`, or use the global administrator's Settings view. Disk is the default provider; the Docker examples mount a dedicated persistent `crawlcache` volume at `/data/crawl-cache`.

```json
{
  "crawlCache": {
    "enabled": true,
    "storageNamespace": "crawl-disk-1",
    "freshnessSeconds": 86400,
    "maxEntryBytes": 8388608,
    "maxTotalBytes": 10737418240,
    "maxEntries": 100000,
    "cleanupIntervalSeconds": 300,
    "cleanupBatchSize": 100,
    "operationTimeoutMs": 5000,
    "leaseSeconds": 180,
    "waitForFillMs": 5000,
    "maxConcurrency": 8,
    "storage": {
      "provider": "Disk",
      "directory": "/data/crawl-cache",
      "s3Endpoint": "",
      "s3Region": "us-west-1",
      "s3Bucket": "",
      "s3Prefix": "mymorningnews/crawl-cache/",
      "s3AccessKey": "",
      "s3SecretKey": ""
    }
  }
}
```

## Freshness and forced retrieval

The default maximum lifetime is 24 hours from the original fetch. Hits preserve that timestamp and do not renew expiry. A shorter origin lifetime takes precedence, including `s-maxage`, `max-age`, `Date`, `Age`, and `Expires`. Lowering the configured lifetime applies to existing entries on their next read. Expired content is never used as a fallback after a failed refresh.

The previous pipeline-only seven-day reuse of `SourceItem.ContentText` is removed. The central fetch service now makes the reuse decision after current domain, network, and robots checks. Fetching robots.txt or resolving DNS may still access the network during a content-cache hit.

`POST /v1.0/api/fetch/test` accepts:

```json
{
  "url": "https://example.com/article",
  "forceRefresh": true
}
```

`forceRefresh` defaults to false. True performs a new retrieval and conversion even when another fill is in progress, then replaces eligible cached content if publication succeeds. Origin failure returns a failure; it never substitutes the previous cached copy. Concurrent ordinary callers can still use a previously fresh copy. Existing endpoint authorization, delays, robots rules, network policy, and size limits apply. In the dashboard, open **Search providers → Fetch a page → Force fresh retrieval**.

The result adds `cacheStatus` (`Hit`, `Miss`, `Forced`, `Bypass`, or `Error`), `cacheUpdated`, and `networkBytes`. `bytes` remains the content size, whereas `networkBytes` is zero for a cache hit. `cacheUpdated` is true only after a generation is published; a successful origin request can return false when content is ineligible, capacity is unavailable, caching is disabled, or storage fails. `durationMs` measures the current call and `fetchedUtc` identifies the original page retrieval.

Callers can use:

- C#: `FetchTestAsync(new FetchTestRequest { Url = url, ForceRefresh = true }, token)`.
- JavaScript: `client.searchProviders.fetchTest(url, true)`.
- Python: `client.search_providers.fetch_test(url, force_refresh=True)`.
- Internal C#: `FetchAsync(url, blockedDomains, token, forceRefresh: true)`.
- Agentic search: optional boolean `forceRefresh` on the `fetch_url` tool.

## Admission and storage identity

Cache keys preserve scheme, host, path casing, trailing slash, query order, and duplicate parameters. Only scheme/host casing, default ports, and fragments are normalized. The key also includes a versioned user-agent/converter profile. This intentionally differs from the more aggressive research URL deduplication hash.

Private/no-store/no-cache responses, cookies, unsupported `Vary` headers, errors, unsupported formats, empty content, signed/token-bearing URLs, credential-bearing URLs, and oversized entries bypass storage. `Vary: Accept-Encoding` is supported; other variants bypass. Redirected responses also bypass storage because CrawlSharp 1.2.3 does not expose intermediate cookie/cache headers. This avoids treating a personalized redirect chain as shared public content. Redirect caching can be added when those headers can be checked at every hop.

The database binds each namespace to its configured provider/location. The same namespace with a different backing location refuses cache reads, writes, and cleanup; origin retrieval still works. Use the same namespace only for replicas sharing the same S3 prefix or the same disk filesystem. Independent disks require distinct namespaces. Namespace bindings remain as small permanent safety records after their disposable entries are removed.

Objects are private, with opaque namespace/entry/generation keys. No public object URLs or new cache enumeration endpoints are exposed. Current collection APIs retain `EnumerationQuery`/`EnumerationResult`.

## S3 configuration

Select `provider: "S3"`, a new namespace, a private bucket, and a dedicated prefix. Leave `s3Endpoint` empty for AWS; a custom HTTP(S) endpoint uses S3-compatible path-style addressing. Set credentials outside the committed JSON:

```text
MYMORNINGNEWS_CRAWL_CACHE_S3_ACCESS_KEY
MYMORNINGNEWS_CRAWL_CACHE_S3_SECRET_KEY
```

Blobject's current S3 client requires an explicit access/secret pair for private storage. Empty credentials select anonymous access in the library, so this application rejects enabled S3 caching without both keys. IAM roles and temporary session credentials are not supported by this adapter. Bucket provisioning is an operator action; allow object read/write/delete only for the dedicated cache prefix and retain private bucket access.

All settings have `MYMORNINGNEWS_CRAWL_CACHE_*` overrides. Other suffixes are `ENABLED`, `NAMESPACE`, `FRESHNESS_SECONDS`, `MAX_ENTRY_BYTES`, `MAX_TOTAL_BYTES`, `MAX_ENTRIES`, `CLEANUP_INTERVAL_SECONDS`, `CLEANUP_BATCH_SIZE`, `OPERATION_TIMEOUT_MS`, `LEASE_SECONDS`, `WAIT_FOR_FILL_MS`, `MAX_CONCURRENCY`, `PROVIDER`, `DIRECTORY`, `S3_ENDPOINT`, `S3_REGION`, `S3_BUCKET`, and `S3_PREFIX`. Secrets are masked in settings responses. Environment overrides win and are not persisted into the JSON.

Provider, location, namespace, credentials, and object-operation concurrency changes require restart. Freshness, capacity, timeouts, and cleanup limits apply live. Turning off `enabled` stops cache reads/writes; cleanup continues for the configured namespace.

## Failure recovery and maintenance

A short PostgreSQL lease coordinates fills across processes. Unique lease tokens fence out late writers, and leases renew during long work. Ordinary contenders wait a bounded time, recheck for a hit, then may retrieve independently without publishing. Forced callers always retrieve anew. PostgreSQL transactions never remain open across HTTP or object-store operations.

Raw and Markdown keys are recorded before uploading. Only a completed generation becomes active; partially written and superseded generations remain recorded until cleanup removes them. Object reads validate lengths and checksums. Missing/corrupt Markdown triggers a fresh retrieval; a bounded background integrity pass checks both objects. Cache database/storage failures do not turn a successful origin retrieval into a failure. Logs report exception types without URLs, bodies, or credentials.

The cleanup worker claims bounded batches of expired, abandoned, superseded, and capacity-evicted generations. It deletes exact recorded objects before releasing their byte reservations or deleting metadata. Failed deletes retain accounting and retry after five minutes. Abandoned writes have a grace period for bounded in-flight object operations. Empty disk generation/entry directories are removed nonrecursively. Old empty leases are also reclaimed. Separate stored source/evidence records are never deleted by cache cleanup.

Capacity limits include ready, writing, and pending-deletion generations. The entry and generation counts are each bounded by `maxEntries`; byte reservations are transactional. Under pressure, expired/deleting generations are preferred, followed by least-recently-used fresh content. Access timestamps are updated at most every five minutes per entry. Storage overhead, empty namespace directories, safety bindings, and S3 version history are outside the content byte budget.

Monitor `mymorningnews.crawl_cache.events`, `.duration`, `.reserved_bytes`, and `.records` (Prometheus converts dots to underscores). Outcome labels distinguish hits, misses, forced retrieval, bypass, errors, publication, fencing, capacity pressure, expiry, eviction, and cleanup errors. Histograms sample reserved bytes and metadata counts each cleanup pass; totals from multiple samples are not current usage gauges. No URL, object key, or tenant labels are recorded.

Use disk quotas/alarms and optional S3 lifecycle expiry as additional controls. For versioned buckets, expire noncurrent versions and incomplete multipart uploads as well. Lifecycle deletion may create a safe cache miss before the database cleanup catches up.

To change backing storage, disable fills, expire/drain the old namespace while its configuration is still available, then configure the new location and a new namespace and restart. Do not reuse an old namespace with a new location. To recover after a database restore, disable the cache and remove only the dedicated retired cache namespace/prefix before starting a new namespace; restored metadata may reference missing objects and post-backup objects may have no metadata. Keep generated artifacts in their separate volume/prefix.

## Validation and rollout

`CrawlCacheSuite` exercises real PostgreSQL and disk, a controlled HTTP origin using the actual crawler/converter, and the Blobject S3 adapter. Its normal S3 case uses the repository's local S3 HTTP fixture. To run against a disposable MinIO instance instead, provide `MYMORNINGNEWS_TEST_CRAWL_S3_ENDPOINT=http://127.0.0.1:12196` with test credentials `mmn-crawl-test` / `mmn-crawl-test-password`; the test creates bucket `mmn-crawl-test` and cleans its unique prefix. The endpoint override is restricted to loopback and is never a production test.

Deploy migration 39 and the updated server with caching disabled. Configure storage, confirm filesystem permissions or private S3 access, enable caching, then check a miss, a hit, forced replacement, expiry, and cleanup. AWS S3 production credentials and bucket policy need a dedicated-prefix smoke test during deployment; local MinIO validation does not establish those account-specific permissions. Roll back behavior by disabling caching; the additive schema can remain.

The completed [implementation plan](../archive/CRAWL_CACHE_PLAN.md) is retained in the archive.

Copyright (c) 2026 Lattice AI LLC.
