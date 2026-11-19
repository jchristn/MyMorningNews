namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create or update a role assignment for a user or credential.
    /// </summary>
    public class AssignmentUpsertRequest
    {
        /// <summary>
        /// Role identifier.
        /// </summary>
        public string? RoleId { get; set; } = null;

        /// <summary>
        /// Built-in role name.
        /// </summary>
        public string? RoleName { get; set; } = null;

        /// <summary>
        /// Tenant or Resource.
        /// </summary>
        public ResourceScopeEnum ResourceScope { get; set; } = ResourceScopeEnum.Tenant;

        /// <summary>
        /// Resource identifier for resource scope.
        /// </summary>
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Tenant grants apply to resources in the tenant.
        /// </summary>
        public bool InheritsToChildren { get; set; } = true;

        /// <summary>
        /// Direct operations (credential scopes only).
        /// </summary>
        public List<string> OperationTypes { get; set; } = new List<string>();

        /// <summary>
        /// Direct resource types (credential scopes only).
        /// </summary>
        public List<string> ResourceTypes { get; set; } = new List<string>();

        /// <summary>
        /// Active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}
