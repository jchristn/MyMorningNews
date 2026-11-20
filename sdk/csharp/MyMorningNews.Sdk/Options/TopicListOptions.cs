namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing topics.
    /// </summary>
    public class TopicListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only topics in this status. Null by default.
        /// </summary>
        public TopicStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Only one-time (true) or recurring (false) topics. Null by default.
        /// </summary>
        public bool? IsOneTime { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TopicListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "status", Status);
            QueryFormat.Add(query, "isOneTime", IsOneTime);
        }
    }
}
