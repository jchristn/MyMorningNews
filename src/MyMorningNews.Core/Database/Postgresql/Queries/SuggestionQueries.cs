namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the suggestions table.
    /// </summary>
    public static class SuggestionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "suggestions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, runid, topicid, userid, title, description, rationale, priority, effort, status, snoozeduntilutc, statuschangedutc, findingids, isdryrun, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS suggestions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    runid VARCHAR(64) NOT NULL,
    topicid VARCHAR(64) NOT NULL,
    userid VARCHAR(64) NOT NULL,
    title TEXT NOT NULL,
    description TEXT,
    rationale TEXT,
    priority VARCHAR(64) NOT NULL,
    effort VARCHAR(64) NOT NULL,
    status VARCHAR(64) NOT NULL,
    snoozeduntilutc TIMESTAMPTZ,
    statuschangedutc TIMESTAMPTZ,
    findingids TEXT[] NOT NULL,
    isdryrun BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_suggestions_tenantid ON suggestions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_suggestions_createdutc ON suggestions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO suggestions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE suggestions SET runid = $3, topicid = $4, userid = $5, title = $6, description = $7, rationale = $8, priority = $9, effort = $10, status = $11, snoozeduntilutc = $12, statuschangedutc = $13, findingids = $14, isdryrun = $15, active = $16, lastupdateutc = $17 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM suggestions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM suggestions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM suggestions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM suggestions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM suggestions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
