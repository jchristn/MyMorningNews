namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for BriefingProgress records. Null properties are not applied.
    /// </summary>
    public class BriefingProgressFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match BriefingId exactly when set.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Match Completed exactly when set.
        /// </summary>
        public bool? Completed { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
