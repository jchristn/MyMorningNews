namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the runhistorysnapshots table.
    /// </summary>
    public static class RunHistorySnapshotQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "runhistorysnapshots";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, runid, topicid, mode, baselineids, contexttext, contexthash, cutoffutc, truncated, note, active, createdutc, lastupdateutc, editionid, briefingid";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS runhistorysnapshots (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    runid VARCHAR(64) NOT NULL REFERENCES runs(id) ON DELETE CASCADE,
    topicid VARCHAR(64) NOT NULL REFERENCES topics(id) ON DELETE CASCADE,
    mode VARCHAR(32) NOT NULL,
    baselineids TEXT[] NOT NULL,
    contexttext TEXT NOT NULL,
    contexthash VARCHAR(64) NOT NULL,
    cutoffutc TIMESTAMPTZ NOT NULL,
    truncated BOOLEAN NOT NULL,
    note TEXT,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_runhistorysnapshots_tenantid ON runhistorysnapshots (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_runhistorysnapshots_runid ON runhistorysnapshots (runid)",
            @"CREATE INDEX IF NOT EXISTS ix_runhistorysnapshots_topicid ON runhistorysnapshots (topicid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_runhistorysnapshots_tenantid_runid ON runhistorysnapshots (tenantid, runid)",
            @"CREATE INDEX IF NOT EXISTS ix_runhistorysnapshots_createdutc ON runhistorysnapshots (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO runhistorysnapshots (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE runhistorysnapshots SET runid = $3, topicid = $4, mode = $5, baselineids = $6, contexttext = $7, contexthash = $8, cutoffutc = $9, truncated = $10, note = $11, active = $12, lastupdateutc = $13, editionid = $14, briefingid = $15 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM runhistorysnapshots WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM runhistorysnapshots WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM runhistorysnapshots WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM runhistorysnapshots WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM runhistorysnapshots WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
