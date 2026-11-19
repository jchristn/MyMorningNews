namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the vaultcredentials table.
    /// </summary>
    public static class VaultCredentialQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "vaultcredentials";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, name, kind, provider, headername, username, secretencrypted, secretlast4, description, createdbyuserid, lastusedutc, lastrevealedutc, expiresutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS vaultcredentials (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(128) NOT NULL,
    kind VARCHAR(64) NOT NULL,
    provider VARCHAR(64) NOT NULL,
    headername VARCHAR(128),
    username VARCHAR(256),
    secretencrypted TEXT NOT NULL,
    secretlast4 VARCHAR(8) NOT NULL,
    description TEXT,
    createdbyuserid VARCHAR(64),
    lastusedutc TIMESTAMPTZ,
    lastrevealedutc TIMESTAMPTZ,
    expiresutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_vaultcredentials_tenantid ON vaultcredentials (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_vaultcredentials_tenantid_name ON vaultcredentials (tenantid, name)",
            @"CREATE INDEX IF NOT EXISTS ix_vaultcredentials_createdutc ON vaultcredentials (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO vaultcredentials (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE vaultcredentials SET name = $3, kind = $4, provider = $5, headername = $6, username = $7, secretencrypted = $8, secretlast4 = $9, description = $10, createdbyuserid = $11, lastusedutc = $12, lastrevealedutc = $13, expiresutc = $14, active = $15, lastupdateutc = $16 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM vaultcredentials WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM vaultcredentials WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM vaultcredentials WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM vaultcredentials WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM vaultcredentials WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
