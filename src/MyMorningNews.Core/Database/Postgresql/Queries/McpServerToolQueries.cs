namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the mcpservertools table.
    /// </summary>
    public static class McpServerToolQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "mcpservertools";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, mcpserverid, name, description, inputschema, enabled, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS mcpservertools (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    mcpserverid VARCHAR(64) NOT NULL REFERENCES mcpservers(id) ON DELETE CASCADE,
    name VARCHAR(256) NOT NULL,
    description TEXT,
    inputschema TEXT,
    enabled BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_mcpservertools_tenantid ON mcpservertools (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_mcpservertools_mcpserverid ON mcpservertools (mcpserverid)",
            @"CREATE INDEX IF NOT EXISTS ix_mcpservertools_createdutc ON mcpservertools (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO mcpservertools (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE mcpservertools SET mcpserverid = $3, name = $4, description = $5, inputschema = $6, enabled = $7, active = $8, lastupdateutc = $9 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM mcpservertools WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM mcpservertools WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM mcpservertools WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM mcpservertools WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM mcpservertools WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
