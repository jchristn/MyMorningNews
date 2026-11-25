namespace MyMorningNews.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database.Interfaces;
    using MyMorningNews.Core.Database.Postgresql.Queries;
    using MyMorningNews.Core.Fetch;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Settings;
    using Npgsql;

    /// <summary>Atomic crawl metadata operations. Short advisory-lock transactions never span object I/O.</summary>
    public class CrawlCacheMethods : PostgresqlMethodsBase, ICrawlCacheMethods
    {
        private const long _Lock = 7100443901;

        /// <summary>Create methods over an initialized PostgreSQL driver.</summary>
        /// <param name="driver">Owning driver.</param>
        public CrawlCacheMethods(PostgresqlDatabaseDriver driver) : base(driver) { }

        /// <inheritdoc />
        public async Task<bool> BindAsync(string ns, string storageIdentity, CancellationToken token = default)
        {
            List<int> rows = await LockedAsync("INSERT INTO crawlcachebindings(storagenamespace,storageidentity) VALUES($1,$2) ON CONFLICT(storagenamespace) DO UPDATE SET storageidentity=EXCLUDED.storageidentity WHERE crawlcachebindings.storageidentity=EXCLUDED.storageidentity RETURNING 1", r => 1, token, ns, storageIdentity).ConfigureAwait(false);
            return rows.Any();
        }

        /// <inheritdoc />
        public async Task<CrawlCacheGeneration?> ReadAsync(string ns, string key, string url, string profile, CancellationToken token = default)
        {
            List<CrawlCacheGeneration> rows = await QueryAsync("SELECT " + CrawlCacheQueries.Columns + " FROM crawlcacheentries e JOIN crawlcachegenerations g ON g.id=e.activeid WHERE e.storagenamespace=$1 AND e.cachekey=$2 AND e.url=$3 AND e.profile=$4 AND g.state='Ready'", Map, token, ns, key, url, profile).ConfigureAwait(false);
            if (rows.Count > 0) await ExecuteAsync("UPDATE crawlcacheentries SET accessedutc=NOW() WHERE id=$1 AND accessedutc<NOW()-INTERVAL '5 minutes'", token, rows[0].EntryId).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<CrawlCacheLease?> AcquireAsync(string ns, string key, string url, string profile, CrawlCacheSettings settings, CancellationToken token = default)
        {
            string owner = IdGenerator.Generate(IdPrefixes.CrawlCache);
            string id = IdGenerator.Generate(IdPrefixes.CrawlCache);
            List<CrawlCacheLease> rows = await LockedAsync(@"
                INSERT INTO crawlcacheentries(id,storagenamespace,cachekey,url,profile,leaseowner,leaseuntil)
                SELECT $1,$2,$3,$4,$5,$6,NOW()+make_interval(secs=>$7)
                WHERE EXISTS(SELECT 1 FROM crawlcacheentries WHERE storagenamespace=$2 AND cachekey=$3)
                   OR (SELECT COUNT(*) FROM crawlcacheentries WHERE storagenamespace=$2)<$8
                ON CONFLICT(storagenamespace,cachekey) DO UPDATE SET leaseowner=$6,leaseuntil=NOW()+make_interval(secs=>$7)
                WHERE (crawlcacheentries.leaseuntil IS NULL OR crawlcacheentries.leaseuntil<=NOW()) AND crawlcacheentries.url=$4 AND crawlcacheentries.profile=$5
                RETURNING id,leaseowner", r => new CrawlCacheLease { EntryId = r.GetString(0), Token = r.GetString(1) }, token, id, ns, key, url, profile, owner, (double)settings.LeaseSeconds, settings.MaxEntries).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<bool> RenewAsync(CrawlCacheLease lease, int seconds, CancellationToken token = default)
        {
            List<int> rows = await LockedAsync("UPDATE crawlcacheentries SET leaseuntil=NOW()+make_interval(secs=>$3) WHERE id=$1 AND leaseowner=$2 AND leaseuntil>NOW() RETURNING 1", r => 1, token, lease.EntryId, lease.Token, (double)seconds).ConfigureAwait(false);
            return rows.Any();
        }

        /// <inheritdoc />
        public async Task ReleaseAsync(CrawlCacheLease lease, CancellationToken token = default)
        {
            await LockedAsync(@"WITH empty AS (DELETE FROM crawlcacheentries e WHERE e.id=$1 AND e.leaseowner=$2 AND NOT EXISTS(SELECT 1 FROM crawlcachegenerations g WHERE g.entryid=e.id) RETURNING id)
                UPDATE crawlcacheentries SET leaseowner=NULL,leaseuntil=NULL WHERE id=$1 AND leaseowner=$2 AND id NOT IN(SELECT id FROM empty) RETURNING 1", r => 1, token, lease.EntryId, lease.Token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ReserveAsync(CrawlCacheLease lease, CrawlCacheGeneration g, CrawlCacheSettings settings, CancellationToken token = default)
        {
            List<int> rows = await LockedAsync(@"
                INSERT INTO crawlcachegenerations(id,entryid,storagenamespace,url,profile,state,rawkey,textkey,rawhash,texthash,rawbytes,textbytes,fetchedutc,expiresutc,finalurl,contenttype,title,statuscode,owner,retryutc)
                SELECT $1,$2,$3,$4,$5,'Writing',$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,NOW()+make_interval(secs=>$21)
                WHERE EXISTS(SELECT 1 FROM crawlcacheentries WHERE id=$2 AND leaseowner=$18 AND leaseuntil>NOW())
                  AND (SELECT COUNT(*) FROM crawlcachegenerations WHERE storagenamespace=$3)<$19
                  AND (SELECT COALESCE(SUM(rawbytes+textbytes),0) FROM crawlcachegenerations WHERE storagenamespace=$3)+$10+$11<=$20
                RETURNING 1", r => 1, token, g.Id, lease.EntryId, g.Namespace, g.Url, g.Profile, g.RawKey, g.TextKey, g.RawHash, g.TextHash, g.RawBytes, g.TextBytes, g.FetchedUtc, g.ExpiresUtc, g.FinalUrl, g.ContentType, g.Title, g.StatusCode, lease.Token, settings.MaxEntries, settings.MaxTotalBytes, (double)(settings.LeaseSeconds + 61)).ConfigureAwait(false);
            return rows.Any();
        }

        /// <inheritdoc />
        public async Task<bool> PublishAsync(CrawlCacheLease lease, string generationId, CancellationToken token = default)
        {
            List<int> rows = await LockedAsync(@"
                WITH owned AS (SELECT id,activeid FROM crawlcacheentries WHERE id=$1 AND leaseowner=$2 AND leaseuntil>NOW()),
                ready AS (UPDATE crawlcachegenerations SET state='Ready',retryutc=NOW() WHERE id=$3 AND owner=$2 AND state='Writing' AND entryid IN(SELECT id FROM owned) RETURNING id),
                retired AS (UPDATE crawlcachegenerations SET state='Deleting',retryutc=NOW() WHERE id IN(SELECT activeid FROM owned) AND EXISTS(SELECT 1 FROM ready) RETURNING id)
                UPDATE crawlcacheentries SET activeid=$3,accessedutc=NOW() WHERE id IN(SELECT id FROM owned) AND EXISTS(SELECT 1 FROM ready) RETURNING 1", r => 1, token, lease.EntryId, lease.Token, generationId).ConfigureAwait(false);
            return rows.Any();
        }

        /// <inheritdoc />
        public async Task InvalidateAsync(string generationId, CancellationToken token = default)
        {
            await LockedAsync(@"WITH detached AS (UPDATE crawlcacheentries SET activeid=NULL WHERE activeid=$1 RETURNING id)
                UPDATE crawlcachegenerations SET state='Deleting',retryutc=NOW() WHERE id=$1 AND state='Ready' RETURNING 1", r => 1, token, generationId).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<CrawlCacheGeneration>> ClaimCleanupAsync(string ns, CrawlCacheSettings settings, CancellationToken token = default)
        {
            string claim = IdGenerator.Generate(IdPrefixes.CrawlCache);
            return await LockedAsync(@"
                WITH pressure AS (SELECT COUNT(*)>=$4 OR COALESCE(SUM(rawbytes+textbytes),0)>=$5 AS pressured FROM crawlcachegenerations WHERE storagenamespace=$1),
                candidates AS (
                  SELECT g.id FROM crawlcachegenerations g JOIN crawlcacheentries e ON e.id=g.entryid
                  WHERE g.storagenamespace=$1 AND g.retryutc<=NOW()
                    AND (e.leaseuntil IS NULL OR e.leaseuntil<=NOW())
                    AND (g.state IN('Deleting','Writing') OR g.expiresutc<=NOW() OR g.fetchedutc+make_interval(secs=>$6)<=NOW() OR (SELECT pressured FROM pressure))
                  ORDER BY CASE WHEN g.state<>'Ready' OR g.expiresutc<=NOW() THEN 0 ELSE 1 END,e.accessedutc,g.id LIMIT $2),
                detached AS (UPDATE crawlcacheentries SET activeid=NULL WHERE activeid IN(SELECT id FROM candidates) RETURNING id)
                UPDATE crawlcachegenerations g SET state='Deleting',claim=$3,retryutc=NOW()+INTERVAL '5 minutes'
                WHERE g.id IN(SELECT id FROM candidates) RETURNING " + CrawlCacheQueries.Columns, Map, token, ns, settings.CleanupBatchSize, claim, settings.MaxEntries, Math.Max(0L, settings.MaxTotalBytes - settings.MaxEntryBytes), (double)settings.FreshnessSeconds).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task CompleteCleanupAsync(CrawlCacheGeneration g, CancellationToken token = default)
        {
            await LockedAsync("DELETE FROM crawlcachegenerations WHERE id=$1 AND state='Deleting' AND claim=$2 RETURNING 1", r => 1, token, g.Id, g.Claim).ConfigureAwait(false);
            await LockedAsync("DELETE FROM crawlcacheentries e WHERE id=$1 AND (leaseuntil IS NULL OR leaseuntil<=NOW()) AND NOT EXISTS(SELECT 1 FROM crawlcachegenerations g WHERE g.entryid=e.id) RETURNING 1", r => 1, token, g.EntryId).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<CrawlCacheGeneration?> ClaimVerificationAsync(string ns, CancellationToken token = default)
        {
            // Also reap crashed empty leases, in a bounded batch.
            await LockedAsync(@"DELETE FROM crawlcacheentries WHERE id IN (SELECT e.id FROM crawlcacheentries e WHERE storagenamespace=$1 AND (leaseuntil IS NULL OR leaseuntil<=NOW()) AND NOT EXISTS(SELECT 1 FROM crawlcachegenerations g WHERE g.entryid=e.id) ORDER BY e.id LIMIT 100) RETURNING 1", r => 1, token, ns).ConfigureAwait(false);
            List<CrawlCacheGeneration> rows = await LockedAsync("UPDATE crawlcachegenerations g SET checkedutc=NOW() WHERE g.id IN(SELECT id FROM crawlcachegenerations WHERE storagenamespace=$1 AND state='Ready' AND checkedutc<NOW()-INTERVAL '5 minutes' ORDER BY checkedutc,id LIMIT 1) RETURNING " + CrawlCacheQueries.Columns, Map, token, ns).ConfigureAwait(false);
            return rows.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<long[]> UsageAsync(string ns, CancellationToken token = default)
        {
            List<long[]> rows = await QueryAsync("SELECT COALESCE(SUM(rawbytes+textbytes),0)::bigint,COUNT(*),(SELECT COUNT(*) FROM crawlcacheentries WHERE storagenamespace=$1) FROM crawlcachegenerations WHERE storagenamespace=$1", r => new[] { r.GetInt64(0), r.GetInt64(1), r.GetInt64(2) }, token, ns).ConfigureAwait(false);
            return rows[0];
        }

        private Task<List<T>> LockedAsync<T>(string sql, Func<NpgsqlDataReader, T> map, CancellationToken token, params object?[] args)
        {
            return Driver.QueryWithAdvisoryLockAsync(_Lock, sql, map, args, token);
        }

        private static CrawlCacheGeneration Map(NpgsqlDataReader r)
        {
            return new CrawlCacheGeneration
            {
                Id = r.GetString(0), EntryId = r.GetString(1), Namespace = r.GetString(2), Url = r.GetString(3), Profile = r.GetString(4),
                RawKey = r.GetString(5), TextKey = r.GetString(6), RawHash = r.GetString(7), TextHash = r.GetString(8), RawBytes = r.GetInt64(9), TextBytes = r.GetInt64(10),
                FetchedUtc = r.GetDateTime(11), ExpiresUtc = r.GetDateTime(12), FinalUrl = r.GetString(13), ContentType = r.GetString(14), Title = r.GetString(15), StatusCode = r.GetInt32(16), Claim = r.GetString(17)
            };
        }
    }
}
