namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the refinementsessions table.
    /// </summary>
    public static class RefinementSessionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "refinementsessions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, topicid, userid, status, step, round, seedtext, goal, depth, horizontext, known, sufficientcontext, outofscopereason, modelendpointid, lasterror, completedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS refinementsessions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    topicid VARCHAR(64) NOT NULL REFERENCES topics(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    status VARCHAR(64) NOT NULL,
    step VARCHAR(64) NOT NULL,
    round INTEGER NOT NULL,
    seedtext TEXT NOT NULL,
    goal TEXT,
    depth VARCHAR(64),
    horizontext VARCHAR(128),
    known TEXT,
    sufficientcontext BOOLEAN NOT NULL,
    outofscopereason TEXT,
    modelendpointid VARCHAR(64),
    lasterror TEXT,
    completedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementsessions_tenantid ON refinementsessions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementsessions_topicid ON refinementsessions (topicid)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementsessions_createdutc ON refinementsessions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO refinementsessions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE refinementsessions SET topicid = $3, userid = $4, status = $5, step = $6, round = $7, seedtext = $8, goal = $9, depth = $10, horizontext = $11, known = $12, sufficientcontext = $13, outofscopereason = $14, modelendpointid = $15, lasterror = $16, completedutc = $17, active = $18, lastupdateutc = $19 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM refinementsessions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM refinementsessions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM refinementsessions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM refinementsessions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM refinementsessions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
