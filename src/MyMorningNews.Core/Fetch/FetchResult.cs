namespace MyMorningNews.Core.Fetch
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// The outcome of fetching one page.
    /// </summary>
    public class FetchResult
    {
        /// <summary>
        /// Requested URL.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// URL after redirects.
        /// </summary>
        public string? FinalUrl { get; set; } = null;

        /// <summary>
        /// Canonical URL of the final page.
        /// </summary>
        public string? CanonicalUrl { get; set; } = null;

        /// <summary>
        /// Disposition.
        /// </summary>
        public FetchDispositionEnum Disposition { get; set; } = FetchDispositionEnum.Failed;

        /// <summary>
        /// HTTP status, when a response arrived.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Content type.
        /// </summary>
        public string? ContentType { get; set; } = null;

        /// <summary>
        /// Page title, when found.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// Extracted text as Markdown (HTML is converted; plain text and Markdown are kept).
        /// </summary>
        public string? Text { get; set; } = null;

        /// <summary>
        /// Bytes downloaded.
        /// </summary>
        public long Bytes { get; set; } = 0;

        /// <summary>
        /// SHA-256 of the downloaded bytes (hex).
        /// </summary>
        public string? ContentHash { get; set; } = null;

        /// <summary>
        /// Error message, when not fetched.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// UTC fetch time.
        /// </summary>
        public DateTime FetchedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Hit, Miss, Forced, Bypass, or Error. Default Bypass. Forced always retrieves from the origin.</summary>
        public string CacheStatus { get; set; } = "Bypass";

        /// <summary>Whether this call successfully published a new cache generation. Default false.</summary>
        public bool CacheUpdated { get; set; } = false;

        /// <summary>Origin body bytes received during this call; zero on a cache hit. Excludes robots requests.</summary>
        public long NetworkBytes { get; set; } = 0;

        internal byte[]? RawData { get; set; }
        internal DateTime? CacheExpiresUtc { get; set; }
    }
}
