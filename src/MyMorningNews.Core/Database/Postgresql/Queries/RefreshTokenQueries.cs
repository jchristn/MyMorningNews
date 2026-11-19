namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the refreshtokens table.
    /// </summary>
    public static class RefreshTokenQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "refreshtokens";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, sessionid, familyid, tokenhash, client, sourceip, useragent, expiresutc, usedutc, revokedutc, revocationreason, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS refreshtokens (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    sessionid VARCHAR(64),
    familyid VARCHAR(64) NOT NULL,
    tokenhash VARCHAR(128) NOT NULL,
    client VARCHAR(32) NOT NULL,
    sourceip VARCHAR(64),
    useragent TEXT,
    expiresutc TIMESTAMPTZ NOT NULL,
    usedutc TIMESTAMPTZ,
    revokedutc TIMESTAMPTZ,
    revocationreason VARCHAR(256),
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_refreshtokens_tenantid ON refreshtokens (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_refreshtokens_familyid ON refreshtokens (familyid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_refreshtokens_tenantid_tokenhash ON refreshtokens (tenantid, tokenhash)",
            @"CREATE INDEX IF NOT EXISTS ix_refreshtokens_createdutc ON refreshtokens (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO refreshtokens (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE refreshtokens SET userid = $3, sessionid = $4, familyid = $5, tokenhash = $6, client = $7, sourceip = $8, useragent = $9, expiresutc = $10, usedutc = $11, revokedutc = $12, revocationreason = $13, active = $14, lastupdateutc = $15 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM refreshtokens WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM refreshtokens WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM refreshtokens WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM refreshtokens WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM refreshtokens WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
