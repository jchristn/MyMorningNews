namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for SearchQuery records. Null properties are not applied.
    /// </summary>
    public class SearchQueryFilter : EnumerationFilter
    {
        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match SearchProviderId exactly when set.
        /// </summary>
        public string? SearchProviderId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
