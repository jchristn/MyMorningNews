namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the accountplanpolicies table.
    /// </summary>
    public static class AccountPlanPolicyQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "accountplanpolicies";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, subscriptionrequired, defaultplanid, fundingmode, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS accountplanpolicies (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    subscriptionrequired BOOLEAN NOT NULL,
    defaultplanid VARCHAR(64) NOT NULL REFERENCES plans(id) ON DELETE RESTRICT,
    fundingmode VARCHAR(32) NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_accountplanpolicies_tenantid ON accountplanpolicies (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_accountplanpolicies_tenantid_userid ON accountplanpolicies (tenantid, userid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_accountplanpolicies_tenantid_userid ON accountplanpolicies (tenantid, userid)",
            @"CREATE INDEX IF NOT EXISTS ix_accountplanpolicies_defaultplanid ON accountplanpolicies (defaultplanid)",
            @"CREATE INDEX IF NOT EXISTS ix_accountplanpolicies_createdutc ON accountplanpolicies (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO accountplanpolicies (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE accountplanpolicies SET userid = $3, subscriptionrequired = $4, defaultplanid = $5, fundingmode = $6, active = $7, lastupdateutc = $8 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM accountplanpolicies WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM accountplanpolicies WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM accountplanpolicies WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM accountplanpolicies WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM accountplanpolicies WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
