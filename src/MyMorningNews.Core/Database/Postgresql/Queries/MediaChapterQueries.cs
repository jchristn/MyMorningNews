namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the mediachapters table.
    /// </summary>
    public static class MediaChapterQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "mediachapters";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, mediaassetid, briefingsectionid, title, startseconds, endseconds, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS mediachapters (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    mediaassetid VARCHAR(64) NOT NULL,
    briefingsectionid VARCHAR(64),
    title VARCHAR(256) NOT NULL,
    startseconds DOUBLE PRECISION NOT NULL,
    endseconds DOUBLE PRECISION NOT NULL,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_mediachapters_tenantid ON mediachapters (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_mediachapters_createdutc ON mediachapters (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO mediachapters (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE mediachapters SET mediaassetid = $3, briefingsectionid = $4, title = $5, startseconds = $6, endseconds = $7, sortorder = $8, active = $9, lastupdateutc = $10 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM mediachapters WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM mediachapters WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM mediachapters WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM mediachapters WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM mediachapters WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
