namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the sourceitems table.
    /// </summary>
    public static class SourceItemQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "sourceitems";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, canonicalurl, canonicalurlhash, domain, title, publishedutc, fetchedutc, httpstatus, contenttype, contenthash, contenttext, contentbytes, firstseenrunid, timesseen, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS sourceitems (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    canonicalurl TEXT NOT NULL,
    canonicalurlhash VARCHAR(64) NOT NULL,
    domain VARCHAR(256),
    title TEXT,
    publishedutc TIMESTAMPTZ,
    fetchedutc TIMESTAMPTZ NOT NULL,
    httpstatus INTEGER,
    contenttype VARCHAR(128),
    contenthash VARCHAR(64),
    contenttext TEXT,
    contentbytes BIGINT NOT NULL,
    firstseenrunid VARCHAR(64),
    timesseen INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_sourceitems_tenantid ON sourceitems (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_sourceitems_tenantid_canonicalurlhash ON sourceitems (tenantid, canonicalurlhash)",
            @"CREATE INDEX IF NOT EXISTS ix_sourceitems_createdutc ON sourceitems (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO sourceitems (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE sourceitems SET canonicalurl = $3, canonicalurlhash = $4, domain = $5, title = $6, publishedutc = $7, fetchedutc = $8, httpstatus = $9, contenttype = $10, contenthash = $11, contenttext = $12, contentbytes = $13, firstseenrunid = $14, timesseen = $15, active = $16, lastupdateutc = $17 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM sourceitems WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM sourceitems WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM sourceitems WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM sourceitems WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM sourceitems WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
