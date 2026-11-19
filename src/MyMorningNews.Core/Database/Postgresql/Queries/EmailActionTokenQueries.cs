namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the emailactiontokens table.
    /// </summary>
    public static class EmailActionTokenQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "emailactiontokens";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, emailmessageid, kind, targettype, targetid, topicid, recipient, allowedactions, tokenhash, expiresutc, revokedutc, opencount, submitcount, lastusedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS emailactiontokens (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    emailmessageid VARCHAR(64) NOT NULL,
    kind VARCHAR(64) NOT NULL,
    targettype VARCHAR(64) NOT NULL,
    targetid VARCHAR(128) NOT NULL,
    topicid VARCHAR(64),
    recipient VARCHAR(320) NOT NULL,
    allowedactions TEXT[] NOT NULL,
    tokenhash VARCHAR(64) NOT NULL,
    expiresutc TIMESTAMPTZ NOT NULL,
    revokedutc TIMESTAMPTZ,
    opencount INTEGER NOT NULL,
    submitcount INTEGER NOT NULL,
    lastusedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_emailactiontokens_tenantid ON emailactiontokens (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_emailactiontokens_tenantid_tokenhash ON emailactiontokens (tenantid, tokenhash)",
            @"CREATE INDEX IF NOT EXISTS ix_emailactiontokens_createdutc ON emailactiontokens (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO emailactiontokens (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE emailactiontokens SET userid = $3, emailmessageid = $4, kind = $5, targettype = $6, targetid = $7, topicid = $8, recipient = $9, allowedactions = $10, tokenhash = $11, expiresutc = $12, revokedutc = $13, opencount = $14, submitcount = $15, lastusedutc = $16, active = $17, lastupdateutc = $18 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM emailactiontokens WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM emailactiontokens WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM emailactiontokens WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM emailactiontokens WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM emailactiontokens WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
