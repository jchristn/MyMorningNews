namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Run a test query against a provider.
    /// </summary>
    public class SearchTestRequest
    {
        /// <summary>
        /// Query.
        /// </summary>
        public string Query { get; set; } = "artificial intelligence";

        /// <summary>
        /// Feed URLs for RSS providers.
        /// </summary>
        public List<string> FeedUrls { get; set; } = new List<string>();

        /// <summary>
        /// Results, 1 to 50.
        /// </summary>
        public int MaxResults { get; set; } = 5;
    }
}
