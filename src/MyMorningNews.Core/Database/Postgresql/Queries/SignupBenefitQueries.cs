namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the signupbenefits table.
    /// </summary>
    public static class SignupBenefitQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "signupbenefits";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, promocodeid, planid, discountpercent, discountmonths, redeemedutc, revokedutc, active, createdutc, lastupdateutc, billingplankey, durationdays, redeemedsubscriptionid, appleofferid, googleofferid";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS signupbenefits (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    promocodeid VARCHAR(64) NOT NULL REFERENCES promocodes(id) ON DELETE RESTRICT,
    planid VARCHAR(64) NOT NULL REFERENCES plans(id) ON DELETE RESTRICT,
    discountpercent NUMERIC(18,6) NOT NULL,
    discountmonths INTEGER,
    redeemedutc TIMESTAMPTZ,
    revokedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_signupbenefits_tenantid ON signupbenefits (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_signupbenefits_tenantid_userid ON signupbenefits (tenantid, userid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_signupbenefits_tenantid_userid ON signupbenefits (tenantid, userid)",
            @"CREATE INDEX IF NOT EXISTS ix_signupbenefits_promocodeid ON signupbenefits (promocodeid)",
            @"CREATE INDEX IF NOT EXISTS ix_signupbenefits_planid ON signupbenefits (planid)",
            @"CREATE INDEX IF NOT EXISTS ix_signupbenefits_createdutc ON signupbenefits (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO signupbenefits (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE signupbenefits SET userid = $3, promocodeid = $4, planid = $5, discountpercent = $6, discountmonths = $7, redeemedutc = $8, revokedutc = $9, active = $10, lastupdateutc = $11, billingplankey = $12, durationdays = $13, redeemedsubscriptionid = $14, appleofferid = $15, googleofferid = $16 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM signupbenefits WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM signupbenefits WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM signupbenefits WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM signupbenefits WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM signupbenefits WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
