namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Finding records. Null properties are not applied.
    /// </summary>
    public class FindingFilter : EnumerationFilter
    {
        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match DimensionId exactly when set.
        /// </summary>
        public string? DimensionId { get; set; } = null;

        /// <summary>
        /// Only findings with a relevance score at or above this value (0 to 100), or null.
        /// </summary>
        public double? MinRelevanceScore { get; set; } = null;

        /// <summary>
        /// Leave out findings in this tier (for example BelowThreshold), or null.
        /// </summary>
        public FindingTierEnum? ExcludeTier { get; set; } = null;

        /// <summary>
        /// Match SourceItemId exactly when set.
        /// </summary>
        public string? SourceItemId { get; set; } = null;

        /// <summary>
        /// Match Category exactly when set.
        /// </summary>
        public FindingCategoryEnum? Category { get; set; } = null;

        /// <summary>
        /// Match Tier exactly when set.
        /// </summary>
        public FindingTierEnum? Tier { get; set; } = null;

        /// <summary>
        /// Match IsDuplicate exactly when set.
        /// </summary>
        public bool? IsDuplicate { get; set; } = null;

        /// <summary>
        /// Match UserRating exactly when set.
        /// </summary>
        public string? UserRating { get; set; } = null;

        /// <summary>
        /// Match IsDryRun exactly when set.
        /// </summary>
        public bool? IsDryRun { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
