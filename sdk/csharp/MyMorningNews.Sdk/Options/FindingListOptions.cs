namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing findings. The server excludes duplicates, dry runs, and below-threshold findings unless asked.
    /// </summary>
    public class FindingListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only findings of this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only findings of this run. Null by default.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Only findings in this tier. Null by default.
        /// </summary>
        public FindingTierEnum? Tier { get; set; } = null;

        /// <summary>
        /// Only findings in this category. Null by default.
        /// </summary>
        public FindingCategoryEnum? Category { get; set; } = null;

        /// <summary>
        /// Include duplicates. Null by default.
        /// </summary>
        public bool? IncludeDuplicates { get; set; } = null;

        /// <summary>
        /// Include dry-run findings. Null by default.
        /// </summary>
        public bool? IncludeDryRun { get; set; } = null;

        /// <summary>
        /// Minimum relevance score (0 to 100). Null by default.
        /// </summary>
        public int? MinScore { get; set; } = null;

        /// <summary>
        /// Only findings for this dimension. Null by default.
        /// </summary>
        public string? DimensionId { get; set; } = null;

        /// <summary>
        /// Include findings below the display threshold. Null by default.
        /// </summary>
        public bool? IncludeBelowThreshold { get; set; } = null;

        /// <summary>
        /// Include findings the owner hid. Null by default.
        /// </summary>
        public bool? IncludeHidden { get; set; } = null;

        /// <summary>
        /// Only pinned (true) or unpinned (false) findings. Null by default.
        /// </summary>
        public bool? Pinned { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public FindingListOptions()
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
            QueryFormat.Add(query, "tier", Tier);
            QueryFormat.Add(query, "category", Category);
            QueryFormat.Add(query, "includeDuplicates", IncludeDuplicates);
            QueryFormat.Add(query, "includeDryRun", IncludeDryRun);
            QueryFormat.Add(query, "minScore", MinScore);
            QueryFormat.Add(query, "dimensionId", DimensionId);
            QueryFormat.Add(query, "includeBelowThreshold", IncludeBelowThreshold);
            QueryFormat.Add(query, "includeHidden", IncludeHidden);
            QueryFormat.Add(query, "pinned", Pinned);
        }
    }
}
