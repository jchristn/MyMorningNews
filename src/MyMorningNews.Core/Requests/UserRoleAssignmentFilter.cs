namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for UserRoleAssignment records. Null properties are not applied.
    /// </summary>
    public class UserRoleAssignmentFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match RoleId exactly when set.
        /// </summary>
        public string? RoleId { get; set; } = null;

        /// <summary>
        /// Match ResourceScope exactly when set.
        /// </summary>
        public ResourceScopeEnum? ResourceScope { get; set; } = null;

        /// <summary>
        /// Match ResourceId exactly when set.
        /// </summary>
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
