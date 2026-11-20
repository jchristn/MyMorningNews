namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing model task assignments.
    /// </summary>
    public class ModelAssignmentListOptions : ListOptions
    {
        /// <summary>
        /// Only assignments for this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only assignments for this task. Null by default.
        /// </summary>
        public ModelTaskEnum? Task { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ModelAssignmentListOptions()
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
            QueryFormat.Add(query, "task", Task);
        }
    }
}
