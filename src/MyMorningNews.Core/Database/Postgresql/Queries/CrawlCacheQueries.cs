namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>Typed disposable crawl metadata schema. Body bytes live only in Blobject.</summary>
    public static class CrawlCacheQueries
    {
        /// <summary>Generation columns in mapper order.</summary>
        public const string Columns = "g.id,g.entryid,g.storagenamespace,g.url,g.profile,g.rawkey,g.textkey,g.rawhash,g.texthash,g.rawbytes,g.textbytes,g.fetchedutc,g.expiresutc,g.finalurl,g.contenttype,g.title,g.statuscode,g.claim";
        /// <summary>Schema statements for migration 39.</summary>
        public static IReadOnlyList<string> Schema { get; } = new List<string>
        {
            "CREATE TABLE crawlcachebindings (storagenamespace VARCHAR(64) PRIMARY KEY, storageidentity VARCHAR(64) NOT NULL)",
            "CREATE TABLE crawlcacheentries (id VARCHAR(64) PRIMARY KEY, storagenamespace VARCHAR(64) NOT NULL, cachekey VARCHAR(64) NOT NULL, url TEXT NOT NULL, profile VARCHAR(64) NOT NULL, activeid VARCHAR(64), leaseowner VARCHAR(64), leaseuntil TIMESTAMPTZ, accessedutc TIMESTAMPTZ NOT NULL DEFAULT NOW(), UNIQUE(storagenamespace,cachekey))",
            "CREATE TABLE crawlcachegenerations (id VARCHAR(64) PRIMARY KEY, entryid VARCHAR(64) NOT NULL REFERENCES crawlcacheentries(id), storagenamespace VARCHAR(64) NOT NULL, url TEXT NOT NULL, profile VARCHAR(64) NOT NULL, state VARCHAR(16) NOT NULL, rawkey TEXT NOT NULL, textkey TEXT NOT NULL, rawhash VARCHAR(64) NOT NULL, texthash VARCHAR(64) NOT NULL, rawbytes BIGINT NOT NULL, textbytes BIGINT NOT NULL, fetchedutc TIMESTAMPTZ NOT NULL, expiresutc TIMESTAMPTZ NOT NULL, finalurl TEXT NOT NULL, contenttype TEXT NOT NULL, title TEXT NOT NULL, statuscode INTEGER NOT NULL, owner VARCHAR(64) NOT NULL, claim VARCHAR(64) NOT NULL DEFAULT '', retryutc TIMESTAMPTZ NOT NULL DEFAULT NOW(), checkedutc TIMESTAMPTZ NOT NULL DEFAULT NOW())",
            "CREATE INDEX ix_crawlcache_expiry ON crawlcachegenerations(storagenamespace,state,expiresutc,id)",
            "CREATE INDEX ix_crawlcache_entry ON crawlcachegenerations(entryid)",
            "CREATE INDEX ix_crawlcache_lease ON crawlcacheentries(storagenamespace,leaseuntil)",
            "CREATE INDEX ix_crawlcache_verify ON crawlcachegenerations(storagenamespace,checkedutc,id)"
        };
    }
}
