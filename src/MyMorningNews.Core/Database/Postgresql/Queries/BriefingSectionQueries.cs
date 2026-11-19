namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the briefingsections table.
    /// </summary>
    public static class BriefingSectionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "briefingsections";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, briefingid, sectionkey, heading, markdown, source, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS briefingsections (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    briefingid VARCHAR(64) NOT NULL,
    sectionkey VARCHAR(128) NOT NULL,
    heading VARCHAR(256) NOT NULL,
    markdown TEXT,
    source VARCHAR(32) NOT NULL,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingsections_tenantid ON briefingsections (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingsections_createdutc ON briefingsections (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO briefingsections (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE briefingsections SET briefingid = $3, sectionkey = $4, heading = $5, markdown = $6, source = $7, sortorder = $8, active = $9, lastupdateutc = $10 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM briefingsections WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM briefingsections WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM briefingsections WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM briefingsections WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM briefingsections WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
