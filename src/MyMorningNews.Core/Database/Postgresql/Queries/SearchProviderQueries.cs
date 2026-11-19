namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the searchproviders table.
    /// </summary>
    public static class SearchProviderQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "searchproviders";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, name, kind, endpoint, credentialid, isdefault, maxresults, timeoutms, lasttestedutc, lasttestok, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS searchproviders (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(128) NOT NULL,
    kind VARCHAR(64) NOT NULL,
    endpoint VARCHAR(512),
    credentialid VARCHAR(64),
    isdefault BOOLEAN NOT NULL,
    maxresults INTEGER NOT NULL,
    timeoutms INTEGER NOT NULL,
    lasttestedutc TIMESTAMPTZ,
    lasttestok BOOLEAN,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_searchproviders_tenantid ON searchproviders (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_searchproviders_createdutc ON searchproviders (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO searchproviders (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE searchproviders SET name = $3, kind = $4, endpoint = $5, credentialid = $6, isdefault = $7, maxresults = $8, timeoutms = $9, lasttestedutc = $10, lasttestok = $11, active = $12, lastupdateutc = $13 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM searchproviders WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM searchproviders WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM searchproviders WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM searchproviders WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM searchproviders WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
