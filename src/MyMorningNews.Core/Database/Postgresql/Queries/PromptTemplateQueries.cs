namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the prompttemplates table.
    /// </summary>
    public static class PromptTemplateQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "prompttemplates";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, key, topicid, name, description, role, outputtype, currentversion, iscustomized, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS prompttemplates (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    key VARCHAR(128) NOT NULL,
    topicid VARCHAR(64),
    name VARCHAR(256) NOT NULL,
    description TEXT,
    role VARCHAR(64) NOT NULL,
    outputtype VARCHAR(128),
    currentversion INTEGER NOT NULL,
    iscustomized BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_prompttemplates_tenantid ON prompttemplates (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_prompttemplates_createdutc ON prompttemplates (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO prompttemplates (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE prompttemplates SET key = $3, topicid = $4, name = $5, description = $6, role = $7, outputtype = $8, currentversion = $9, iscustomized = $10, active = $11, lastupdateutc = $12 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM prompttemplates WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM prompttemplates WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM prompttemplates WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM prompttemplates WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM prompttemplates WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
