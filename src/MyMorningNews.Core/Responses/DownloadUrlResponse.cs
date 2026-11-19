namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// A time-limited download URL, when the store offers one.
    /// </summary>
    public class DownloadUrlResponse
    {
        /// <summary>
        /// URL, or null to download through the server.
        /// </summary>
        public string? Url { get; set; } = null;

        /// <summary>
        /// Expiry.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Server path that always works.
        /// </summary>
        public string ContentPath { get; set; } = String.Empty;
    }
}
