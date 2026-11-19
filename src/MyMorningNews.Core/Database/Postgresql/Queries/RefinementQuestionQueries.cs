namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the refinementquestions table.
    /// </summary>
    public static class RefinementQuestionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "refinementquestions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, sessionid, round, dimensionid, questiontext, rationale, kind, answertext, answered, skipped, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS refinementquestions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    sessionid VARCHAR(64) NOT NULL REFERENCES refinementsessions(id) ON DELETE CASCADE,
    round INTEGER NOT NULL,
    dimensionid VARCHAR(64),
    questiontext TEXT NOT NULL,
    rationale TEXT,
    kind VARCHAR(64) NOT NULL,
    answertext TEXT,
    answered BOOLEAN NOT NULL,
    skipped BOOLEAN NOT NULL,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementquestions_tenantid ON refinementquestions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementquestions_sessionid ON refinementquestions (sessionid)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementquestions_createdutc ON refinementquestions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO refinementquestions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE refinementquestions SET sessionid = $3, round = $4, dimensionid = $5, questiontext = $6, rationale = $7, kind = $8, answertext = $9, answered = $10, skipped = $11, sortorder = $12, active = $13, lastupdateutc = $14 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM refinementquestions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM refinementquestions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM refinementquestions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM refinementquestions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM refinementquestions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
