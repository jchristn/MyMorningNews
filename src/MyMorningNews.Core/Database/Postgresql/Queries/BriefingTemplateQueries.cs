namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the briefingtemplates table.
    /// </summary>
    public static class BriefingTemplateQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "briefingtemplates";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, name, description, defaultkey, isdefault, currentversion, customized, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS briefingtemplates (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64),
    name VARCHAR(128) NOT NULL,
    description TEXT,
    defaultkey VARCHAR(64),
    isdefault BOOLEAN NOT NULL,
    currentversion INTEGER NOT NULL,
    customized BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingtemplates_tenantid ON briefingtemplates (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_briefingtemplates_tenantid_userid_name ON briefingtemplates (tenantid, userid, name)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingtemplates_createdutc ON briefingtemplates (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO briefingtemplates (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE briefingtemplates SET userid = $3, name = $4, description = $5, defaultkey = $6, isdefault = $7, currentversion = $8, customized = $9, active = $10, lastupdateutc = $11 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM briefingtemplates WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM briefingtemplates WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM briefingtemplates WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM briefingtemplates WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM briefingtemplates WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
