namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the predictionreviews table.
    /// </summary>
    public static class PredictionReviewQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "predictionreviews";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, predictionid, runid, proposedstatus, rationale, confidence, acceptedbyuser, findingids, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS predictionreviews (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    predictionid VARCHAR(64) NOT NULL REFERENCES predictions(id) ON DELETE CASCADE,
    runid VARCHAR(64) NOT NULL,
    proposedstatus VARCHAR(64) NOT NULL,
    rationale TEXT,
    confidence DOUBLE PRECISION NOT NULL,
    acceptedbyuser BOOLEAN,
    findingids TEXT[] NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_predictionreviews_tenantid ON predictionreviews (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_predictionreviews_predictionid ON predictionreviews (predictionid)",
            @"CREATE INDEX IF NOT EXISTS ix_predictionreviews_createdutc ON predictionreviews (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO predictionreviews (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE predictionreviews SET predictionid = $3, runid = $4, proposedstatus = $5, rationale = $6, confidence = $7, acceptedbyuser = $8, findingids = $9, active = $10, lastupdateutc = $11 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM predictionreviews WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM predictionreviews WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM predictionreviews WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM predictionreviews WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM predictionreviews WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
