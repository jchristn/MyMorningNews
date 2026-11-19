namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for SearchResult records. Null properties are not applied.
    /// </summary>
    public class SearchResultFilter : EnumerationFilter
    {
        /// <summary>
        /// Match SearchQueryId exactly when set.
        /// </summary>
        public string? SearchQueryId { get; set; } = null;

        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match CanonicalUrlHash exactly when set.
        /// </summary>
        public string? CanonicalUrlHash { get; set; } = null;

        /// <summary>
        /// Match Disposition exactly when set.
        /// </summary>
        public SearchResultDispositionEnum? Disposition { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
