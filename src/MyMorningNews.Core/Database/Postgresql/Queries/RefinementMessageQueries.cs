namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the refinementmessages table.
    /// </summary>
    public static class RefinementMessageQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "refinementmessages";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, sessionid, role, content, llmcallid, sequence, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS refinementmessages (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    sessionid VARCHAR(64) NOT NULL REFERENCES refinementsessions(id) ON DELETE CASCADE,
    role VARCHAR(64) NOT NULL,
    content TEXT NOT NULL,
    llmcallid VARCHAR(64),
    sequence INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementmessages_tenantid ON refinementmessages (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementmessages_sessionid ON refinementmessages (sessionid)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementmessages_createdutc ON refinementmessages (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO refinementmessages (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE refinementmessages SET sessionid = $3, role = $4, content = $5, llmcallid = $6, sequence = $7, active = $8, lastupdateutc = $9 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM refinementmessages WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM refinementmessages WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM refinementmessages WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM refinementmessages WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM refinementmessages WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
