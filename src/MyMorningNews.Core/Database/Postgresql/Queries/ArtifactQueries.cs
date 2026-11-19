namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the artifacts table.
    /// </summary>
    public static class ArtifactQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "artifacts";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, kind, topicid, briefingid, researchrequestid, filename, contenttype, bytes, sha256, storageprovider, storagekey, expiresutc, pinned, deletedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS artifacts (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    kind VARCHAR(64) NOT NULL,
    topicid VARCHAR(64),
    briefingid VARCHAR(64),
    researchrequestid VARCHAR(64),
    filename VARCHAR(256) NOT NULL,
    contenttype VARCHAR(128) NOT NULL,
    bytes BIGINT NOT NULL,
    sha256 VARCHAR(64) NOT NULL,
    storageprovider VARCHAR(64) NOT NULL,
    storagekey VARCHAR(512) NOT NULL,
    expiresutc TIMESTAMPTZ,
    pinned BOOLEAN NOT NULL,
    deletedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_artifacts_tenantid ON artifacts (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_artifacts_createdutc ON artifacts (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO artifacts (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE artifacts SET userid = $3, kind = $4, topicid = $5, briefingid = $6, researchrequestid = $7, filename = $8, contenttype = $9, bytes = $10, sha256 = $11, storageprovider = $12, storagekey = $13, expiresutc = $14, pinned = $15, deletedutc = $16, active = $17, lastupdateutc = $18 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM artifacts WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM artifacts WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM artifacts WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM artifacts WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM artifacts WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
