namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the briefingitems table.
    /// </summary>
    public static class BriefingItemQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "briefingitems";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, briefingid, sectionid, itemtype, itemid, citationnumber, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS briefingitems (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    briefingid VARCHAR(64) NOT NULL,
    sectionid VARCHAR(64) NOT NULL,
    itemtype VARCHAR(64) NOT NULL,
    itemid VARCHAR(64) NOT NULL,
    citationnumber INTEGER,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingitems_tenantid ON briefingitems (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingitems_createdutc ON briefingitems (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO briefingitems (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE briefingitems SET briefingid = $3, sectionid = $4, itemtype = $5, itemid = $6, citationnumber = $7, sortorder = $8, active = $9, lastupdateutc = $10 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM briefingitems WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM briefingitems WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM briefingitems WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM briefingitems WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM briefingitems WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
