namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// A search provider choice for a topic.
    /// </summary>
    public class TopicSearchProviderRequest
    {
        /// <summary>
        /// Search provider.
        /// </summary>
        public string SearchProviderId { get; set; } = String.Empty;

        /// <summary>
        /// Enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Results per query override.
        /// </summary>
        public int? MaxResults { get; set; } = null;
    }
}
