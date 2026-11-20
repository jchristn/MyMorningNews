namespace MyMorningNews.Core.Analytics
{
    using System;

    /// <summary>
    /// What one search provider's results turned into. A result is used when a non-duplicate finding at or above the
    /// display threshold in the same run came from the same source item. When two providers return the same page,
    /// both are credited.
    /// </summary>
    public class ProviderYieldRow
    {
        /// <summary>
        /// Provider, as stored on the search result (unknown when missing).
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Search results returned.
        /// </summary>
        public int Results { get; set; } = 0;

        /// <summary>
        /// Results that were read (disposition Fetched).
        /// </summary>
        public int Fetched { get; set; } = 0;

        /// <summary>
        /// Results that led to at least one finding.
        /// </summary>
        public int Used { get; set; } = 0;

        /// <summary>
        /// Distinct findings that came from this provider's results.
        /// </summary>
        public int Findings { get; set; } = 0;

        /// <summary>
        /// Used over results, from 0 to 1. Null when there are no results.
        /// </summary>
        public double? Yield { get; set; } = null;
    }
}
