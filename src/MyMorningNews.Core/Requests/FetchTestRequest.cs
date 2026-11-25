namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Fetch one page as the research pipeline would.
    /// </summary>
    public class FetchTestRequest
    {
        /// <summary>
        /// URL.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>Default false. Retrieve from origin even with a fresh cache entry; update eligible content after success. No cached fallback on failure.</summary>
        public bool ForceRefresh { get; set; } = false;
    }
}
