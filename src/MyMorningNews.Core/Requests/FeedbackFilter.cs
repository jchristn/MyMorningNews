namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Feedback records. Null properties are not applied.
    /// </summary>
    public class FeedbackFilter : EnumerationQuery
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match TargetType exactly when set.
        /// </summary>
        public FeedbackTargetTypeEnum? TargetType { get; set; } = null;

        /// <summary>
        /// Match TargetId exactly when set.
        /// </summary>
        public string? TargetId { get; set; } = null;

        /// <summary>
        /// Match TopicId exactly when set.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Match Relevance exactly when set.
        /// </summary>
        public RelevanceRatingEnum? Relevance { get; set; } = null;

        /// <summary>
        /// Match Accuracy exactly when set.
        /// </summary>
        public AccuracyRatingEnum? Accuracy { get; set; } = null;

        /// <summary>
        /// Match Usefulness exactly when set.
        /// </summary>
        public UsefulnessRatingEnum? Usefulness { get; set; } = null;

        /// <summary>
        /// Match Channel exactly when set.
        /// </summary>
        public FeedbackChannelEnum? Channel { get; set; } = null;

        /// <summary>
        /// Match EmailMessageId exactly when set.
        /// </summary>
        public string? EmailMessageId { get; set; } = null;

        /// <summary>
        /// Match IsCurrent exactly when set.
        /// </summary>
        public bool? IsCurrent { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
