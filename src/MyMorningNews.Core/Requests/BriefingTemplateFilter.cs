namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for BriefingTemplate records. Null properties are not applied.
    /// </summary>
    public class BriefingTemplateFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match DefaultKey exactly when set.
        /// </summary>
        public string? DefaultKey { get; set; } = null;

        /// <summary>
        /// Match IsDefault exactly when set.
        /// </summary>
        public bool? IsDefault { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
