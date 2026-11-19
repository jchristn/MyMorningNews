namespace MyMorningNews.Core.Feedback
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// A rating to record. Give an action (as in email links) or explicit ratings.
    /// </summary>
    public class FeedbackInput
    {
        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// User.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Item type.
        /// </summary>
        public FeedbackTargetTypeEnum TargetType { get; set; } = FeedbackTargetTypeEnum.Finding;

        /// <summary>
        /// Item.
        /// </summary>
        public string TargetId { get; set; } = String.Empty;

        /// <summary>
        /// Action (relevant, somewhat, not-relevant, accurate, partially, inaccurate, too-early, useful, not-useful, done, snooze), or null.
        /// </summary>
        public string? Action { get; set; } = null;

        /// <summary>
        /// Relevance, or null.
        /// </summary>
        public RelevanceRatingEnum? Relevance { get; set; } = null;

        /// <summary>
        /// Accuracy, or null.
        /// </summary>
        public AccuracyRatingEnum? Accuracy { get; set; } = null;

        /// <summary>
        /// Usefulness, or null.
        /// </summary>
        public UsefulnessRatingEnum? Usefulness { get; set; } = null;

        /// <summary>
        /// Why (up to 2000 characters), or null.
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Channel.
        /// </summary>
        public FeedbackChannelEnum Channel { get; set; } = FeedbackChannelEnum.Dashboard;

        /// <summary>
        /// Email, for email feedback.
        /// </summary>
        public string? EmailMessageId { get; set; } = null;
    }
}
