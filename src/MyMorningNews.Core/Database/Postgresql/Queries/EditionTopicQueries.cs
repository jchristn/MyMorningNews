namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the editiontopics table.
    /// </summary>
    public static class EditionTopicQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "editiontopics";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, editionid, topicid, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS editiontopics (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    editionid VARCHAR(64) NOT NULL,
    topicid VARCHAR(64) NOT NULL,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_editiontopics_tenantid ON editiontopics (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_editiontopics_editionid_topicid ON editiontopics (editionid, topicid)",
            @"CREATE INDEX IF NOT EXISTS ix_editiontopics_createdutc ON editiontopics (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO editiontopics (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE editiontopics SET editionid = $3, topicid = $4, sortorder = $5, active = $6, lastupdateutc = $7 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM editiontopics WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM editiontopics WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM editiontopics WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM editiontopics WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM editiontopics WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
