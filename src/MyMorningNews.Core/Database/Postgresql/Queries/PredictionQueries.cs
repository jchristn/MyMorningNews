namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the predictions table.
    /// </summary>
    public static class PredictionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "predictions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, runid, topicid, userid, dimensionid, statement, rationale, confidence, horizonutc, status, resolvedutc, resolvedby, resolutionnote, brierscore, isdryrun, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS predictions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    runid VARCHAR(64) NOT NULL,
    topicid VARCHAR(64) NOT NULL,
    userid VARCHAR(64) NOT NULL,
    dimensionid VARCHAR(64),
    statement TEXT NOT NULL,
    rationale TEXT,
    confidence DOUBLE PRECISION NOT NULL,
    horizonutc TIMESTAMPTZ NOT NULL,
    status VARCHAR(64) NOT NULL,
    resolvedutc TIMESTAMPTZ,
    resolvedby VARCHAR(32),
    resolutionnote TEXT,
    brierscore DOUBLE PRECISION,
    isdryrun BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_predictions_tenantid ON predictions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_predictions_createdutc ON predictions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO predictions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE predictions SET runid = $3, topicid = $4, userid = $5, dimensionid = $6, statement = $7, rationale = $8, confidence = $9, horizonutc = $10, status = $11, resolvedutc = $12, resolvedby = $13, resolutionnote = $14, brierscore = $15, isdryrun = $16, active = $17, lastupdateutc = $18 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM predictions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM predictions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM predictions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM predictions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM predictions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
