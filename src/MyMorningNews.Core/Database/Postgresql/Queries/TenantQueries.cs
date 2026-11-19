namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the tenants table.
    /// </summary>
    public static class TenantQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "tenants";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, name, description, region, timezone, isprotected, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS tenants (
    id VARCHAR(64) PRIMARY KEY,
    name VARCHAR(128) NOT NULL,
    description TEXT,
    region VARCHAR(64),
    timezone VARCHAR(64) NOT NULL,
    isprotected BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_tenants_createdutc ON tenants (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO tenants (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)";

        /// <summary>
        /// Update statement. $1 is id, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE tenants SET name = $2, description = $3, region = $4, timezone = $5, isprotected = $6, active = $7, lastupdateutc = $8 WHERE id = $1";

        /// <summary>
        /// Select by identifier.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM tenants WHERE id = $1";

        /// <summary>
        /// Delete by identifier.
        /// </summary>
        public const string Delete = "DELETE FROM tenants WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM tenants WHERE id = $1 LIMIT 1";
    }
}
