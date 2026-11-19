namespace MyMorningNews.Core.Database.Postgresql
{
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Database.Postgresql.Queries;

    /// <summary>
    /// Ordered PostgreSQL schema migrations. Add new versions at the end; never edit an applied version.
    /// </summary>
    public static class PostgresqlMigrations
    {
        /// <summary>
        /// All migrations in ascending version order.
        /// </summary>
        public static IReadOnlyList<SchemaMigration> All { get; } = new List<SchemaMigration>
        {
            new SchemaMigration(1, "Tenancy, security, audit, and request history", Concat(
                TenantQueries.Schema,
                UserQueries.Schema,
                CredentialQueries.Schema,
                AuthSessionQueries.Schema,
                UserRoleQueries.Schema,
                PermissionQueries.Schema,
                RolePermissionMapQueries.Schema,
                UserRoleAssignmentQueries.Schema,
                CredentialScopeAssignmentQueries.Schema,
                AuditRecordQueries.Schema,
                RequestHistoryQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_permissions_resourcetypes ON permissions USING GIN (resourcetypes)",
                    "CREATE INDEX IF NOT EXISTS ix_permissions_operationtypes ON permissions USING GIN (operationtypes)",
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_userroles_builtin_name ON userroles (name) WHERE tenantid IS NULL",
                    "CREATE INDEX IF NOT EXISTS ix_credentials_accesskey ON credentials (accesskey)",
                    "CREATE INDEX IF NOT EXISTS ix_auditrecords_tenantid_createdutc ON auditrecords (tenantid, createdutc)"
                })),
            new SchemaMigration(2, "Credential vault, model endpoints, assignments, model call log, and prompt templates", Concat(
                VaultCredentialQueries.Schema,
                ModelEndpointQueries.Schema,
                ModelAssignmentQueries.Schema,
                LlmCallQueries.Schema,
                PromptTemplateQueries.Schema,
                PromptTemplateVersionQueries.Schema,
                new List<string>
                {
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_modelassignments_scope ON modelassignments (tenantid, task, COALESCE(topicid, ''))",
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_prompttemplates_scope ON prompttemplates (tenantid, key, COALESCE(topicid, ''))"
                })),
            new SchemaMigration(3, "Full model call accounting: attribution, token types, streaming timings, tool calls, split cost", new List<string>
            {
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS userid VARCHAR(64) NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS topicid VARCHAR(64) NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS streamed BOOLEAN NOT NULL DEFAULT FALSE",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS attempts INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS cachedinputtokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS cachewritetokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS reasoningtokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS totaltokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS timetofirsttokenms DOUBLE PRECISION NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS streamingdurationms DOUBLE PRECISION NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS chunkcount INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS outputtokenspersecond DOUBLE PRECISION NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS toolcallcount INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS toolnames TEXT[] NOT NULL DEFAULT '{}'",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS inputcostusd NUMERIC(18,6) NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS outputcostusd NUMERIC(18,6) NULL",
                "ALTER TABLE modelendpoints ADD COLUMN IF NOT EXISTS cachedinputcostpermtok NUMERIC(18,6) NULL",
                "ALTER TABLE modelendpoints ADD COLUMN IF NOT EXISTS cachewritecostpermtok NUMERIC(18,6) NULL",
                "CREATE INDEX IF NOT EXISTS ix_llmcalls_tenantid_userid_createdutc ON llmcalls (tenantid, userid, createdutc)",
                "CREATE INDEX IF NOT EXISTS ix_llmcalls_tenantid_topicid ON llmcalls (tenantid, topicid)",
                "CREATE INDEX IF NOT EXISTS ix_llmcalls_createdutc ON llmcalls (createdutc)"
            }),
            new SchemaMigration(4, "Mobile refresh tokens and push devices", Concat(
                RefreshTokenQueries.Schema,
                DeviceQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_users_email_lower ON users (LOWER(email))"
                }))
        };

        private static List<string> Concat(params IReadOnlyList<string>[] parts)
        {
            return parts.SelectMany(p => p).ToList();
        }
    }
}
