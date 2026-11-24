namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Briefing records. Null properties are not applied.
    /// </summary>
    public class BriefingFilter : EnumerationQuery
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match Kind exactly when set.
        /// </summary>
        public BriefingKindEnum? Kind { get; set; } = null;

        /// <summary>
        /// Match EditionId exactly when set.
        /// </summary>
        public string? EditionId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match ResearchRequestId exactly when set.
        /// </summary>
        public string? ResearchRequestId { get; set; } = null;

        /// <summary>
        /// Match Status exactly when set.
        /// </summary>
        public BriefingStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Match Pinned exactly when set.
        /// </summary>
        public bool? Pinned { get; set; } = null;

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
