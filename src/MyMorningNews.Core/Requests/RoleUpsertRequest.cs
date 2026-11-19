namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create or update a tenant role.
    /// </summary>
    public class RoleUpsertRequest
    {
        /// <summary>
        /// Name (required on create).
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Intended scope.
        /// </summary>
        public ResourceScopeEnum? DefaultScope { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool? Active { get; set; } = null;

        /// <summary>
        /// Permissions mapped to the role; replaces the current set when supplied.
        /// </summary>
        public List<string>? PermissionIds { get; set; } = null;
    }
}
