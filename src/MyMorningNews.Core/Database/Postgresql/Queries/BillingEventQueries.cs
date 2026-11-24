namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the billingevents table.
    /// </summary>
    public static class BillingEventQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "billingevents";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, provider, environment, eventid, providerid, status, attempts, retryutc, error, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS billingevents (
    id VARCHAR(64) PRIMARY KEY,
    provider VARCHAR(16) NOT NULL,
    environment VARCHAR(16) NOT NULL,
    eventid VARCHAR(256) NOT NULL,
    providerid VARCHAR(256) NOT NULL,
    status VARCHAR(32) NOT NULL,
    attempts INTEGER NOT NULL,
    retryutc TIMESTAMPTZ NOT NULL,
    error TEXT,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_billingevents_provider_environment_eventid ON billingevents (provider, environment, eventid)",
            @"CREATE INDEX IF NOT EXISTS ix_billingevents_createdutc ON billingevents (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO billingevents (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)";

        /// <summary>
        /// Update statement. $1 is id, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE billingevents SET provider = $2, environment = $3, eventid = $4, providerid = $5, status = $6, attempts = $7, retryutc = $8, error = $9, active = $10, lastupdateutc = $11 WHERE id = $1";

        /// <summary>
        /// Select by identifier.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM billingevents WHERE id = $1";

        /// <summary>
        /// Delete by identifier.
        /// </summary>
        public const string Delete = "DELETE FROM billingevents WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM billingevents WHERE id = $1 LIMIT 1";
    }
}
