namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the planfeatures table.
    /// </summary>
    public static class PlanFeatureQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "planfeatures";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, planid, key, value, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS planfeatures (
    id VARCHAR(64) PRIMARY KEY,
    planid VARCHAR(64) NOT NULL,
    key VARCHAR(128) NOT NULL,
    value VARCHAR(256) NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_planfeatures_planid_key ON planfeatures (planid, key)",
            @"CREATE INDEX IF NOT EXISTS ix_planfeatures_createdutc ON planfeatures (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO planfeatures (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7)";

        /// <summary>
        /// Update statement. $1 is id, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE planfeatures SET planid = $2, key = $3, value = $4, active = $5, lastupdateutc = $6 WHERE id = $1";

        /// <summary>
        /// Select by identifier.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM planfeatures WHERE id = $1";

        /// <summary>
        /// Delete by identifier.
        /// </summary>
        public const string Delete = "DELETE FROM planfeatures WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM planfeatures WHERE id = $1 LIMIT 1";
    }
}
