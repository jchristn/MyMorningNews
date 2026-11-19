namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for BriefingSection records. Null properties are not applied.
    /// </summary>
    public class BriefingSectionFilter : EnumerationFilter
    {
        /// <summary>
        /// Match BriefingId exactly when set.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
