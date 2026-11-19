namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the findingevidence table.
    /// </summary>
    public static class FindingEvidenceQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "findingevidence";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, findingid, evidenceid, role, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS findingevidence (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    findingid VARCHAR(64) NOT NULL REFERENCES findings(id) ON DELETE CASCADE,
    evidenceid VARCHAR(64) NOT NULL REFERENCES evidence(id) ON DELETE CASCADE,
    role VARCHAR(64) NOT NULL,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_findingevidence_tenantid ON findingevidence (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_findingevidence_findingid ON findingevidence (findingid)",
            @"CREATE INDEX IF NOT EXISTS ix_findingevidence_evidenceid ON findingevidence (evidenceid)",
            @"CREATE INDEX IF NOT EXISTS ix_findingevidence_createdutc ON findingevidence (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO findingevidence (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE findingevidence SET findingid = $3, evidenceid = $4, role = $5, sortorder = $6, active = $7, lastupdateutc = $8 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM findingevidence WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM findingevidence WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM findingevidence WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM findingevidence WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM findingevidence WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
