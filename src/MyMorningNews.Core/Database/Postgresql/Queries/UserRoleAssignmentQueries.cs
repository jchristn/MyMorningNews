namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the userroleassignments table.
    /// </summary>
    public static class UserRoleAssignmentQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "userroleassignments";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, roleid, rolename, resourcescope, resourceid, inheritstochildren, isprotected, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS userroleassignments (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    roleid VARCHAR(64) REFERENCES userroles(id) ON DELETE CASCADE,
    rolename VARCHAR(128),
    resourcescope VARCHAR(64) NOT NULL,
    resourceid VARCHAR(64),
    inheritstochildren BOOLEAN NOT NULL,
    isprotected BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_userroleassignments_tenantid ON userroleassignments (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_userroleassignments_roleid ON userroleassignments (roleid)",
            @"CREATE INDEX IF NOT EXISTS ix_userroleassignments_resourceid ON userroleassignments (resourceid)",
            @"CREATE INDEX IF NOT EXISTS ix_userroleassignments_createdutc ON userroleassignments (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO userroleassignments (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE userroleassignments SET userid = $3, roleid = $4, rolename = $5, resourcescope = $6, resourceid = $7, inheritstochildren = $8, isprotected = $9, active = $10, lastupdateutc = $11 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM userroleassignments WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM userroleassignments WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM userroleassignments WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM userroleassignments WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM userroleassignments WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
