namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing audio episodes.
    /// </summary>
    public class MediaListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only episodes of this briefing. Null by default.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Only episodes of this edition. Null by default.
        /// </summary>
        public string? EditionId { get; set; } = null;

        /// <summary>
        /// Only episodes in this status. Null by default.
        /// </summary>
        public MediaStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Only episodes of this kind. Null by default.
        /// </summary>
        public MediaKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public MediaListOptions()
        {
        }

        /// <summary>
        /// Add this resource's filters.
        /// </summary>
        /// <param name="query">Query to add to.</param>
        protected override void AddFilters(Dictionary<string, string> query)
        {
            base.AddFilters(query);
            QueryFormat.Add(query, "briefingId", BriefingId);
            QueryFormat.Add(query, "editionId", EditionId);
            QueryFormat.Add(query, "status", Status);
            QueryFormat.Add(query, "kind", Kind);
        }
    }
}
