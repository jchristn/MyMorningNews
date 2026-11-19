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
                }))
        };

        private static List<string> Concat(params IReadOnlyList<string>[] parts)
        {
            return parts.SelectMany(p => p).ToList();
        }
    }
}
