namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// One search hit recorded for a run, with what the pipeline did with it.
    /// </summary>
    public class SearchResultDto
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// URL as returned by search.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Canonical URL hash.
        /// </summary>
        public string CanonicalUrlHash { get; set; } = String.Empty;

        /// <summary>
        /// Rank in its query.
        /// </summary>
        public int Rank { get; set; } = 0;

        /// <summary>
        /// Disposition: Pending, Fetched, SkippedSeen, SkippedBlocked, SkippedRobots, SkippedLimit, SkippedDuplicate, or FetchFailed.
        /// </summary>
        public string Disposition { get; set; } = String.Empty;

        /// <summary>
        /// Stored source, when fetched.
        /// </summary>
        public string? SourceItemId { get; set; } = null;

        /// <summary>
        /// Creation time (search order).
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
