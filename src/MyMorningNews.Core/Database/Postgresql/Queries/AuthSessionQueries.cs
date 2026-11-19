namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the authsessions table.
    /// </summary>
    public static class AuthSessionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "authsessions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, credentialid, principaltype, authscheme, tokenid, sourceip, useragent, expiresutc, lastusedutc, revokedutc, revocationreason, isprotected, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS authsessions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) REFERENCES users(id) ON DELETE CASCADE,
    credentialid VARCHAR(64) REFERENCES credentials(id) ON DELETE CASCADE,
    principaltype VARCHAR(64) NOT NULL,
    authscheme VARCHAR(64) NOT NULL,
    tokenid VARCHAR(64) NOT NULL,
    sourceip VARCHAR(64),
    useragent TEXT,
    expiresutc TIMESTAMPTZ NOT NULL,
    lastusedutc TIMESTAMPTZ,
    revokedutc TIMESTAMPTZ,
    revocationreason VARCHAR(256),
    isprotected BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_authsessions_tenantid ON authsessions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_authsessions_credentialid ON authsessions (credentialid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_authsessions_tenantid_tokenid ON authsessions (tenantid, tokenid)",
            @"CREATE INDEX IF NOT EXISTS ix_authsessions_createdutc ON authsessions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO authsessions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE authsessions SET userid = $3, credentialid = $4, principaltype = $5, authscheme = $6, tokenid = $7, sourceip = $8, useragent = $9, expiresutc = $10, lastusedutc = $11, revokedutc = $12, revocationreason = $13, isprotected = $14, active = $15, lastupdateutc = $16 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM authsessions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM authsessions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM authsessions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM authsessions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM authsessions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
