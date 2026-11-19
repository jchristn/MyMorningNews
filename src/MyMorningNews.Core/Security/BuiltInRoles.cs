namespace MyMorningNews.Core.Security
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Built-in roles seeded with a null tenant, available to every tenant, and refreshed at startup.
    /// </summary>
    public static class BuiltInRoles
    {
        #region Public-Members

        /// <summary>
        /// Full access within a tenant.
        /// </summary>
        public const string TenantAdmin = "TenantAdmin";

        /// <summary>
        /// Security administration.
        /// </summary>
        public const string SecurityAdmin = "SecurityAdmin";

        /// <summary>
        /// Read-only security review.
        /// </summary>
        public const string Auditor = "Auditor";

        /// <summary>
        /// Full access to one topic.
        /// </summary>
        public const string TopicAdmin = "TopicAdmin";

        /// <summary>
        /// Read, write, and execute on assigned domain data.
        /// </summary>
        public const string Editor = "Editor";

        /// <summary>
        /// Read-only on assigned domain data.
        /// </summary>
        public const string Viewer = "Viewer";

        /// <summary>
        /// Default role for users without assignments: works with their own domain data.
        /// </summary>
        public const string TenantMember = "TenantMember";

        /// <summary>
        /// Template for tenant-defined roles; no permissions.
        /// </summary>
        public const string Custom = "Custom";

        /// <summary>
        /// Domain resource types (non-administrative).
        /// </summary>
        public static readonly IReadOnlyList<ResourceTypeEnum> DomainTypes = new List<ResourceTypeEnum>
        {
            ResourceTypeEnum.Topic, ResourceTypeEnum.Schedule, ResourceTypeEnum.RefinementSession, ResourceTypeEnum.Run, ResourceTypeEnum.Finding,
            ResourceTypeEnum.Source, ResourceTypeEnum.Evidence, ResourceTypeEnum.Prediction, ResourceTypeEnum.Suggestion, ResourceTypeEnum.Feedback,
            ResourceTypeEnum.LearnedPreference, ResourceTypeEnum.Briefing, ResourceTypeEnum.Edition, ResourceTypeEnum.Template, ResourceTypeEnum.Prompt,
            ResourceTypeEnum.Media, ResourceTypeEnum.EmailMessage, ResourceTypeEnum.Analytics
        };

        /// <summary>
        /// Cost and model call accounting types (tenant-wide, across users). Read by tenant administrators and auditors only;
        /// members see their own usage through /v1.0/api/usage/me.
        /// </summary>
        public static readonly IReadOnlyList<ResourceTypeEnum> AccountingTypes = new List<ResourceTypeEnum>
        {
            ResourceTypeEnum.Usage, ResourceTypeEnum.LlmCall
        };

        /// <summary>
        /// Tenant configuration resource types members may read.
        /// </summary>
        public static readonly IReadOnlyList<ResourceTypeEnum> ConfigurationTypes = new List<ResourceTypeEnum>
        {
            ResourceTypeEnum.ModelEndpoint, ResourceTypeEnum.SearchProvider, ResourceTypeEnum.Voice, ResourceTypeEnum.Tenant,
            ResourceTypeEnum.VaultCredential, ResourceTypeEnum.McpServer
        };

        /// <summary>
        /// Every built-in role definition.
        /// </summary>
        public static IReadOnlyList<BuiltInRoleDefinition> All { get; } = Build();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Find a built-in definition by name.
        /// </summary>
        /// <param name="name">Role name.</param>
        /// <returns>Definition, or null.</returns>
        public static BuiltInRoleDefinition? Find(string? name)
        {
            if (String.IsNullOrWhiteSpace(name)) return null;
            return All.FirstOrDefault(r => String.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Private-Methods

        private static List<BuiltInRoleDefinition> Build()
        {
            List<ResourceTypeEnum> security = new List<ResourceTypeEnum>
            {
                ResourceTypeEnum.User, ResourceTypeEnum.Credential, ResourceTypeEnum.Session, ResourceTypeEnum.Role,
                ResourceTypeEnum.Permission, ResourceTypeEnum.Assignment, ResourceTypeEnum.Audit, ResourceTypeEnum.Tenant
            };

            return new List<BuiltInRoleDefinition>
            {
                new BuiltInRoleDefinition(TenantAdmin, "All permissions on all resources within the tenant.", ResourceScopeEnum.Tenant, true, new List<BuiltInPermissionDefinition>
                {
                    Permit(new[] { ResourceTypeEnum.All }, OperationTypeEnum.All)
                }),
                new BuiltInRoleDefinition(SecurityAdmin, "Administers users, credentials, roles, permissions, assignments, sessions, and audit.", ResourceScopeEnum.Tenant, true, new List<BuiltInPermissionDefinition>
                {
                    Permit(security.Concat(new[] { ResourceTypeEnum.Admin }).ToArray(), OperationTypeEnum.Admin),
                    Permit(security.Concat(new[] { ResourceTypeEnum.RequestHistory }).ToArray(), OperationTypeEnum.Read)
                }),
                new BuiltInRoleDefinition(Auditor, "Read-only access to security records and cost accounting.", ResourceScopeEnum.Tenant, true, new List<BuiltInPermissionDefinition>
                {
                    Permit(security.Concat(new[] { ResourceTypeEnum.RequestHistory }).Concat(AccountingTypes).ToArray(), OperationTypeEnum.Read)
                }),
                new BuiltInRoleDefinition(TopicAdmin, "All permissions on one assigned topic and its children.", ResourceScopeEnum.Resource, true, new List<BuiltInPermissionDefinition>
                {
                    Permit(DomainTypes.ToArray(), OperationTypeEnum.All)
                }),
                new BuiltInRoleDefinition(Editor, "Read, write, and execute on assigned domain data.", ResourceScopeEnum.Resource, false, new List<BuiltInPermissionDefinition>
                {
                    Permit(DomainTypes.ToArray(), OperationTypeEnum.Read, OperationTypeEnum.Write, OperationTypeEnum.Execute),
                    Permit(ConfigurationTypes.ToArray(), OperationTypeEnum.Read)
                }),
                new BuiltInRoleDefinition(Viewer, "Read-only on assigned domain data.", ResourceScopeEnum.Resource, false, new List<BuiltInPermissionDefinition>
                {
                    Permit(DomainTypes.ToArray(), OperationTypeEnum.Read),
                    Permit(ConfigurationTypes.ToArray(), OperationTypeEnum.Read)
                }),
                new BuiltInRoleDefinition(TenantMember, "Works with their own topics, briefings, and feedback; reads tenant configuration.", ResourceScopeEnum.Tenant, false, new List<BuiltInPermissionDefinition>
                {
                    Permit(DomainTypes.ToArray(), OperationTypeEnum.Read, OperationTypeEnum.Write, OperationTypeEnum.Execute),
                    Permit(ConfigurationTypes.ToArray(), OperationTypeEnum.Read),
                    Permit(new[] { ResourceTypeEnum.User }, OperationTypeEnum.Read)
                }),
                new BuiltInRoleDefinition(Custom, "Template for tenant-defined roles; carries no permissions.", ResourceScopeEnum.Tenant, false, new List<BuiltInPermissionDefinition>())
            };
        }

        private static BuiltInPermissionDefinition Permit(ResourceTypeEnum[] types, params OperationTypeEnum[] operations)
        {
            return new BuiltInPermissionDefinition(PermissionTypeEnum.Permit, types, operations);
        }

        #endregion
    }
}
