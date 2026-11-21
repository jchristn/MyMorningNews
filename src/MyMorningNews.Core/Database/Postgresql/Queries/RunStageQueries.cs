namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the runstages table.
    /// </summary>
    public static class RunStageQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "runstages";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, runid, stage, status, startedutc, completedutc, durationms, itemsin, itemsout, errormessage, sortorder, active, createdutc, lastupdateutc, llmcallcount, unpricedcallcount, inputtokens, outputtokens, cachedinputtokens, reasoningtokens, estimatedcostusd";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS runstages (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    runid VARCHAR(64) NOT NULL REFERENCES runs(id) ON DELETE CASCADE,
    stage VARCHAR(64) NOT NULL,
    status VARCHAR(64) NOT NULL,
    startedutc TIMESTAMPTZ,
    completedutc TIMESTAMPTZ,
    durationms DOUBLE PRECISION NOT NULL,
    itemsin INTEGER NOT NULL,
    itemsout INTEGER NOT NULL,
    errormessage TEXT,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_runstages_tenantid ON runstages (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_runstages_runid ON runstages (runid)",
            @"CREATE INDEX IF NOT EXISTS ix_runstages_createdutc ON runstages (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO runstages (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE runstages SET runid = $3, stage = $4, status = $5, startedutc = $6, completedutc = $7, durationms = $8, itemsin = $9, itemsout = $10, errormessage = $11, sortorder = $12, active = $13, lastupdateutc = $14, llmcallcount = $15, unpricedcallcount = $16, inputtokens = $17, outputtokens = $18, cachedinputtokens = $19, reasoningtokens = $20, estimatedcostusd = $21 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM runstages WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM runstages WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM runstages WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM runstages WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM runstages WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
