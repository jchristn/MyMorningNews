namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the plans table.
    /// </summary>
    public static class PlanQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "plans";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, key, name, description, isdefault, sortorder, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS plans (
    id VARCHAR(64) PRIMARY KEY,
    key VARCHAR(64) NOT NULL,
    name VARCHAR(128) NOT NULL,
    description TEXT,
    isdefault BOOLEAN NOT NULL,
    sortorder INTEGER NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_plans_key ON plans (key)",
            @"CREATE INDEX IF NOT EXISTS ix_plans_createdutc ON plans (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO plans (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)";

        /// <summary>
        /// Update statement. $1 is id, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE plans SET key = $2, name = $3, description = $4, isdefault = $5, sortorder = $6, active = $7, lastupdateutc = $8 WHERE id = $1";

        /// <summary>
        /// Select by identifier.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM plans WHERE id = $1";

        /// <summary>
        /// Delete by identifier.
        /// </summary>
        public const string Delete = "DELETE FROM plans WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM plans WHERE id = $1 LIMIT 1";
    }
}
