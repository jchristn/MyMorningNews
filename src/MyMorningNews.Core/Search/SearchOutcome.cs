namespace MyMorningNews.Core.Search
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The result of searching one provider: hits, timing, and an error when the provider failed.
    /// </summary>
    public class SearchOutcome
    {
        /// <summary>
        /// Search provider ID.
        /// </summary>
        public string ProviderId { get; set; } = String.Empty;

        /// <summary>
        /// Provider kind.
        /// </summary>
        public string Kind { get; set; } = String.Empty;

        /// <summary>
        /// Whether the provider answered.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Error message when it failed.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Hits after domain filtering and de-duplication.
        /// </summary>
        public List<SearchHit> Hits { get; set; } = new List<SearchHit>();
    }
}
