namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the topicsearchproviders table.
    /// </summary>
    public static class TopicSearchProviderQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "topicsearchproviders";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, topicid, searchproviderid, enabled, maxresults, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS topicsearchproviders (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    topicid VARCHAR(64) NOT NULL REFERENCES topics(id) ON DELETE CASCADE,
    searchproviderid VARCHAR(64) NOT NULL REFERENCES searchproviders(id) ON DELETE CASCADE,
    enabled BOOLEAN NOT NULL,
    maxresults INTEGER,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_topicsearchproviders_tenantid ON topicsearchproviders (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_topicsearchproviders_topicid ON topicsearchproviders (topicid)",
            @"CREATE INDEX IF NOT EXISTS ix_topicsearchproviders_searchproviderid ON topicsearchproviders (searchproviderid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_topicsearchproviders_topicid_searchproviderid ON topicsearchproviders (topicid, searchproviderid)",
            @"CREATE INDEX IF NOT EXISTS ix_topicsearchproviders_createdutc ON topicsearchproviders (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO topicsearchproviders (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE topicsearchproviders SET topicid = $3, searchproviderid = $4, enabled = $5, maxresults = $6, active = $7, lastupdateutc = $8 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM topicsearchproviders WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM topicsearchproviders WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM topicsearchproviders WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM topicsearchproviders WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM topicsearchproviders WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
