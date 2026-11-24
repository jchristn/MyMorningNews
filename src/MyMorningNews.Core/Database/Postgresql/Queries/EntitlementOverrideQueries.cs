namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the entitlementoverrides table.
    /// </summary>
    public static class EntitlementOverrideQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "entitlementoverrides";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, featurekey, value, reason, grantedbyuserid, expiresutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS entitlementoverrides (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    featurekey VARCHAR(128) NOT NULL,
    value VARCHAR(128) NOT NULL,
    reason TEXT NOT NULL,
    grantedbyuserid VARCHAR(64),
    expiresutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_entitlementoverrides_tenantid ON entitlementoverrides (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_entitlementoverrides_tenantid_userid ON entitlementoverrides (tenantid, userid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_entitlementoverrides_tenantid_userid_featurekey ON entitlementoverrides (tenantid, userid, featurekey)",
            @"CREATE INDEX IF NOT EXISTS ix_entitlementoverrides_createdutc ON entitlementoverrides (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO entitlementoverrides (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE entitlementoverrides SET userid = $3, featurekey = $4, value = $5, reason = $6, grantedbyuserid = $7, expiresutc = $8, active = $9, lastupdateutc = $10 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM entitlementoverrides WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM entitlementoverrides WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM entitlementoverrides WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM entitlementoverrides WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM entitlementoverrides WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
