namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the searchqueries table.
    /// </summary>
    public static class SearchQueryQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "searchqueries";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, runid, topicid, dimensionid, querytext, rationale, searchproviderid, provider, resultcount, durationms, succeeded, errormessage, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS searchqueries (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    runid VARCHAR(64) NOT NULL REFERENCES runs(id) ON DELETE CASCADE,
    topicid VARCHAR(64) NOT NULL,
    dimensionid VARCHAR(64),
    querytext TEXT NOT NULL,
    rationale TEXT,
    searchproviderid VARCHAR(64),
    provider VARCHAR(32),
    resultcount INTEGER NOT NULL,
    durationms DOUBLE PRECISION NOT NULL,
    succeeded BOOLEAN NOT NULL,
    errormessage TEXT,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_searchqueries_tenantid ON searchqueries (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_searchqueries_runid ON searchqueries (runid)",
            @"CREATE INDEX IF NOT EXISTS ix_searchqueries_createdutc ON searchqueries (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO searchqueries (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE searchqueries SET runid = $3, topicid = $4, dimensionid = $5, querytext = $6, rationale = $7, searchproviderid = $8, provider = $9, resultcount = $10, durationms = $11, succeeded = $12, errormessage = $13, active = $14, lastupdateutc = $15 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM searchqueries WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM searchqueries WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM searchqueries WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM searchqueries WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM searchqueries WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
