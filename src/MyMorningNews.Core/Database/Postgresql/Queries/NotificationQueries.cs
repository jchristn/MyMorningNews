namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the notifications table.
    /// </summary>
    public static class NotificationQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "notifications";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, kind, title, body, topicid, runid, briefingid, mediaassetid, predictionid, readutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS notifications (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    kind VARCHAR(64) NOT NULL,
    title VARCHAR(256) NOT NULL,
    body TEXT,
    topicid VARCHAR(64),
    runid VARCHAR(64),
    briefingid VARCHAR(64),
    mediaassetid VARCHAR(64),
    predictionid VARCHAR(64),
    readutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_notifications_tenantid ON notifications (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_notifications_createdutc ON notifications (createdutc)",
            @"CREATE INDEX IF NOT EXISTS ix_notifications_userid_createdutc ON notifications (userid, createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO notifications (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE notifications SET userid = $3, kind = $4, title = $5, body = $6, topicid = $7, runid = $8, briefingid = $9, mediaassetid = $10, predictionid = $11, readutc = $12, active = $13, lastupdateutc = $14 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM notifications WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM notifications WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM notifications WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM notifications WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM notifications WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
