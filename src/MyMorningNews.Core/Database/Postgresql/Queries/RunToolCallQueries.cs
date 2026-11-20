namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the runtoolcalls table.
    /// </summary>
    public static class RunToolCallQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "runtoolcalls";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, runid, topicid, tool, argumentssummary, succeeded, errormessage, resultcharacters, durationms, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS runtoolcalls (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    runid VARCHAR(64) NOT NULL,
    topicid VARCHAR(64) NOT NULL,
    tool VARCHAR(320) NOT NULL,
    argumentssummary TEXT,
    succeeded BOOLEAN NOT NULL,
    errormessage TEXT,
    resultcharacters INTEGER NOT NULL,
    durationms DOUBLE PRECISION NOT NULL,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_runtoolcalls_tenantid ON runtoolcalls (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_runtoolcalls_createdutc ON runtoolcalls (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO runtoolcalls (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE runtoolcalls SET runid = $3, topicid = $4, tool = $5, argumentssummary = $6, succeeded = $7, errormessage = $8, resultcharacters = $9, durationms = $10, sortorder = $11, active = $12, lastupdateutc = $13 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM runtoolcalls WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM runtoolcalls WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM runtoolcalls WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM runtoolcalls WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM runtoolcalls WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
