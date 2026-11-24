namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the billingsubscriptions table.
    /// </summary>
    public static class BillingSubscriptionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "billingsubscriptions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, provider, environment, providerid, customerid, planid, status, endsutc, cancelatperiodend, lastverifiedutc, active, createdutc, lastupdateutc, verificationsecretencrypted";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS billingsubscriptions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    provider VARCHAR(16) NOT NULL,
    environment VARCHAR(16) NOT NULL,
    providerid VARCHAR(256) NOT NULL,
    customerid VARCHAR(256),
    planid VARCHAR(64) NOT NULL REFERENCES plans(id) ON DELETE RESTRICT,
    status VARCHAR(32) NOT NULL,
    endsutc TIMESTAMPTZ,
    cancelatperiodend BOOLEAN NOT NULL,
    lastverifiedutc TIMESTAMPTZ NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL,
    verificationsecretencrypted TEXT
)",
            @"CREATE INDEX IF NOT EXISTS ix_billingsubscriptions_tenantid ON billingsubscriptions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_billingsubscriptions_tenantid_userid ON billingsubscriptions (tenantid, userid)",
            @"CREATE INDEX IF NOT EXISTS ix_billingsubscriptions_planid ON billingsubscriptions (planid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_billingsubscriptions_provider_environment_providerid ON billingsubscriptions (provider, environment, providerid)",
            @"CREATE INDEX IF NOT EXISTS ix_billingsubscriptions_createdutc ON billingsubscriptions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO billingsubscriptions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE billingsubscriptions SET userid = $3, provider = $4, environment = $5, providerid = $6, customerid = $7, planid = $8, status = $9, endsutc = $10, cancelatperiodend = $11, lastverifiedutc = $12, active = $13, lastupdateutc = $14, verificationsecretencrypted = $15 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM billingsubscriptions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM billingsubscriptions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM billingsubscriptions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM billingsubscriptions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM billingsubscriptions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
