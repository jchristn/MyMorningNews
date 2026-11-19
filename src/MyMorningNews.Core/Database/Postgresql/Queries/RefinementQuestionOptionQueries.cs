namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the refinementquestionoptions table.
    /// </summary>
    public static class RefinementQuestionOptionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "refinementquestionoptions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, questionid, label, value, selected, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS refinementquestionoptions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    questionid VARCHAR(64) NOT NULL REFERENCES refinementquestions(id) ON DELETE CASCADE,
    label TEXT NOT NULL,
    value VARCHAR(200),
    selected BOOLEAN NOT NULL,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementquestionoptions_tenantid ON refinementquestionoptions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementquestionoptions_questionid ON refinementquestionoptions (questionid)",
            @"CREATE INDEX IF NOT EXISTS ix_refinementquestionoptions_createdutc ON refinementquestionoptions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO refinementquestionoptions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE refinementquestionoptions SET questionid = $3, label = $4, value = $5, selected = $6, sortorder = $7, active = $8, lastupdateutc = $9 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM refinementquestionoptions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM refinementquestionoptions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM refinementquestionoptions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM refinementquestionoptions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM refinementquestionoptions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
