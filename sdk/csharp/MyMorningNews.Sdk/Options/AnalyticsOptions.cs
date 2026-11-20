namespace MyMorningNews.Sdk.Options
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Scope for analytics reports. The server defaults to the caller's own data for the last 90 days.
    /// </summary>
    public class AnalyticsOptions
    {
        /// <summary>
        /// Only this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Start of the window. Null uses 90 days before ToUtc.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// End of the window. Null uses now.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Administrators: report on this user. Null by default.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Administrators: report on the whole tenant. Null by default.
        /// </summary>
        public bool? All { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AnalyticsOptions()
        {
        }

        /// <summary>
        /// Build the query string values.
        /// </summary>
        /// <returns>Query values.</returns>
        public Dictionary<string, string> ToQuery()
        {
            Dictionary<string, string> query = new Dictionary<string, string>(StringComparer.Ordinal);
            QueryFormat.Add(query, "topicId", TopicId);
            QueryFormat.Add(query, "from", FromUtc);
            QueryFormat.Add(query, "to", ToUtc);
            QueryFormat.Add(query, "userId", UserId);
            QueryFormat.Add(query, "all", All);
            return query;
        }
    }
}
