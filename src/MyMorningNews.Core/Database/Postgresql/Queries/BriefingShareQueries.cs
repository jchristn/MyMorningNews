namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the briefingshares table.
    /// </summary>
    public static class BriefingShareQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "briefingshares";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, briefingid, tokenhash, tokenprefix, showownername, expiresutc, revokedutc, viewcount, lastviewedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent. Deleting the briefing, the user, or the tenant removes the share.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS briefingshares (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    briefingid VARCHAR(64) NOT NULL REFERENCES briefings(id) ON DELETE CASCADE,
    tokenhash VARCHAR(64) NOT NULL,
    tokenprefix VARCHAR(16) NOT NULL,
    showownername BOOLEAN NOT NULL,
    expiresutc TIMESTAMPTZ NOT NULL,
    revokedutc TIMESTAMPTZ,
    viewcount BIGINT NOT NULL,
    lastviewedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingshares_tenantid ON briefingshares (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_briefingshares_tokenhash ON briefingshares (tokenhash)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingshares_briefingid ON briefingshares (briefingid)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingshares_tenantid_userid ON briefingshares (tenantid, userid)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingshares_createdutc ON briefingshares (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO briefingshares (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns. The view counter is changed only by <see cref="RecordView"/>.
        /// </summary>
        public const string Update = "UPDATE briefingshares SET showownername = $3, expiresutc = $4, revokedutc = $5, active = $6, lastupdateutc = $7 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM briefingshares WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM briefingshares WHERE id = $1";

        /// <summary>
        /// Select by token hash without tenant scoping (public requests carry no tenant).
        /// </summary>
        public const string SelectByHash = "SELECT " + Columns + " FROM briefingshares WHERE tokenhash = $1 LIMIT 1";

        /// <summary>
        /// Add one view and stamp the time, atomically. $1 is id, $2 is the time.
        /// </summary>
        public const string RecordView = "UPDATE briefingshares SET viewcount = viewcount + 1, lastviewedutc = $2 WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM briefingshares WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM briefingshares WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM briefingshares WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
