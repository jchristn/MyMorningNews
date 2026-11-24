namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the billingcheckouts table.
    /// </summary>
    public static class BillingCheckoutQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "billingcheckouts";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, offerkey, sessionid, checkouturl, expiresutc, status, active, createdutc, lastupdateutc, priceid, customeremail, returnurl, signupbenefitid";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS billingcheckouts (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    offerkey VARCHAR(128) NOT NULL,
    sessionid VARCHAR(256),
    checkouturl TEXT,
    expiresutc TIMESTAMPTZ NOT NULL,
    status VARCHAR(32) NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_billingcheckouts_tenantid ON billingcheckouts (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_billingcheckouts_tenantid_userid ON billingcheckouts (tenantid, userid)",
            @"CREATE INDEX IF NOT EXISTS ix_billingcheckouts_createdutc ON billingcheckouts (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO billingcheckouts (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE billingcheckouts SET userid = $3, offerkey = $4, sessionid = $5, checkouturl = $6, expiresutc = $7, status = $8, active = $9, lastupdateutc = $10, priceid = $11, customeremail = $12, returnurl = $13, signupbenefitid = $14 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM billingcheckouts WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM billingcheckouts WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM billingcheckouts WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM billingcheckouts WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM billingcheckouts WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
