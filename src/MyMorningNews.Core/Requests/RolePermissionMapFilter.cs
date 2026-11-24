namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for RolePermissionMap records. Null properties are not applied.
    /// </summary>
    public class RolePermissionMapFilter : EnumerationQuery
    {
        /// <summary>
        /// Match RoleId exactly when set.
        /// </summary>
        public string? RoleId { get; set; } = null;

        /// <summary>
        /// Match PermissionId exactly when set.
        /// </summary>
        public string? PermissionId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
