namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the briefingprogress table.
    /// </summary>
    public static class BriefingProgressQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "briefingprogress";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, briefingid, positionseconds, durationseconds, completed, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS briefingprogress (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    briefingid VARCHAR(64) NOT NULL REFERENCES briefings(id) ON DELETE CASCADE,
    positionseconds DOUBLE PRECISION NOT NULL,
    durationseconds DOUBLE PRECISION,
    completed BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingprogress_tenantid ON briefingprogress (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingprogress_tenantid_userid ON briefingprogress (tenantid, userid)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingprogress_briefingid ON briefingprogress (briefingid)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingprogress_createdutc ON briefingprogress (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO briefingprogress (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE briefingprogress SET userid = $3, briefingid = $4, positionseconds = $5, durationseconds = $6, completed = $7, active = $8, lastupdateutc = $9 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM briefingprogress WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM briefingprogress WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM briefingprogress WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM briefingprogress WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM briefingprogress WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
