namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the modelendpoints table.
    /// </summary>
    public static class ModelEndpointQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "modelendpoints";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, name, provider, endpoint, credentialid, model, embeddingmodel, embeddingdimensions, supportschat, supportstools, supportsembeddings, temperature, maxtokens, reasoningeffort, contextlength, timeoutms, inputcostpermtok, outputcostpermtok, cachedinputcostpermtok, cachewritecostpermtok, lastvalidatedutc, lastvalidationresult, active, createdutc, lastupdateutc, lastvalidationsucceeded";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS modelendpoints (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(128) NOT NULL,
    provider VARCHAR(64) NOT NULL,
    endpoint VARCHAR(512),
    credentialid VARCHAR(64) REFERENCES vaultcredentials(id) ON DELETE SET NULL,
    model VARCHAR(256),
    embeddingmodel VARCHAR(256),
    embeddingdimensions INTEGER,
    supportschat BOOLEAN NOT NULL,
    supportstools BOOLEAN,
    supportsembeddings BOOLEAN NOT NULL,
    temperature DOUBLE PRECISION,
    maxtokens INTEGER NOT NULL,
    reasoningeffort VARCHAR(64),
    contextlength INTEGER,
    timeoutms INTEGER NOT NULL,
    inputcostpermtok NUMERIC(18,6),
    outputcostpermtok NUMERIC(18,6),
    cachedinputcostpermtok NUMERIC(18,6),
    cachewritecostpermtok NUMERIC(18,6),
    lastvalidatedutc TIMESTAMPTZ,
    lastvalidationresult TEXT,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_modelendpoints_tenantid ON modelendpoints (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_modelendpoints_credentialid ON modelendpoints (credentialid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_modelendpoints_tenantid_name ON modelendpoints (tenantid, name)",
            @"CREATE INDEX IF NOT EXISTS ix_modelendpoints_createdutc ON modelendpoints (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO modelendpoints (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE modelendpoints SET name = $3, provider = $4, endpoint = $5, credentialid = $6, model = $7, embeddingmodel = $8, embeddingdimensions = $9, supportschat = $10, supportstools = $11, supportsembeddings = $12, temperature = $13, maxtokens = $14, reasoningeffort = $15, contextlength = $16, timeoutms = $17, inputcostpermtok = $18, outputcostpermtok = $19, cachedinputcostpermtok = $20, cachewritecostpermtok = $21, lastvalidatedutc = $22, lastvalidationresult = $23, active = $24, lastupdateutc = $25, lastvalidationsucceeded = $26 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM modelendpoints WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM modelendpoints WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM modelendpoints WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM modelendpoints WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM modelendpoints WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
