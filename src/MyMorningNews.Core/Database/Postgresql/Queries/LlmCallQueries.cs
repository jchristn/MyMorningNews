namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the llmcalls table.
    /// </summary>
    public static class LlmCallQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "llmcalls";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, task, runid, refinementsessionid, briefingid, modelendpointid, provider, model, promptkey, prompttemplateversionid, userid, topicid, streamed, attempts, inputtokens, cachedinputtokens, cachewritetokens, outputtokens, reasoningtokens, totaltokens, durationms, timetofirsttokenms, streamingdurationms, chunkcount, outputtokenspersecond, toolcallcount, toolnames, success, statuscode, finishreason, errormessage, structuredoutputrepairs, inputcostusd, outputcostusd, estimatedcostusd, isdryrun, prompttext, responsetext, active, createdutc, lastupdateutc, runstageid";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS llmcalls (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    task VARCHAR(64) NOT NULL,
    runid VARCHAR(64),
    refinementsessionid VARCHAR(64),
    briefingid VARCHAR(64),
    modelendpointid VARCHAR(64) REFERENCES modelendpoints(id) ON DELETE SET NULL,
    provider VARCHAR(32),
    model VARCHAR(256),
    promptkey VARCHAR(128),
    prompttemplateversionid VARCHAR(64),
    userid VARCHAR(64),
    topicid VARCHAR(64),
    streamed BOOLEAN NOT NULL,
    attempts INTEGER NOT NULL,
    inputtokens INTEGER NOT NULL,
    cachedinputtokens INTEGER NOT NULL,
    cachewritetokens INTEGER NOT NULL,
    outputtokens INTEGER NOT NULL,
    reasoningtokens INTEGER NOT NULL,
    totaltokens INTEGER NOT NULL,
    durationms DOUBLE PRECISION NOT NULL,
    timetofirsttokenms DOUBLE PRECISION,
    streamingdurationms DOUBLE PRECISION,
    chunkcount INTEGER NOT NULL,
    outputtokenspersecond DOUBLE PRECISION,
    toolcallcount INTEGER NOT NULL,
    toolnames TEXT[] NOT NULL,
    success BOOLEAN NOT NULL,
    statuscode INTEGER,
    finishreason VARCHAR(64),
    errormessage TEXT,
    structuredoutputrepairs INTEGER NOT NULL,
    inputcostusd NUMERIC(18,6),
    outputcostusd NUMERIC(18,6),
    estimatedcostusd NUMERIC(18,6),
    isdryrun BOOLEAN NOT NULL,
    prompttext TEXT,
    responsetext TEXT,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_llmcalls_tenantid ON llmcalls (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_llmcalls_modelendpointid ON llmcalls (modelendpointid)",
            @"CREATE INDEX IF NOT EXISTS ix_llmcalls_tenantid_createdutc ON llmcalls (tenantid, createdutc)",
            @"CREATE INDEX IF NOT EXISTS ix_llmcalls_createdutc ON llmcalls (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO llmcalls (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27, $28, $29, $30, $31, $32, $33, $34, $35, $36, $37, $38, $39, $40, $41, $42, $43)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE llmcalls SET task = $3, runid = $4, refinementsessionid = $5, briefingid = $6, modelendpointid = $7, provider = $8, model = $9, promptkey = $10, prompttemplateversionid = $11, userid = $12, topicid = $13, streamed = $14, attempts = $15, inputtokens = $16, cachedinputtokens = $17, cachewritetokens = $18, outputtokens = $19, reasoningtokens = $20, totaltokens = $21, durationms = $22, timetofirsttokenms = $23, streamingdurationms = $24, chunkcount = $25, outputtokenspersecond = $26, toolcallcount = $27, toolnames = $28, success = $29, statuscode = $30, finishreason = $31, errormessage = $32, structuredoutputrepairs = $33, inputcostusd = $34, outputcostusd = $35, estimatedcostusd = $36, isdryrun = $37, prompttext = $38, responsetext = $39, active = $40, lastupdateutc = $41, runstageid = $42 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM llmcalls WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM llmcalls WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM llmcalls WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM llmcalls WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM llmcalls WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
