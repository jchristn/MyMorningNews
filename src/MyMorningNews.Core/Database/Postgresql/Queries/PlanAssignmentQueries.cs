namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the planassignments table.
    /// </summary>
    public static class PlanAssignmentQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "planassignments";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, planid, source, promocodeid, startsutc, endsutc, revokedutc, note, grantedbyuserid, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS planassignments (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    planid VARCHAR(64) NOT NULL,
    source VARCHAR(64) NOT NULL,
    promocodeid VARCHAR(64),
    startsutc TIMESTAMPTZ NOT NULL,
    endsutc TIMESTAMPTZ,
    revokedutc TIMESTAMPTZ,
    note TEXT,
    grantedbyuserid VARCHAR(64),
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_planassignments_tenantid ON planassignments (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_planassignments_createdutc ON planassignments (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO planassignments (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE planassignments SET userid = $3, planid = $4, source = $5, promocodeid = $6, startsutc = $7, endsutc = $8, revokedutc = $9, note = $10, grantedbyuserid = $11, active = $12, lastupdateutc = $13 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM planassignments WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM planassignments WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM planassignments WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM planassignments WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM planassignments WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
