namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing predictions.
    /// </summary>
    public class PredictionListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only predictions of this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only predictions in this status. Null by default.
        /// </summary>
        public PredictionStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Include dry-run predictions. Null by default.
        /// </summary>
        public bool? IncludeDryRun { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PredictionListOptions()
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
            QueryFormat.Add(query, "includeDryRun", IncludeDryRun);
        }
    }
}
