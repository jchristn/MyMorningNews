namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the providerconnections table.
    /// </summary>
    public static class ProviderConnectionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "providerconnections";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, kind, provider, endpoint, model, voice, secretencrypted, secretlast4, validatedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS providerconnections (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    kind VARCHAR(16) NOT NULL,
    provider VARCHAR(32) NOT NULL,
    endpoint TEXT,
    model VARCHAR(256) NOT NULL,
    voice VARCHAR(128),
    secretencrypted TEXT,
    secretlast4 VARCHAR(4),
    validatedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_providerconnections_tenantid ON providerconnections (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_providerconnections_tenantid_userid ON providerconnections (tenantid, userid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_providerconnections_tenantid_userid_kind ON providerconnections (tenantid, userid, kind)",
            @"CREATE INDEX IF NOT EXISTS ix_providerconnections_createdutc ON providerconnections (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO providerconnections (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE providerconnections SET userid = $3, kind = $4, provider = $5, endpoint = $6, model = $7, voice = $8, secretencrypted = $9, secretlast4 = $10, validatedutc = $11, active = $12, lastupdateutc = $13 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM providerconnections WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM providerconnections WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM providerconnections WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM providerconnections WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM providerconnections WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
