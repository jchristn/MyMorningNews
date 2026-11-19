namespace MyMorningNews.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// SQL for the auditrecords table.
    /// </summary>
    public static class AuditRecordQueries
    {
        /// <summary>
        /// Table name.
        /// </summary>
        public const string Table = "auditrecords";

        /// <summary>
        /// Column list in model order.
        /// </summary>
        public const string Columns = "id, tenantid, eventtype, requestid, correlationid, traceid, authscheme, principaltype, principalid, sessionid, userid, credentialid, resourcetype, resourceid, requesttype, method, path, sourceip, authenticationresult, authorizationresult, reason, bypassreason, requiredpermission, statuscode, description, active, createdutc, lastupdateutc";

        /// <summary>
        /// Schema statements: table and indexes. Idempotent.
        /// </summary>
        public static readonly IReadOnlyList<string> Schema = new List<string>
        {
            @"CREATE TABLE IF NOT EXISTS auditrecords (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) REFERENCES tenants(id) ON DELETE CASCADE,
    eventtype VARCHAR(64) NOT NULL,
    requestid VARCHAR(64),
    correlationid VARCHAR(128),
    traceid VARCHAR(64),
    authscheme VARCHAR(64),
    principaltype VARCHAR(32),
    principalid VARCHAR(64),
    sessionid VARCHAR(64),
    userid VARCHAR(64),
    credentialid VARCHAR(64),
    resourcetype VARCHAR(64),
    resourceid VARCHAR(64),
    requesttype VARCHAR(64),
    method VARCHAR(16),
    path TEXT,
    sourceip VARCHAR(64),
    authenticationresult VARCHAR(32),
    authorizationresult VARCHAR(32),
    reason TEXT,
    bypassreason TEXT,
    requiredpermission VARCHAR(64),
    statuscode INTEGER NOT NULL,
    description TEXT,
    active BOOLEAN NOT NULL,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
)",
            @"CREATE INDEX IF NOT EXISTS ix_auditrecords_tenantid ON auditrecords (tenantid)",
            @"CREATE INDEX IF NOT EXISTS ix_auditrecords_createdutc ON auditrecords (createdutc)"
        };

        /// <summary>
        /// Insert statement with positional parameters in column order.
        /// </summary>
        public const string Insert = "INSERT INTO auditrecords (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27, $28)";

        /// <summary>
        /// Update statement. $1 is id, $2 is tenantid, then the mutable columns in model order.
        /// </summary>
        public const string Update = "UPDATE auditrecords SET eventtype = $3, requestid = $4, correlationid = $5, traceid = $6, authscheme = $7, principaltype = $8, principalid = $9, sessionid = $10, userid = $11, credentialid = $12, resourcetype = $13, resourceid = $14, requesttype = $15, method = $16, path = $17, sourceip = $18, authenticationresult = $19, authorizationresult = $20, reason = $21, bypassreason = $22, requiredpermission = $23, statuscode = $24, description = $25, active = $26, lastupdateutc = $27 WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Select by identifier within a tenant.
        /// </summary>
        public const string SelectById = "SELECT " + Columns + " FROM auditrecords WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Select by identifier without tenant scoping.
        /// </summary>
        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM auditrecords WHERE id = $1";

        /// <summary>
        /// Delete by identifier within a tenant.
        /// </summary>
        public const string Delete = "DELETE FROM auditrecords WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2";

        /// <summary>
        /// Delete by identifier without tenant scoping.
        /// </summary>
        public const string DeleteUnscoped = "DELETE FROM auditrecords WHERE id = $1";

        /// <summary>
        /// Existence check.
        /// </summary>
        public const string Exists = "SELECT 1 FROM auditrecords WHERE id = $1 AND tenantid IS NOT DISTINCT FROM $2 LIMIT 1";
    }
}
