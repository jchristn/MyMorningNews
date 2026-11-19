namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the findings table.
    /// </summary>
    public static class FindingQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "findings";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, runid, topicid, userid, dimensionid, sourceitemid, title, summary, whyitmatters, category, relevancescore, llmscore, feedbackscore, noveltyscore, sourcescore, recencyscore, tier, relevancerationale, confidence, isduplicate, duplicateoffindingid, isupdateoffindingid, rank, userrating, publishedutc, isdryrun, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS findings (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    runid VARCHAR(64) NOT NULL,
    topicid VARCHAR(64) NOT NULL,
    userid VARCHAR(64) NOT NULL,
    dimensionid VARCHAR(64),
    sourceitemid VARCHAR(64),
    title TEXT NOT NULL,
    summary TEXT,
    whyitmatters TEXT,
    category VARCHAR(64) NOT NULL,
    relevancescore DOUBLE PRECISION NOT NULL,
    llmscore DOUBLE PRECISION NOT NULL,
    feedbackscore DOUBLE PRECISION NOT NULL,
    noveltyscore DOUBLE PRECISION NOT NULL,
    sourcescore DOUBLE PRECISION NOT NULL,
    recencyscore DOUBLE PRECISION NOT NULL,
    tier VARCHAR(64) NOT NULL,
    relevancerationale TEXT,
    confidence DOUBLE PRECISION NOT NULL,
    isduplicate BOOLEAN NOT NULL,
    duplicateoffindingid VARCHAR(64),
    isupdateoffindingid VARCHAR(64),
    rank INTEGER NOT NULL,
    userrating VARCHAR(32),
    publishedutc TIMESTAMPTZ,
    isdryrun BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_findings_tenantid ON findings (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_findings_createdutc ON findings (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO findings (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27, $28, $29, $30)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE findings SET runid = $3, topicid = $4, userid = $5, dimensionid = $6, sourceitemid = $7, title = $8, summary = $9, whyitmatters = $10, category = $11, relevancescore = $12, llmscore = $13, feedbackscore = $14, noveltyscore = $15, sourcescore = $16, recencyscore = $17, tier = $18, relevancerationale = $19, confidence = $20, isduplicate = $21, duplicateoffindingid = $22, isupdateoffindingid = $23, rank = $24, userrating = $25, publishedutc = $26, isdryrun = $27, active = $28, lastupdateutc = $29 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM findings WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM findings WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM findings WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM findings WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM findings WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
