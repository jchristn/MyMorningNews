namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the briefings table.
    /// </summary>
    public static class BriefingQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "briefings";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, kind, editionid, topicid, runid, researchrequestid, scheduleid, periodstartutc, periodendutc, title, markdown, status, composeafterutc, researchdeadlineutc, lockeduntilutc, briefingtemplateversionid, mediaassetid, markdownartifactid, htmlartifactid, itemcount, wordcount, readminutes, modelfallbacks, errormessage, pinned, isdryrun, composedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS briefings (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL,
    kind VARCHAR(64) NOT NULL,
    editionid VARCHAR(64),
    topicid VARCHAR(64),
    runid VARCHAR(64),
    researchrequestid VARCHAR(64),
    scheduleid VARCHAR(64),
    periodstartutc TIMESTAMPTZ,
    periodendutc TIMESTAMPTZ,
    title VARCHAR(256) NOT NULL,
    markdown TEXT,
    status VARCHAR(64) NOT NULL,
    composeafterutc TIMESTAMPTZ,
    researchdeadlineutc TIMESTAMPTZ,
    lockeduntilutc TIMESTAMPTZ,
    briefingtemplateversionid VARCHAR(64),
    mediaassetid VARCHAR(64),
    markdownartifactid VARCHAR(64),
    htmlartifactid VARCHAR(64),
    itemcount INTEGER NOT NULL,
    wordcount INTEGER NOT NULL,
    readminutes INTEGER NOT NULL,
    modelfallbacks INTEGER NOT NULL,
    errormessage TEXT,
    pinned BOOLEAN NOT NULL,
    isdryrun BOOLEAN NOT NULL,
    composedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_briefings_tenantid ON briefings (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_briefings_createdutc ON briefings (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO briefings (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27, $28, $29, $30, $31, $32)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE briefings SET userid = $3, kind = $4, editionid = $5, topicid = $6, runid = $7, researchrequestid = $8, scheduleid = $9, periodstartutc = $10, periodendutc = $11, title = $12, markdown = $13, status = $14, composeafterutc = $15, researchdeadlineutc = $16, lockeduntilutc = $17, briefingtemplateversionid = $18, mediaassetid = $19, markdownartifactid = $20, htmlartifactid = $21, itemcount = $22, wordcount = $23, readminutes = $24, modelfallbacks = $25, errormessage = $26, pinned = $27, isdryrun = $28, composedutc = $29, active = $30, lastupdateutc = $31 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM briefings WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM briefings WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM briefings WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM briefings WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM briefings WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
