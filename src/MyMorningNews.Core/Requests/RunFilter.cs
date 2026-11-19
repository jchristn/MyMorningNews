namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Run records. Null properties are not applied.
    /// </summary>
    public class RunFilter : EnumerationFilter
    {
        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match ScheduleId exactly when set.
        /// </summary>
        public string? ScheduleId { get; set; } = null;

        /// <summary>
        /// Match ResearchRequestId exactly when set.
        /// </summary>
        public string? ResearchRequestId { get; set; } = null;

        /// <summary>
        /// Match Trigger exactly when set.
        /// </summary>
        public RunTriggerEnum? Trigger { get; set; } = null;

        /// <summary>
        /// Match Status exactly when set.
        /// </summary>
        public RunStatusEnum? Status { get; set; } = null;

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
