namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for User records. Null properties are not applied.
    /// </summary>
    public class UserFilter : EnumerationFilter
    {
        /// <summary>
        /// Match Email exactly when set.
        /// </summary>
        public string? Email { get; set; } = null;

        /// <summary>
        /// Match IsAdmin exactly when set.
        /// </summary>
        public bool? IsAdmin { get; set; } = null;

        /// <summary>
        /// Match IsTenantAdmin exactly when set.
        /// </summary>
        public bool? IsTenantAdmin { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
