namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the prompttemplateversions table.
    /// </summary>
    public static class PromptTemplateVersionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "prompttemplateversions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, prompttemplateid, version, content, changenote, createdbyuserid, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS prompttemplateversions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    prompttemplateid VARCHAR(64) NOT NULL REFERENCES prompttemplates(id) ON DELETE CASCADE,
    version INTEGER NOT NULL,
    content TEXT NOT NULL,
    changenote TEXT,
    createdbyuserid VARCHAR(64),
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_prompttemplateversions_tenantid ON prompttemplateversions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_prompttemplateversions_prompttemplateid ON prompttemplateversions (prompttemplateid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_prompttemplateversions_prompttemplateid_version ON prompttemplateversions (prompttemplateid, version)",
            @"CREATE INDEX IF NOT EXISTS ix_prompttemplateversions_createdutc ON prompttemplateversions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO prompttemplateversions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE prompttemplateversions SET prompttemplateid = $3, version = $4, content = $5, changenote = $6, createdbyuserid = $7, active = $8, lastupdateutc = $9 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM prompttemplateversions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM prompttemplateversions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM prompttemplateversions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM prompttemplateversions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM prompttemplateversions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
