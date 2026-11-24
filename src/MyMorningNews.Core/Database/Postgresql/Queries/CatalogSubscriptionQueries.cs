namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the catalogsubscriptions table.
    /// </summary>
    public static class CatalogSubscriptionQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "catalogsubscriptions";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, userid, publicationid, emailenabled, pushenabled, suspended, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS catalogsubscriptions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    userid VARCHAR(64) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    publicationid VARCHAR(64) NOT NULL REFERENCES catalogpublications(id) ON DELETE CASCADE,
    emailenabled BOOLEAN NOT NULL,
    pushenabled BOOLEAN NOT NULL,
    suspended BOOLEAN NOT NULL,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_catalogsubscriptions_tenantid ON catalogsubscriptions (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_catalogsubscriptions_tenantid_userid ON catalogsubscriptions (tenantid, userid)",
            @"CREATE INDEX IF NOT EXISTS ix_catalogsubscriptions_publicationid ON catalogsubscriptions (publicationid)",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ux_catalogsubscriptions_tenantid_userid_publicationid ON catalogsubscriptions (tenantid, userid, publicationid)",
            @"CREATE INDEX IF NOT EXISTS ix_catalogsubscriptions_createdutc ON catalogsubscriptions (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO catalogsubscriptions (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE catalogsubscriptions SET userid = $3, publicationid = $4, emailenabled = $5, pushenabled = $6, suspended = $7, active = $8, lastupdateutc = $9 WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM catalogsubscriptions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM catalogsubscriptions WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM catalogsubscriptions WHERE id = $1 AND tenantid = $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM catalogsubscriptions WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM catalogsubscriptions WHERE id = $1 AND tenantid = $2 LIMIT 1";
    }
}
