namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the editions table.
    /// </summary>
    public static class EditionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "editions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, name, description, briefingtemplateid, voiceprofileid, audioenabled, emailenabled, enabled, freshresearchbeforecompose, freshresearchleadminutes, lookbackmode, lookbackhours, lastcomposedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS editions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    name VARCHAR(128) NOT NULL,
    description TEXT,
    briefingtemplateid VARCHAR(64),
    voiceprofileid VARCHAR(64),
    audioenabled BOOLEAN NOT NULL,
    emailenabled BOOLEAN NOT NULL,
    enabled BOOLEAN NOT NULL,
    freshresearchbeforecompose BOOLEAN NOT NULL,
    freshresearchleadminutes INTEGER NOT NULL,
    lookbackmode VARCHAR(64) NOT NULL,
    lookbackhours INTEGER NOT NULL,
    lastcomposedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_editions_tenantid ON editions (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_editions_tenantid_userid_name ON editions (tenantid, userid, name)",
            @"CREATE INDEX IF NOT EXISTS ix_editions_createdutc ON editions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO editions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE editions SET userid = $3, name = $4, description = $5, briefingtemplateid = $6, voiceprofileid = $7, audioenabled = $8, emailenabled = $9, enabled = $10, freshresearchbeforecompose = $11, freshresearchleadminutes = $12, lookbackmode = $13, lookbackhours = $14, lastcomposedutc = $15, active = $16, lastupdateutc = $17 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM editions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM editions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM editions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM editions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM editions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
