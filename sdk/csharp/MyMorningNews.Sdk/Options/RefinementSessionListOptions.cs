namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing refinement sessions.
    /// </summary>
    public class RefinementSessionListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only sessions in this status. Null by default.
        /// </summary>
        public RefinementStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Only sessions for this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RefinementSessionListOptions()
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
            QueryFormat.Add(query, "topicId", TopicId);
        }
    }
}
