namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the evidence table.
    /// </summary>
    public static class EvidenceQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "evidence";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, sourceitemid, runid, quote, startoffset, endoffset, contexttext, quotehash, verified, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS evidence (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    sourceitemid VARCHAR(64) NOT NULL REFERENCES sourceitems(id) ON DELETE CASCADE,
    runid VARCHAR(64),
    quote TEXT NOT NULL,
    startoffset INTEGER NOT NULL,
    endoffset INTEGER NOT NULL,
    contexttext TEXT,
    quotehash VARCHAR(64) NOT NULL,
    verified BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_evidence_tenantid ON evidence (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_evidence_sourceitemid ON evidence (sourceitemid)",
            @"CREATE INDEX IF NOT EXISTS ix_evidence_createdutc ON evidence (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO evidence (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE evidence SET sourceitemid = $3, runid = $4, quote = $5, startoffset = $6, endoffset = $7, contexttext = $8, quotehash = $9, verified = $10, active = $11, lastupdateutc = $12 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM evidence WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM evidence WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM evidence WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM evidence WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM evidence WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
