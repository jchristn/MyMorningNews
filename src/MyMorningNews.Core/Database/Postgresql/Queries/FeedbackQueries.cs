namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the feedback table.
    /// </summary>
    public static class FeedbackQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "feedback";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, targettype, targetid, topicid, relevance, accuracy, usefulness, reason, channel, emailmessageid, supersedesfeedbackid, iscurrent, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS feedback (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    targettype VARCHAR(64) NOT NULL,
    targetid VARCHAR(64) NOT NULL,
    topicid VARCHAR(64),
    relevance VARCHAR(64),
    accuracy VARCHAR(64),
    usefulness VARCHAR(64),
    reason TEXT,
    channel VARCHAR(64) NOT NULL,
    emailmessageid VARCHAR(64),
    supersedesfeedbackid VARCHAR(64),
    iscurrent BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_feedback_tenantid ON feedback (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_feedback_createdutc ON feedback (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO feedback (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE feedback SET userid = $3, targettype = $4, targetid = $5, topicid = $6, relevance = $7, accuracy = $8, usefulness = $9, reason = $10, channel = $11, emailmessageid = $12, supersedesfeedbackid = $13, iscurrent = $14, active = $15, lastupdateutc = $16 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM feedback WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM feedback WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM feedback WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM feedback WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM feedback WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
