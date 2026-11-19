namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the schedules table.
    /// </summary>
    public static class ScheduleQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "schedules";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, ownertype, ownerid, userid, name, intervaltype, intervalvalue, minuteoffset, daysofweekmask, daysofmonth, timesofday, cronexpression, timezone, startutc, endutc, runonceutc, enabled, missedrunpolicy, nextrunutc, lastrunutc, lockeduntilutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS schedules (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    ownertype VARCHAR(64) NOT NULL,
    ownerid VARCHAR(64) NOT NULL,
    userid VARCHAR(64) NOT NULL,
    name VARCHAR(200),
    intervaltype VARCHAR(64) NOT NULL,
    intervalvalue INTEGER NOT NULL,
    minuteoffset INTEGER NOT NULL,
    daysofweekmask INTEGER NOT NULL,
    daysofmonth INTEGER[] NOT NULL,
    timesofday TEXT[] NOT NULL,
    cronexpression VARCHAR(200),
    timezone VARCHAR(64) NOT NULL,
    startutc TIMESTAMPTZ,
    endutc TIMESTAMPTZ,
    runonceutc TIMESTAMPTZ,
    enabled BOOLEAN NOT NULL,
    missedrunpolicy VARCHAR(64) NOT NULL,
    nextrunutc TIMESTAMPTZ,
    lastrunutc TIMESTAMPTZ,
    lockeduntilutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_schedules_tenantid ON schedules (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_schedules_nextrunutc ON schedules (nextrunutc)",
            @"CREATE INDEX IF NOT EXISTS ix_schedules_createdutc ON schedules (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO schedules (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE schedules SET ownertype = $3, ownerid = $4, userid = $5, name = $6, intervaltype = $7, intervalvalue = $8, minuteoffset = $9, daysofweekmask = $10, daysofmonth = $11, timesofday = $12, cronexpression = $13, timezone = $14, startutc = $15, endutc = $16, runonceutc = $17, enabled = $18, missedrunpolicy = $19, nextrunutc = $20, lastrunutc = $21, lockeduntilutc = $22, active = $23, lastupdateutc = $24 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM schedules WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM schedules WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM schedules WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM schedules WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM schedules WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
