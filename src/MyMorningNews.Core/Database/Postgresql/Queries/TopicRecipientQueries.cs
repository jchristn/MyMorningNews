namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the topicrecipients table.
    /// </summary>
    public static class TopicRecipientQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "topicrecipients";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, topicid, email, userid, isactive, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS topicrecipients (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    topicid VARCHAR(64) NOT NULL REFERENCES topics(id) ON DELETE CASCADE,
    email VARCHAR(256) NOT NULL,
    userid VARCHAR(64),
    isactive BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_topicrecipients_tenantid ON topicrecipients (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_topicrecipients_topicid ON topicrecipients (topicid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_topicrecipients_topicid_email ON topicrecipients (topicid, email)",
            @"CREATE INDEX IF NOT EXISTS ix_topicrecipients_createdutc ON topicrecipients (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO topicrecipients (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE topicrecipients SET topicid = $3, email = $4, userid = $5, isactive = $6, lastupdateutc = $7 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM topicrecipients WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM topicrecipients WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM topicrecipients WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM topicrecipients WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM topicrecipients WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
