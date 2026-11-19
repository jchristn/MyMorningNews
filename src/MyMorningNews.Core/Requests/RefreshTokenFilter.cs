namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for RefreshToken records. Null properties are not applied.
    /// </summary>
    public class RefreshTokenFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match SessionId exactly when set.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Match FamilyId exactly when set.
        /// </summary>
        public string? FamilyId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
