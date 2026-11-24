namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the billingaccounts table.
    /// </summary>
    public static class BillingAccountQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "billingaccounts";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, purchaseaccounttoken, stripecustomerid, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS billingaccounts (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    purchaseaccounttoken VARCHAR(64) NOT NULL,
    stripecustomerid VARCHAR(128),
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_billingaccounts_tenantid ON billingaccounts (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_billingaccounts_tenantid_userid ON billingaccounts (tenantid, userid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_billingaccounts_tenantid_userid ON billingaccounts (tenantid, userid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_billingaccounts_purchaseaccounttoken ON billingaccounts (purchaseaccounttoken)",
            @"CREATE INDEX IF NOT EXISTS ix_billingaccounts_createdutc ON billingaccounts (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO billingaccounts (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE billingaccounts SET userid = $3, purchaseaccounttoken = $4, stripecustomerid = $5, active = $6, lastupdateutc = $7 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM billingaccounts WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM billingaccounts WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM billingaccounts WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM billingaccounts WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM billingaccounts WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
