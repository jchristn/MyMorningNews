namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for FeedbackReasonTag records. Null properties are not applied.
    /// </summary>
    public class FeedbackReasonTagFilter : EnumerationQuery
    {
        /// <summary>
        /// Match FeedbackId exactly when set.
        /// </summary>
        public string? FeedbackId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match Tag exactly when set.
        /// </summary>
        public string? Tag { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
