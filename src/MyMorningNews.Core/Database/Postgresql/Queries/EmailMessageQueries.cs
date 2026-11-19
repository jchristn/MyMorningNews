namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the emailmessages table.
    /// </summary>
    public static class EmailMessageQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "emailmessages";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, template, runid, briefingid, topicid, recipient, subject, status, provider, providermessageid, attempts, nextattemptutc, lockeduntilutc, lasterror, idempotencykey, renderedhtml, renderedtext, sentutc, failedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS emailmessages (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64),
    template VARCHAR(64) NOT NULL,
    runid VARCHAR(64),
    briefingid VARCHAR(64),
    topicid VARCHAR(64),
    recipient VARCHAR(320) NOT NULL,
    subject VARCHAR(512) NOT NULL,
    status VARCHAR(64) NOT NULL,
    provider VARCHAR(64) NOT NULL,
    providermessageid VARCHAR(256),
    attempts INTEGER NOT NULL,
    nextattemptutc TIMESTAMPTZ,
    lockeduntilutc TIMESTAMPTZ,
    lasterror TEXT,
    idempotencykey VARCHAR(256) NOT NULL,
    renderedhtml TEXT,
    renderedtext TEXT,
    sentutc TIMESTAMPTZ,
    failedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_emailmessages_tenantid ON emailmessages (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_emailmessages_tenantid_idempotencykey ON emailmessages (tenantid, idempotencykey)",
            @"CREATE INDEX IF NOT EXISTS ix_emailmessages_createdutc ON emailmessages (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO emailmessages (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE emailmessages SET userid = $3, template = $4, runid = $5, briefingid = $6, topicid = $7, recipient = $8, subject = $9, status = $10, provider = $11, providermessageid = $12, attempts = $13, nextattemptutc = $14, lockeduntilutc = $15, lasterror = $16, idempotencykey = $17, renderedhtml = $18, renderedtext = $19, sentutc = $20, failedutc = $21, active = $22, lastupdateutc = $23 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM emailmessages WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM emailmessages WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM emailmessages WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM emailmessages WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM emailmessages WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
