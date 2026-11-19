namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the voiceprofiles table.
    /// </summary>
    public static class VoiceProfileQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "voiceprofiles";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, name, provider, endpoint, model, voice, secondaryvoice, speed, credentialid, isdefault, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS voiceprofiles (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(128) NOT NULL,
    provider VARCHAR(64) NOT NULL,
    endpoint VARCHAR(512),
    model VARCHAR(128) NOT NULL,
    voice VARCHAR(128) NOT NULL,
    secondaryvoice VARCHAR(128),
    speed DOUBLE PRECISION NOT NULL,
    credentialid VARCHAR(64),
    isdefault BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_voiceprofiles_tenantid ON voiceprofiles (tenantid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_voiceprofiles_tenantid_name ON voiceprofiles (tenantid, name)",
            @"CREATE INDEX IF NOT EXISTS ix_voiceprofiles_createdutc ON voiceprofiles (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO voiceprofiles (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE voiceprofiles SET name = $3, provider = $4, endpoint = $5, model = $6, voice = $7, secondaryvoice = $8, speed = $9, credentialid = $10, isdefault = $11, active = $12, lastupdateutc = $13 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM voiceprofiles WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM voiceprofiles WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM voiceprofiles WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM voiceprofiles WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM voiceprofiles WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
