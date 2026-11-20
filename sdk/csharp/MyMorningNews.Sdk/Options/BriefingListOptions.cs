namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing briefings. Dry runs and previews are excluded unless IncludeDryRun is true.
    /// </summary>
    public class BriefingListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only briefings of this kind. Null by default.
        /// </summary>
        public BriefingKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Only briefings of this edition. Null by default.
        /// </summary>
        public string? EditionId { get; set; } = null;

        /// <summary>
        /// Only briefings of this topic. Null by default.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Only briefings of this run. Null by default.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Only briefings in this status. Null by default.
        /// </summary>
        public BriefingStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Only pinned (true) or unpinned (false) briefings. Null by default.
        /// </summary>
        public bool? Pinned { get; set; } = null;

        /// <summary>
        /// Include dry runs and previews. Null by default.
        /// </summary>
        public bool? IncludeDryRun { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BriefingListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "kind", Kind);
            QueryFormat.Add(query, "editionId", EditionId);
            QueryFormat.Add(query, "topicId", TopicId);
            QueryFormat.Add(query, "runId", RunId);
            QueryFormat.Add(query, "status", Status);
            QueryFormat.Add(query, "pinned", Pinned);
            QueryFormat.Add(query, "includeDryRun", IncludeDryRun);
        }
    }
}
