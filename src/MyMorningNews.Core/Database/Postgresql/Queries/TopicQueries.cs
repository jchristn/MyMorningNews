namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the topics table.
    /// </summary>
    public static class TopicQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "topics";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, name, slug, description, brief, audiencelevel, horizon, language, status, isonetime, noveltypreference, emailonrun, emailwhenempty, maxemailfindings, displaythreshold, weightllm, weightfeedback, weightnovelty, weightsource, weightrecency, seedqueries, briefingtemplateid, lastrunutc, lastrefinedutc, active, createdutc, lastupdateutc, agenticsearch, mcptoolids, voiceprofileid, audioenabled, historymode, historycount, historysinceutc, historybriefingid, historytokenbudget";

        /// <summary>
        /// Columns read back: <see cref="Columns"/> plus slotpriority, which only the slot selection writes.
        /// </summary>
        public const string SelectColumns = Columns + ", slotpriority";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS topics (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    slug VARCHAR(200) NOT NULL,
    description TEXT,
    brief TEXT,
    audiencelevel VARCHAR(64) NOT NULL,
    horizon VARCHAR(64) NOT NULL,
    language VARCHAR(16) NOT NULL,
    status VARCHAR(64) NOT NULL,
    isonetime BOOLEAN NOT NULL,
    noveltypreference INTEGER NOT NULL,
    emailonrun BOOLEAN NOT NULL,
    emailwhenempty BOOLEAN NOT NULL,
    maxemailfindings INTEGER NOT NULL,
    displaythreshold INTEGER,
    weightllm DOUBLE PRECISION,
    weightfeedback DOUBLE PRECISION,
    weightnovelty DOUBLE PRECISION,
    weightsource DOUBLE PRECISION,
    weightrecency DOUBLE PRECISION,
    seedqueries TEXT[] NOT NULL,
    briefingtemplateid VARCHAR(64),
    lastrunutc TIMESTAMPTZ,
    lastrefinedutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_topics_tenantid ON topics (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_topics_tenantid_slug ON topics (tenantid, slug)",
            @"CREATE INDEX IF NOT EXISTS ix_topics_createdutc ON topics (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO topics (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27, $28, $29, $30, $31, $32, $33, $34, $35, $36, $37, $38)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE topics SET userid = $3, name = $4, slug = $5, description = $6, brief = $7, audiencelevel = $8, horizon = $9, language = $10, status = $11, isonetime = $12, noveltypreference = $13, emailonrun = $14, emailwhenempty = $15, maxemailfindings = $16, displaythreshold = $17, weightllm = $18, weightfeedback = $19, weightnovelty = $20, weightsource = $21, weightrecency = $22, seedqueries = $23, briefingtemplateid = $24, lastrunutc = $25, lastrefinedutc = $26, active = $27, lastupdateutc = $28, agenticsearch = $29, mcptoolids = $30, voiceprofileid = $31, audioenabled = $32, historymode = $33, historycount = $34, historysinceutc = $35, historybriefingid = $36, historytokenbudget = $37 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + SelectColumns + " FROM topics WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + SelectColumns + " FROM topics WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM topics WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM topics WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM topics WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
