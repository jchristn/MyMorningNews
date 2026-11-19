namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the mediaassets table.
    /// </summary>
    public static class MediaAssetQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "mediaassets";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, briefingid, editionid, kind, status, format, voiceprofileid, title, scriptmarkdown, scriptfrommodel, artifactid, chaptersartifactid, contenttype, bytes, durationseconds, segmentcount, attempts, lockeduntilutc, errormessage, completedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS mediaassets (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    briefingid VARCHAR(64) NOT NULL,
    editionid VARCHAR(64),
    kind VARCHAR(64) NOT NULL,
    status VARCHAR(64) NOT NULL,
    format VARCHAR(16) NOT NULL,
    voiceprofileid VARCHAR(64),
    title VARCHAR(256) NOT NULL,
    scriptmarkdown TEXT,
    scriptfrommodel BOOLEAN NOT NULL,
    artifactid VARCHAR(64),
    chaptersartifactid VARCHAR(64),
    contenttype VARCHAR(64),
    bytes BIGINT NOT NULL,
    durationseconds DOUBLE PRECISION NOT NULL,
    segmentcount INTEGER NOT NULL,
    attempts INTEGER NOT NULL,
    lockeduntilutc TIMESTAMPTZ,
    errormessage TEXT,
    completedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_mediaassets_tenantid ON mediaassets (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_mediaassets_createdutc ON mediaassets (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO mediaassets (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE mediaassets SET userid = $3, briefingid = $4, editionid = $5, kind = $6, status = $7, format = $8, voiceprofileid = $9, title = $10, scriptmarkdown = $11, scriptfrommodel = $12, artifactid = $13, chaptersartifactid = $14, contenttype = $15, bytes = $16, durationseconds = $17, segmentcount = $18, attempts = $19, lockeduntilutc = $20, errormessage = $21, completedutc = $22, active = $23, lastupdateutc = $24 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM mediaassets WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM mediaassets WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM mediaassets WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM mediaassets WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM mediaassets WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
