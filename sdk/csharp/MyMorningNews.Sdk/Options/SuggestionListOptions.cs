namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing suggestions.
    /// </summary>
    public class SuggestionListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only suggestions of this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only suggestions in this status. Null by default.
        /// </summary>
        public SuggestionStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Only suggestions with this priority. Null by default.
        /// </summary>
        public SuggestionPriorityEnum? Priority { get; set; } = null;

        /// <summary>
        /// Include dry-run suggestions. Null by default.
        /// </summary>
        public bool? IncludeDryRun { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SuggestionListOptions()
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
            QueryFormat.Add(query, "status", Status);
            QueryFormat.Add(query, "priority", Priority);
            QueryFormat.Add(query, "includeDryRun", IncludeDryRun);
        }
    }
}
