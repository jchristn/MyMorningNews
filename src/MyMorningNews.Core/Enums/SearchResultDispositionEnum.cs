namespace MyMorningNews.Core.Enums
{
    /// <summary>
    /// What happened to a search result.
    /// </summary>
    public enum SearchResultDispositionEnum
    {
        /// <summary>
        /// Not processed.
        /// </summary>
        Pending,

        /// <summary>
        /// Fetched.
        /// </summary>
        Fetched,

        /// <summary>
        /// Already seen in this topic.
        /// </summary>
        SkippedSeen,

        /// <summary>
        /// Blocked domain.
        /// </summary>
        SkippedBlocked,

        /// <summary>
        /// Disallowed by robots.txt.
        /// </summary>
        SkippedRobots,

        /// <summary>
        /// Over the fetch limit.
        /// </summary>
        SkippedLimit,

        /// <summary>
        /// Duplicate in this run.
        /// </summary>
        SkippedDuplicate,

        /// <summary>
        /// Fetch failed.
        /// </summary>
        FetchFailed
    }
}
