namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the learningcheckpoints table.
    /// </summary>
    public static class LearningCheckpointQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "learningcheckpoints";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, topicid, lastfeedbackutc, lastrunutc, proposed, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS learningcheckpoints (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    topicid VARCHAR(64) NOT NULL,
    lastfeedbackutc TIMESTAMPTZ,
    lastrunutc TIMESTAMPTZ,
    proposed INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_learningcheckpoints_tenantid ON learningcheckpoints (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_learningcheckpoints_tenantid_userid_topicid ON learningcheckpoints (tenantid, userid, topicid)",
            @"CREATE INDEX IF NOT EXISTS ix_learningcheckpoints_createdutc ON learningcheckpoints (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO learningcheckpoints (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE learningcheckpoints SET userid = $3, topicid = $4, lastfeedbackutc = $5, lastrunutc = $6, proposed = $7, active = $8, lastupdateutc = $9 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM learningcheckpoints WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM learningcheckpoints WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM learningcheckpoints WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM learningcheckpoints WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM learningcheckpoints WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
