namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the modelassignments table.
    /// </summary>
    public static class ModelAssignmentQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "modelassignments";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, task, topicid, modelendpointid, modeloverride, reasoningeffort, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS modelassignments (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    task VARCHAR(64) NOT NULL,
    topicid VARCHAR(64),
    modelendpointid VARCHAR(64) NOT NULL REFERENCES modelendpoints(id) ON DELETE CASCADE,
    modeloverride VARCHAR(256),
    reasoningeffort VARCHAR(64),
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_modelassignments_tenantid ON modelassignments (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_modelassignments_modelendpointid ON modelassignments (modelendpointid)",
            @"CREATE INDEX IF NOT EXISTS ix_modelassignments_createdutc ON modelassignments (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO modelassignments (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE modelassignments SET task = $3, topicid = $4, modelendpointid = $5, modeloverride = $6, reasoningeffort = $7, active = $8, lastupdateutc = $9 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM modelassignments WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM modelassignments WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM modelassignments WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM modelassignments WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM modelassignments WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
