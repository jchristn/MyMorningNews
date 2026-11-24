namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for LlmCall records. Null properties are not applied.
    /// </summary>
    public class LlmCallFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Task exactly when set.
        /// </summary>
        public ModelTaskEnum? Task { get; set; } = null;

        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match RunStageId exactly when set.
        /// </summary>
        public string? RunStageId { get; set; } = null;

        /// <summary>
        /// Match RefinementSessionId exactly when set.
        /// </summary>
        public string? RefinementSessionId { get; set; } = null;

        /// <summary>
        /// Match BriefingId exactly when set.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Match ModelEndpointId exactly when set.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Match Model exactly when set.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Match PromptKey exactly when set.
        /// </summary>
        public string? PromptKey { get; set; } = null;

        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match Success exactly when set.
        /// </summary>
        public bool? Success { get; set; } = null;

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
