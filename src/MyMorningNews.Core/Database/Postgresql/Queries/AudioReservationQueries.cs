namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the audioreservations table.
    /// </summary>
    public static class AudioReservationQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "audioreservations";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, operationid, periodstartutc, reservedseconds, committedmilliseconds, status, expiresutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS audioreservations (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    operationid VARCHAR(64) NOT NULL,
    periodstartutc TIMESTAMPTZ NOT NULL,
    reservedseconds BIGINT NOT NULL,
    committedmilliseconds BIGINT NOT NULL,
    status VARCHAR(32) NOT NULL,
    expiresutc TIMESTAMPTZ NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_audioreservations_tenantid ON audioreservations (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_audioreservations_tenantid_userid ON audioreservations (tenantid, userid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_audioreservations_tenantid_userid_operationid ON audioreservations (tenantid, userid, operationid)",
            @"CREATE INDEX IF NOT EXISTS ix_audioreservations_tenantid_userid_periodstartutc ON audioreservations (tenantid, userid, periodstartutc)",
            @"CREATE INDEX IF NOT EXISTS ix_audioreservations_createdutc ON audioreservations (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO audioreservations (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE audioreservations SET userid = $3, operationid = $4, periodstartutc = $5, reservedseconds = $6, committedmilliseconds = $7, status = $8, expiresutc = $9, active = $10, lastupdateutc = $11 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM audioreservations WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM audioreservations WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM audioreservations WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM audioreservations WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM audioreservations WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
