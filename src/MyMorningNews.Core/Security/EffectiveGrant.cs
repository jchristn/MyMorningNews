namespace MyMorningNews.Core.Security
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// One expanded permission grant for a principal.
    /// </summary>
    public class EffectiveGrant
    {
        /// <summary>
        /// Resource type (All is a wildcard).
        /// </summary>
        public ResourceTypeEnum ResourceType { get; set; } = ResourceTypeEnum.All;

        /// <summary>
        /// Operation (All is a wildcard; Write expands to Create, Update, and Delete).
        /// </summary>
        public OperationTypeEnum Operation { get; set; } = OperationTypeEnum.Read;

        /// <summary>
        /// Permit or deny.
        /// </summary>
        public PermissionTypeEnum PermissionType { get; set; } = PermissionTypeEnum.Permit;

        /// <summary>
        /// Tenant or resource scope.
        /// </summary>
        public ResourceScopeEnum ResourceScope { get; set; } = ResourceScopeEnum.Tenant;

        /// <summary>
        /// Resource identifier for resource-scoped grants.
        /// </summary>
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Whether a tenant-scoped grant applies to resources inside the tenant.
        /// </summary>
        public bool InheritsToChildren { get; set; } = true;

        /// <summary>
        /// Source description, for example role:TenantMember.
        /// </summary>
        public string Source { get; set; } = String.Empty;

        /// <summary>
        /// Whether this grant covers a resource type.
        /// </summary>
        /// <param name="type">Requested type.</param>
        /// <returns>True when it matches.</returns>
        public bool MatchesResource(ResourceTypeEnum type)
        {
            return ResourceType == ResourceTypeEnum.All || ResourceType == type;
        }

        /// <summary>
        /// Whether this grant covers an operation.
        /// </summary>
        /// <param name="operation">Requested operation.</param>
        /// <returns>True when it matches.</returns>
        public bool MatchesOperation(OperationTypeEnum operation)
        {
            if (Operation == OperationTypeEnum.All || Operation == operation) return true;
            if (Operation == OperationTypeEnum.Write)
                return operation == OperationTypeEnum.Create || operation == OperationTypeEnum.Update || operation == OperationTypeEnum.Delete;
            return false;
        }
    }
}
