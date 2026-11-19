namespace MyMorningNews.Core.Search
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One search query issued by the research pipeline or a provider test.
    /// </summary>
    public class ResearchSearchRequest
    {
        /// <summary>
        /// Query text.
        /// </summary>
        public string Query { get; set; } = String.Empty;

        /// <summary>
        /// Maximum results. Default 10. Minimum 1, maximum 50.
        /// </summary>
        public int MaxResults
        {
            get { return _MaxResults; }
            set { _MaxResults = Math.Clamp(value, 1, 50); }
        }

        /// <summary>
        /// Only results from these domains, where the provider supports it.
        /// </summary>
        public List<string> IncludeDomains { get; set; } = new List<string>();

        /// <summary>
        /// Never results from these domains (also filtered after the provider responds).
        /// </summary>
        public List<string> ExcludeDomains { get; set; } = new List<string>();

        /// <summary>
        /// Feed URLs for the RSS source (the topic's FeedUrl constraints).
        /// </summary>
        public List<string> FeedUrls { get; set; } = new List<string>();

        /// <summary>
        /// Only items published after this time (UTC), where known.
        /// </summary>
        public DateTime? SinceUtc { get; set; } = null;

        private int _MaxResults = 10;
    }
}
