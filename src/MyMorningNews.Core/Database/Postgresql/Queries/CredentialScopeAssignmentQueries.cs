namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the credentialscopeassignments table.
    /// </summary>
    public static class CredentialScopeAssignmentQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "credentialscopeassignments";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, credentialid, roleid, rolename, resourcescope, resourceid, operationtypes, resourcetypes, isprotected, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS credentialscopeassignments (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    credentialid VARCHAR(64) NOT NULL REFERENCES credentials(id) ON DELETE CASCADE,
    roleid VARCHAR(64) REFERENCES userroles(id) ON DELETE CASCADE,
    rolename VARCHAR(128),
    resourcescope VARCHAR(64) NOT NULL,
    resourceid VARCHAR(64),
    operationtypes TEXT[] NOT NULL,
    resourcetypes TEXT[] NOT NULL,
    isprotected BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_credentialscopeassignments_tenantid ON credentialscopeassignments (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_credentialscopeassignments_credentialid ON credentialscopeassignments (credentialid)",
            @"CREATE INDEX IF NOT EXISTS ix_credentialscopeassignments_roleid ON credentialscopeassignments (roleid)",
            @"CREATE INDEX IF NOT EXISTS ix_credentialscopeassignments_createdutc ON credentialscopeassignments (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO credentialscopeassignments (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE credentialscopeassignments SET credentialid = $3, roleid = $4, rolename = $5, resourcescope = $6, resourceid = $7, operationtypes = $8, resourcetypes = $9, isprotected = $10, active = $11, lastupdateutc = $12 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM credentialscopeassignments WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM credentialscopeassignments WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM credentialscopeassignments WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM credentialscopeassignments WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM credentialscopeassignments WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
