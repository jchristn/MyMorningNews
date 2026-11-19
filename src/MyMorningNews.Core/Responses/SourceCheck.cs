namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// A suggested source with the result of a live fetch.
    /// </summary>
    public class SourceCheck
    {
        /// <summary>
        /// URL.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// FeedUrl or SeedUrl.
        /// </summary>
        public string Kind { get; set; } = String.Empty;

        /// <summary>
        /// Why it was suggested.
        /// </summary>
        public string Reason { get; set; } = String.Empty;

        /// <summary>
        /// Dimension it serves.
        /// </summary>
        public string? Dimension { get; set; } = null;

        /// <summary>
        /// Fetched successfully.
        /// </summary>
        public bool Reachable { get; set; } = false;

        /// <summary>
        /// HTTP status.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Content type.
        /// </summary>
        public string? ContentType { get; set; } = null;

        /// <summary>
        /// Fetch error.
        /// </summary>
        public string? Error { get; set; } = null;
    }
}
