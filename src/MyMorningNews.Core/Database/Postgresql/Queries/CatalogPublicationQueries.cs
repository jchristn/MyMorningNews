namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the catalogpublications table.
    /// </summary>
    public static class CatalogPublicationQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "catalogpublications";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, topicid, ownertenantid, title, description, category, language, status, freeeligible, sortorder, revision, publishedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS catalogpublications (
    id VARCHAR(64) PRIMARY KEY,
    topicid VARCHAR(64) NOT NULL REFERENCES topics(id) ON DELETE RESTRICT,
    ownertenantid VARCHAR(64) NOT NULL,
    title VARCHAR(200) NOT NULL,
    description TEXT,
    category VARCHAR(128) NOT NULL,
    language VARCHAR(16) NOT NULL,
    status VARCHAR(32) NOT NULL,
    freeeligible BOOLEAN NOT NULL,
    sortorder INTEGER NOT NULL,
    revision BIGINT NOT NULL,
    publishedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_catalogpublications_topicid ON catalogpublications (topicid)",
            @"CREATE INDEX IF NOT EXISTS ix_catalogpublications_createdutc ON catalogpublications (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO catalogpublications (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15)";

        /// <summary>
        /// Update statement. $1 is id, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE catalogpublications SET topicid = $2, ownertenantid = $3, title = $4, description = $5, category = $6, language = $7, status = $8, freeeligible = $9, sortorder = $10, revision = $11, publishedutc = $12, active = $13, lastupdateutc = $14 WHERE id = $1 AND revision = $11 - 1";

        /// <summary>
        /// Select by identifier.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM catalogpublications WHERE id = $1";

        /// <summary>
        /// Delete by identifier.
        /// </summary>
        public const string Delete = "DELETE FROM catalogpublications WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM catalogpublications WHERE id = $1 LIMIT 1";
    }
}
