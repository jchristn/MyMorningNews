namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the credentials table.
    /// </summary>
    public static class CredentialQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "credentials";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, name, accesskey, secretkeyencrypted, secretkeylast4, authmode, scopes, resourceids, lastusedutc, expiresutc, isprotected, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS credentials (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(128) NOT NULL,
    accesskey VARCHAR(64) NOT NULL,
    secretkeyencrypted TEXT NOT NULL,
    secretkeylast4 VARCHAR(8) NOT NULL,
    authmode VARCHAR(64) NOT NULL,
    scopes TEXT[] NOT NULL,
    resourceids TEXT[] NOT NULL,
    lastusedutc TIMESTAMPTZ,
    expiresutc TIMESTAMPTZ,
    isprotected BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_credentials_tenantid ON credentials (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_credentials_tenantid_userid ON credentials (tenantid, userid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_credentials_tenantid_accesskey ON credentials (tenantid, accesskey)",
            @"CREATE INDEX IF NOT EXISTS ix_credentials_createdutc ON credentials (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO credentials (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE credentials SET userid = $3, name = $4, accesskey = $5, secretkeyencrypted = $6, secretkeylast4 = $7, authmode = $8, scopes = $9, resourceids = $10, lastusedutc = $11, expiresutc = $12, isprotected = $13, active = $14, lastupdateutc = $15 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM credentials WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM credentials WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM credentials WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM credentials WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM credentials WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
