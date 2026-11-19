namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for SourceItem records. Null properties are not applied.
    /// </summary>
    public class SourceItemFilter : EnumerationFilter
    {
        /// <summary>
        /// Match CanonicalUrlHash exactly when set.
        /// </summary>
        public string? CanonicalUrlHash { get; set; } = null;

        /// <summary>
        /// Match Domain exactly when set.
        /// </summary>
        public string? Domain { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
