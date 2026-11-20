namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing prompt templates.
    /// </summary>
    public class PromptListOptions : ListOptions
    {
        /// <summary>
        /// Only this prompt key. Null by default.
        /// </summary>
        public string? Key { get; set; } = null;

        /// <summary>
        /// Only overrides for this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only customized (true) or default (false) prompts. Null by default.
        /// </summary>
        public bool? IsCustomized { get; set; } = null;

        /// <summary>
        /// Only tenant prompts, not topic overrides. Null by default.
        /// </summary>
        public bool? TenantOnly { get; set; } = null;

        /// <summary>
        /// Only prompts for this model task. Null by default.
        /// </summary>
        public ModelTaskEnum? Role { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PromptListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "key", Key);
            QueryFormat.Add(query, "topicId", TopicId);
            QueryFormat.Add(query, "isCustomized", IsCustomized);
            QueryFormat.Add(query, "tenantOnly", TenantOnly);
            QueryFormat.Add(query, "role", Role);
        }
    }
}
