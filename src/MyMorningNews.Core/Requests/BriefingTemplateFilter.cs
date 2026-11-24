namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for BriefingTemplate records. Null properties are not applied.
    /// </summary>
    public class BriefingTemplateFilter : EnumerationQuery
    {
        /// <summary>Only shared templates and this user's templates. Null disables visibility filtering.</summary>
        public string? VisibleToUserId { get; set; } = null;

        /// <summary>Additional user visible to an administrator. Null adds no user.</summary>
        public string? AdditionalUserId { get; set; } = null;

        /// <summary>Restrict to shared templates (true) or private templates (false). Null includes both.</summary>
        public bool? Shared { get; set; } = null;

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
