namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the searchresults table.
    /// </summary>
    public static class SearchResultQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "searchresults";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, searchqueryid, runid, topicid, url, canonicalurlhash, title, snippet, publishedutc, rank, provider, disposition, sourceitemid, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS searchresults (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    searchqueryid VARCHAR(64) NOT NULL REFERENCES searchqueries(id) ON DELETE CASCADE,
    runid VARCHAR(64) NOT NULL,
    topicid VARCHAR(64) NOT NULL,
    url TEXT NOT NULL,
    canonicalurlhash VARCHAR(64) NOT NULL,
    title TEXT,
    snippet TEXT,
    publishedutc TIMESTAMPTZ,
    rank INTEGER NOT NULL,
    provider VARCHAR(32),
    disposition VARCHAR(64) NOT NULL,
    sourceitemid VARCHAR(64),
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_searchresults_tenantid ON searchresults (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_searchresults_searchqueryid ON searchresults (searchqueryid)",
            @"CREATE INDEX IF NOT EXISTS ix_searchresults_createdutc ON searchresults (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO searchresults (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE searchresults SET searchqueryid = $3, runid = $4, topicid = $5, url = $6, canonicalurlhash = $7, title = $8, snippet = $9, publishedutc = $10, rank = $11, provider = $12, disposition = $13, sourceitemid = $14, active = $15, lastupdateutc = $16 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM searchresults WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM searchresults WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM searchresults WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM searchresults WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM searchresults WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
