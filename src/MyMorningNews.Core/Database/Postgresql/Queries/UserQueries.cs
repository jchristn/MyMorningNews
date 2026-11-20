namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the users table.
    /// </summary>
    public static class UserQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "users";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, firstname, lastname, email, passwordsha256, isadmin, istenantadmin, isprotected, timezone, locale, lastloginutc, active, createdutc, lastupdateutc, selfsignup, emailverifiedutc, termsversion, termsacceptedutc, deletionrequestedutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS users (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    firstname VARCHAR(128),
    lastname VARCHAR(128),
    email VARCHAR(256) NOT NULL,
    passwordsha256 VARCHAR(64) NOT NULL,
    isadmin BOOLEAN NOT NULL,
    istenantadmin BOOLEAN NOT NULL,
    isprotected BOOLEAN NOT NULL,
    timezone VARCHAR(64),
    locale VARCHAR(16),
    lastloginutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_users_tenantid ON users (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_users_tenantid_email ON users (tenantid, email)",
            @"CREATE INDEX IF NOT EXISTS ix_users_createdutc ON users (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO users (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE users SET firstname = $3, lastname = $4, email = $5, passwordsha256 = $6, isadmin = $7, istenantadmin = $8, isprotected = $9, timezone = $10, locale = $11, lastloginutc = $12, active = $13, lastupdateutc = $14, selfsignup = $15, emailverifiedutc = $16, termsversion = $17, termsacceptedutc = $18, deletionrequestedutc = $19 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM users WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM users WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM users WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM users WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM users WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
