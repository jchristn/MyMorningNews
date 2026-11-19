namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the researchrequests table.
    /// </summary>
    public static class ResearchRequestQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "researchrequests";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, topicid, requesttext, title, deliverables, length, status, runatutc, emailrecipients, publishtofeed, outofscopereason, estimatedcostusd, runid, briefingid, convertedtopicid, errormessage, confirmedutc, completedutc, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS researchrequests (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    topicid VARCHAR(64),
    requesttext TEXT NOT NULL,
    title VARCHAR(200),
    deliverables TEXT[] NOT NULL,
    length VARCHAR(64) NOT NULL,
    status VARCHAR(64) NOT NULL,
    runatutc TIMESTAMPTZ,
    emailrecipients TEXT[] NOT NULL,
    publishtofeed BOOLEAN NOT NULL,
    outofscopereason TEXT,
    estimatedcostusd NUMERIC(18,6),
    runid VARCHAR(64),
    briefingid VARCHAR(64),
    convertedtopicid VARCHAR(64),
    errormessage TEXT,
    confirmedutc TIMESTAMPTZ,
    completedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_researchrequests_tenantid ON researchrequests (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_researchrequests_createdutc ON researchrequests (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO researchrequests (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE researchrequests SET userid = $3, topicid = $4, requesttext = $5, title = $6, deliverables = $7, length = $8, status = $9, runatutc = $10, emailrecipients = $11, publishtofeed = $12, outofscopereason = $13, estimatedcostusd = $14, runid = $15, briefingid = $16, convertedtopicid = $17, errormessage = $18, confirmedutc = $19, completedutc = $20, active = $21, lastupdateutc = $22 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM researchrequests WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM researchrequests WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM researchrequests WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM researchrequests WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM researchrequests WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
