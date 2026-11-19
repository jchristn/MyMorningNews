namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the topicdimensions table.
    /// </summary>
    public static class TopicDimensionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "topicdimensions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, topicid, name, description, rationale, weight, includeterms, excludeterms, islocked, origin, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS topicdimensions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    topicid VARCHAR(64) NOT NULL REFERENCES topics(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    rationale TEXT,
    weight VARCHAR(64) NOT NULL,
    includeterms TEXT[] NOT NULL,
    excludeterms TEXT[] NOT NULL,
    islocked BOOLEAN NOT NULL,
    origin VARCHAR(64) NOT NULL,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_topicdimensions_tenantid ON topicdimensions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_topicdimensions_topicid ON topicdimensions (topicid)",
            @"CREATE INDEX IF NOT EXISTS ix_topicdimensions_createdutc ON topicdimensions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO topicdimensions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE topicdimensions SET topicid = $3, name = $4, description = $5, rationale = $6, weight = $7, includeterms = $8, excludeterms = $9, islocked = $10, origin = $11, sortorder = $12, active = $13, lastupdateutc = $14 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM topicdimensions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM topicdimensions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM topicdimensions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM topicdimensions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM topicdimensions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
