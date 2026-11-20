namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;

    /// <summary>
    /// Filters for listing fetched sources.
    /// </summary>
    public class SourceListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only sources of this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only sources of this run. Null by default.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SourceListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "topicId", TopicId);
            QueryFormat.Add(query, "runId", RunId);
        }
    }
}
