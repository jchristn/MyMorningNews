namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the emailsuppressions table.
    /// </summary>
    public static class EmailSuppressionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "emailsuppressions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, recipient, category, topicid, source, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS emailsuppressions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    recipient VARCHAR(320) NOT NULL,
    category VARCHAR(64) NOT NULL,
    topicid VARCHAR(64),
    source VARCHAR(32) NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_emailsuppressions_tenantid ON emailsuppressions (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_emailsuppressions_tenantid_recipient_category_topicid ON emailsuppressions (tenantid, recipient, category, topicid)",
            @"CREATE INDEX IF NOT EXISTS ix_emailsuppressions_createdutc ON emailsuppressions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO emailsuppressions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE emailsuppressions SET recipient = $3, category = $4, topicid = $5, source = $6, active = $7, lastupdateutc = $8 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM emailsuppressions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM emailsuppressions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM emailsuppressions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM emailsuppressions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM emailsuppressions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
