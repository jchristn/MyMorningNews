namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the feedtokens table.
    /// </summary>
    public static class FeedTokenQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "feedtokens";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, editionid, name, tokenhash, tokenprefix, lastusedutc, revokedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS feedtokens (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    editionid VARCHAR(64),
    name VARCHAR(128) NOT NULL,
    tokenhash VARCHAR(64) NOT NULL,
    tokenprefix VARCHAR(16) NOT NULL,
    lastusedutc TIMESTAMPTZ,
    revokedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_feedtokens_tenantid ON feedtokens (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_feedtokens_tenantid_tokenhash ON feedtokens (tenantid, tokenhash)",
            @"CREATE INDEX IF NOT EXISTS ix_feedtokens_createdutc ON feedtokens (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO feedtokens (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE feedtokens SET userid = $3, editionid = $4, name = $5, tokenhash = $6, tokenprefix = $7, lastusedutc = $8, revokedutc = $9, active = $10, lastupdateutc = $11 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM feedtokens WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM feedtokens WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM feedtokens WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM feedtokens WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM feedtokens WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
