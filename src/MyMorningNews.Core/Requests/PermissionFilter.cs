namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Permission records. Null properties are not applied.
    /// </summary>
    public class PermissionFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Name exactly when set.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Match PermissionType exactly when set.
        /// </summary>
        public PermissionTypeEnum? PermissionType { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
