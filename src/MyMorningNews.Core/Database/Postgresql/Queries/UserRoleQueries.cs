namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the userroles table.
    /// </summary>
    public static class UserRoleQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "userroles";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, name, description, isbuiltin, isprotected, defaultscope, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS userroles (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(128) NOT NULL,
    description TEXT,
    isbuiltin BOOLEAN NOT NULL,
    isprotected BOOLEAN NOT NULL,
    defaultscope VARCHAR(64) NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_userroles_tenantid ON userroles (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_userroles_tenantid_name ON userroles (tenantid, name)",
            @"CREATE INDEX IF NOT EXISTS ix_userroles_createdutc ON userroles (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO userroles (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE userroles SET name = $3, description = $4, isbuiltin = $5, isprotected = $6, defaultscope = $7, active = $8, lastupdateutc = $9 WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM userroles WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM userroles WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM userroles WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM userroles WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM userroles WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2 LIMIT 1";
    }
}
