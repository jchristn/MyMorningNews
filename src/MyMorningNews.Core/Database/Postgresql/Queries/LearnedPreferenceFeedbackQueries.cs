namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the learnedpreferencefeedback table.
    /// </summary>
    public static class LearnedPreferenceFeedbackQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "learnedpreferencefeedback";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, learnedpreferenceid, feedbackid, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS learnedpreferencefeedback (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    learnedpreferenceid VARCHAR(64) NOT NULL,
    feedbackid VARCHAR(64) NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_learnedpreferencefeedback_tenantid ON learnedpreferencefeedback (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_learnedpreferencefeedback_learnedpreferenceid_feedbackid ON learnedpreferencefeedback (learnedpreferenceid, feedbackid)",
            @"CREATE INDEX IF NOT EXISTS ix_learnedpreferencefeedback_createdutc ON learnedpreferencefeedback (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO learnedpreferencefeedback (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE learnedpreferencefeedback SET learnedpreferenceid = $3, feedbackid = $4, active = $5, lastupdateutc = $6 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM learnedpreferencefeedback WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM learnedpreferencefeedback WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM learnedpreferencefeedback WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM learnedpreferencefeedback WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM learnedpreferencefeedback WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
