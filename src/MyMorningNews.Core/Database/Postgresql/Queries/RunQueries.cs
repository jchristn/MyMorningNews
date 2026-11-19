namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the runs table.
    /// </summary>
    public static class RunQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "runs";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, topicid, userid, scheduleid, researchrequestid, trigger, status, isdryrun, cancelrequested, queuedutc, startedutc, completedutc, lockeduntilutc, workerid, skippedreason, errormessage, summary, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS runs (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    topicid VARCHAR(64) NOT NULL,
    userid VARCHAR(64) NOT NULL,
    scheduleid VARCHAR(64),
    researchrequestid VARCHAR(64),
    trigger VARCHAR(64) NOT NULL,
    status VARCHAR(64) NOT NULL,
    isdryrun BOOLEAN NOT NULL,
    cancelrequested BOOLEAN NOT NULL,
    queuedutc TIMESTAMPTZ NOT NULL,
    startedutc TIMESTAMPTZ,
    completedutc TIMESTAMPTZ,
    lockeduntilutc TIMESTAMPTZ,
    workerid VARCHAR(128),
    skippedreason TEXT,
    errormessage TEXT,
    summary TEXT,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_runs_tenantid ON runs (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_runs_createdutc ON runs (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO runs (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE runs SET topicid = $3, userid = $4, scheduleid = $5, researchrequestid = $6, trigger = $7, status = $8, isdryrun = $9, cancelrequested = $10, queuedutc = $11, startedutc = $12, completedutc = $13, lockeduntilutc = $14, workerid = $15, skippedreason = $16, errormessage = $17, summary = $18, active = $19, lastupdateutc = $20 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM runs WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM runs WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM runs WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM runs WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM runs WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
