namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the briefingdeltas table.
    /// </summary>
    public static class BriefingDeltaQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "briefingdeltas";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, briefingid, kind, title, summary, findingid, predictionid, baselinebriefingid, baselineitemid, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS briefingdeltas (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    briefingid VARCHAR(64) NOT NULL REFERENCES briefings(id) ON DELETE CASCADE,
    kind VARCHAR(32) NOT NULL CHECK (kind IN ('NewDevelopment','ChangedFact','Resolved','Correction','Unchanged','Unavailable')),
    title VARCHAR(512) NOT NULL,
    summary TEXT,
    findingid VARCHAR(64) REFERENCES findings(id) ON DELETE SET NULL,
    predictionid VARCHAR(64) REFERENCES predictions(id) ON DELETE SET NULL,
    baselinebriefingid VARCHAR(64) REFERENCES briefings(id) ON DELETE SET NULL,
    baselineitemid VARCHAR(64),
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_briefingdeltas_briefing ON briefingdeltas (tenantid, briefingid, sortorder)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO briefingdeltas (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)";

        /// <summary>
        /// Delete every change of one briefing.
        /// </summary>
        public const string DeleteByBriefing = "DELETE FROM briefingdeltas WHERE tenantid = $1 AND briefingid = $2";
    }
}
