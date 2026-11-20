namespace MyMorningNews.Sdk.Options
{
    using System.Collections.Generic;
    using MyMorningNews.Sdk.Enums;

    /// <summary>
    /// Filters for listing stored files.
    /// </summary>
    public class ArtifactListOptions : OwnedListOptions
    {
        /// <summary>
        /// Only files of this briefing. Null by default.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Only files of this kind. Null by default.
        /// </summary>
        public ArtifactKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Only pinned (true) or unpinned (false) files. Null by default.
        /// </summary>
        public bool? Pinned { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ArtifactListOptions()
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
            QueryFormat.Add(query, "kind", Kind);
            QueryFormat.Add(query, "pinned", Pinned);
        }
    }
}
