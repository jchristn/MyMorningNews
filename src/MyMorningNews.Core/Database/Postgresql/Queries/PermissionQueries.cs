namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the permissions table.
    /// </summary>
    public static class PermissionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "permissions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, name, resourcetypes, operationtypes, permissiontype, isbuiltin, isprotected, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS permissions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(128) NOT NULL,
    resourcetypes TEXT[] NOT NULL,
    operationtypes TEXT[] NOT NULL,
    permissiontype VARCHAR(64) NOT NULL,
    isbuiltin BOOLEAN NOT NULL,
    isprotected BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_permissions_tenantid ON permissions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_permissions_createdutc ON permissions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO permissions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE permissions SET name = $3, resourcetypes = $4, operationtypes = $5, permissiontype = $6, isbuiltin = $7, isprotected = $8, active = $9, lastupdateutc = $10 WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM permissions WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM permissions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM permissions WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM permissions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM permissions WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2 LIMIT 1";
    }
}
