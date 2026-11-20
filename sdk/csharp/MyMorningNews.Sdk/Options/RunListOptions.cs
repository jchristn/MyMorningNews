namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing runs.
    /// </summary>
    public class RunListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only runs of this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only runs in this status. Null by default.
        /// </summary>
        public RunStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Only runs started this way. Null by default.
        /// </summary>
        public RunTriggerEnum? Trigger { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RunListOptions()
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
            QueryFormat.Add(query, "trigger", Trigger);
        }
    }
}
