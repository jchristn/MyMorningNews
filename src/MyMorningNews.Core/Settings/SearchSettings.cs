namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Search and fetch settings (plan sections 8.3 and 15).
    /// </summary>
    public class SearchSettings
    {
        #region Public-Members

        /// <summary>
        /// SearXNG base URL used when a SearXNG provider has no endpoint. Default http://127.0.0.1:12103 (compose sets http://searxng:8080).
        /// </summary>
        [Setting(Description = "Default SearXNG URL.", EnvironmentVariable = "SEARXNG_URL")]
        public string SearxngUrl { get; set; } = "http://127.0.0.1:12103";

        /// <summary>
        /// Hacker News search API base URL. Default https://hn.algolia.com/api/v1.
        /// </summary>
        [Setting(Description = "Hacker News (Algolia) API base URL.")]
        public string HackerNewsUrl { get; set; } = "https://hn.algolia.com/api/v1";

        /// <summary>
        /// arXiv API base URL. Default https://export.arxiv.org/api.
        /// </summary>
        [Setting(Description = "arXiv API base URL.")]
        public string ArxivUrl { get; set; } = "https://export.arxiv.org/api";

        /// <summary>
        /// User agent for search and fetch requests. Default "MyMorningNews/0.1 (+https://mymorningnews.ai)".
        /// </summary>
        [Setting(Description = "User agent for search and fetch.")]
        public string UserAgent { get; set; } = "MyMorningNews/0.1 (+https://mymorningnews.ai)";

        /// <summary>
        /// Respect robots.txt when fetching. Default true.
        /// </summary>
        [Setting(Description = "Respect robots.txt when fetching.")]
        public bool RespectRobots { get; set; } = true;

        /// <summary>
        /// Fetch timeout in milliseconds. Default 20000. Minimum 1000, maximum 120000.
        /// </summary>
        [Setting(Description = "Fetch timeout in milliseconds.", Minimum = 1000, Maximum = 120000)]
        public int FetchTimeoutMs
        {
            get { return _FetchTimeoutMs; }
            set { _FetchTimeoutMs = Math.Clamp(value, 1000, 120000); }
        }

        /// <summary>
        /// Minimum delay between requests to one domain in milliseconds. Default 1500. Minimum 0, maximum 60000.
        /// </summary>
        [Setting(Description = "Delay between requests to one domain in milliseconds.", Minimum = 0, Maximum = 60000)]
        public int PerDomainDelayMs
        {
            get { return _PerDomainDelayMs; }
            set { _PerDomainDelayMs = Math.Clamp(value, 0, 60000); }
        }

        /// <summary>
        /// Largest page fetched in bytes. Default 2097152 (2 MB). Minimum 65536, maximum 52428800.
        /// </summary>
        [Setting(Description = "Largest page fetched in bytes.", Minimum = 65536, Maximum = 52428800)]
        public int MaxFetchBytes
        {
            get { return _MaxFetchBytes; }
            set { _MaxFetchBytes = Math.Clamp(value, 65536, 52428800); }
        }

        /// <summary>
        /// Attempts per search request, counting the first. A request that gets 408, 429, 500, 502, 503, or 504 is
        /// retried after the Retry-After delay (at most 5 seconds) or an exponential backoff from 500 ms. Default 3.
        /// Minimum 1 (no retry), maximum 6.
        /// </summary>
        [Setting(Description = "Attempts per search request (retries on 429 and 5xx).", Minimum = 1, Maximum = 6)]
        public int MaxAttempts
        {
            get { return _MaxAttempts; }
            set { _MaxAttempts = Math.Clamp(value, 1, 6); }
        }

        #endregion

        #region Private-Members

        private int _MaxAttempts = 3;
        private int _FetchTimeoutMs = 20000;
        private int _PerDomainDelayMs = 1500;
        private int _MaxFetchBytes = 2097152;

        #endregion
    }
}
