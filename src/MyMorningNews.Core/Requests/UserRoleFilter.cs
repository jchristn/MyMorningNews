namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for UserRole records. Null properties are not applied.
    /// </summary>
    public class UserRoleFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Name exactly when set.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Match IsBuiltIn exactly when set.
        /// </summary>
        public bool? IsBuiltIn { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
