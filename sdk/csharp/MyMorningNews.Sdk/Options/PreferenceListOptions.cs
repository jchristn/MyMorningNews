namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing learned preferences.
    /// </summary>
    public class PreferenceListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only preferences of this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only preferences in this status. Null by default.
        /// </summary>
        public PreferenceStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Only preferences with this origin. Null by default.
        /// </summary>
        public PreferenceOriginEnum? Origin { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PreferenceListOptions()
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
            QueryFormat.Add(query, "origin", Origin);
        }
    }
}
