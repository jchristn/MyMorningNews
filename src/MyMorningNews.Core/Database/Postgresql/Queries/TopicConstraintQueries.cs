namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the topicconstraints table.
    /// </summary>
    public static class TopicConstraintQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "topicconstraints";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, topicid, dimensionid, kind, value, note, islocked, origin, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS topicconstraints (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    topicid VARCHAR(64) NOT NULL REFERENCES topics(id) ON DELETE CASCADE,
    dimensionid VARCHAR(64),
    kind VARCHAR(64) NOT NULL,
    value TEXT NOT NULL,
    note TEXT,
    islocked BOOLEAN NOT NULL,
    origin VARCHAR(64) NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_topicconstraints_tenantid ON topicconstraints (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_topicconstraints_topicid ON topicconstraints (topicid)",
            @"CREATE INDEX IF NOT EXISTS ix_topicconstraints_createdutc ON topicconstraints (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO topicconstraints (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE topicconstraints SET topicid = $3, dimensionid = $4, kind = $5, value = $6, note = $7, islocked = $8, origin = $9, active = $10, lastupdateutc = $11 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM topicconstraints WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM topicconstraints WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM topicconstraints WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM topicconstraints WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM topicconstraints WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
