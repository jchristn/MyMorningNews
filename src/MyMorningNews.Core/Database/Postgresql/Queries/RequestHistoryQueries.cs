namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the requesthistory table. Header columns hold diagnostic JSON (unmanaged by design: captured request and
    /// response metadata retained only for inspection); every filterable field has its own typed column.
    /// </summary>
    public static class RequestHistoryQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "requesthistory";

        /// <summary>
        /// Summary columns (no bodies or headers).
        /// </summary>
        public const string SummaryColumns = "id, tenantid, userid, principalname, method, path, routetemplate, url, statuscode, durationms, sourceip, requestbodybytes, requestbodytruncated, responsebodybytes, responsebodytruncated, createdutc, completedutc";

        /// <summary>
        /// All columns.
        /// </summary>
        public const string AllColumns = SummaryColumns + ", requestheadersjson, requestbody, responseheadersjson, responsebody";

        /// <summary>
        /// Schema statements.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS requesthistory (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NULL,
    userid VARCHAR(64) NULL,
    principalname VARCHAR(256) NULL,
    method VARCHAR(16) NOT NULL,
    path TEXT NOT NULL,
    routetemplate TEXT NULL,
    url TEXT NOT NULL,
    statuscode INTEGER NOT NULL,
    durationms DOUBLE PRECISION NOT NULL,
    sourceip VARCHAR(64) NULL,
    requestbodybytes BIGINT NOT NULL,
    requestbodytruncated BOOLEAN NOT NULL,
    responsebodybytes BIGINT NOT NULL,
    responsebodytruncated BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    completedutc TIMESTAMPTZ NULL,
    requestheadersjson TEXT NULL,
    requestbody TEXT NULL,
    responseheadersjson TEXT NULL,
    responsebody TEXT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_requesthistory_tenantid_createdutc ON requesthistory (tenantid, createdutc)",
            @"CREATE INDEX IF NOT EXISTS ix_requesthistory_createdutc ON requesthistory (createdutc)",
            @"CREATE INDEX IF NOT EXISTS ix_requesthistory_statuscode ON requesthistory (statuscode)"
        };

        /// <summary>
        /// Insert.
        /// </summary>
        public const string Insert = "INSERT INTO requesthistory (" + AllColumns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21)";
    }
}
