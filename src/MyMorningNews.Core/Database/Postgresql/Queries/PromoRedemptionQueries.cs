namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the promoredemptions table.
    /// </summary>
    public static class PromoRedemptionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "promoredemptions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, promocodeid, code, userid, email, planassignmentid, channel, revokedutc, active, createdutc, lastupdateutc, signupbenefitid";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS promoredemptions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    promocodeid VARCHAR(64) NOT NULL,
    code VARCHAR(40) NOT NULL,
    userid VARCHAR(64) NOT NULL,
    email VARCHAR(320),
    planassignmentid VARCHAR(64) NOT NULL,
    channel VARCHAR(64) NOT NULL,
    revokedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_promoredemptions_tenantid ON promoredemptions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_promoredemptions_createdutc ON promoredemptions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO promoredemptions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE promoredemptions SET promocodeid = $3, code = $4, userid = $5, email = $6, planassignmentid = $7, channel = $8, revokedutc = $9, active = $10, lastupdateutc = $11, signupbenefitid = $12 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM promoredemptions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM promoredemptions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM promoredemptions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM promoredemptions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM promoredemptions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
