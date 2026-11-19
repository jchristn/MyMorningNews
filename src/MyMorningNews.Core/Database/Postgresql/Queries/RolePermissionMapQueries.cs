namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the rolepermissionmaps table.
    /// </summary>
    public static class RolePermissionMapQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "rolepermissionmaps";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, roleid, permissionid, isprotected, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS rolepermissionmaps (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) REFERENCES tenants(id) ON DELETE CASCADE,
    roleid VARCHAR(64) NOT NULL REFERENCES userroles(id) ON DELETE CASCADE,
    permissionid VARCHAR(64) NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    isprotected BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_rolepermissionmaps_tenantid ON rolepermissionmaps (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_rolepermissionmaps_roleid ON rolepermissionmaps (roleid)",
            @"CREATE INDEX IF NOT EXISTS ix_rolepermissionmaps_permissionid ON rolepermissionmaps (permissionid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_rolepermissionmaps_roleid_permissionid ON rolepermissionmaps (roleid, permissionid)",
            @"CREATE INDEX IF NOT EXISTS ix_rolepermissionmaps_createdutc ON rolepermissionmaps (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO rolepermissionmaps (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE rolepermissionmaps SET roleid = $3, permissionid = $4, isprotected = $5, active = $6, lastupdateutc = $7 WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM rolepermissionmaps WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM rolepermissionmaps WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM rolepermissionmaps WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM rolepermissionmaps WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM rolepermissionmaps WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2 LIMIT 1";
    }
}
