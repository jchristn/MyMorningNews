namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the mcpservers table.
    /// </summary>
    public static class McpServerQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "mcpservers";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, name, url, transport, vaultcredentialid, headername, headerprefix, enabled, toolcount, lastdiscoveredutc, lasterror, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS mcpservers (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(128) NOT NULL,
    url VARCHAR(2048) NOT NULL,
    transport VARCHAR(64) NOT NULL,
    vaultcredentialid VARCHAR(64),
    headername VARCHAR(128) NOT NULL,
    headerprefix VARCHAR(64) NOT NULL,
    enabled BOOLEAN NOT NULL,
    toolcount INTEGER NOT NULL,
    lastdiscoveredutc TIMESTAMPTZ,
    lasterror TEXT,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_mcpservers_tenantid ON mcpservers (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_mcpservers_createdutc ON mcpservers (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO mcpservers (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE mcpservers SET name = $3, url = $4, transport = $5, vaultcredentialid = $6, headername = $7, headerprefix = $8, enabled = $9, toolcount = $10, lastdiscoveredutc = $11, lasterror = $12, active = $13, lastupdateutc = $14 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM mcpservers WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM mcpservers WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM mcpservers WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM mcpservers WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM mcpservers WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
