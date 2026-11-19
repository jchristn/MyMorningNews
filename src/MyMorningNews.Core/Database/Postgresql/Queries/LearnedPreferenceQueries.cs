namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the learnedpreferences table.
    /// </summary>
    public static class LearnedPreferenceQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "learnedpreferences";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, topicid, kind, statement, status, origin, supportcount, confidence, lastevidenceutc, decidedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS learnedpreferences (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    topicid VARCHAR(64),
    kind VARCHAR(64) NOT NULL,
    statement TEXT NOT NULL,
    status VARCHAR(64) NOT NULL,
    origin VARCHAR(64) NOT NULL,
    supportcount INTEGER NOT NULL,
    confidence DOUBLE PRECISION NOT NULL,
    lastevidenceutc TIMESTAMPTZ,
    decidedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_learnedpreferences_tenantid ON learnedpreferences (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_learnedpreferences_createdutc ON learnedpreferences (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO learnedpreferences (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE learnedpreferences SET userid = $3, topicid = $4, kind = $5, statement = $6, status = $7, origin = $8, supportcount = $9, confidence = $10, lastevidenceutc = $11, decidedutc = $12, active = $13, lastupdateutc = $14 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM learnedpreferences WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM learnedpreferences WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM learnedpreferences WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM learnedpreferences WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM learnedpreferences WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
