namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Give feedback on an item.
    /// </summary>
    public class FeedbackCreateRequest
    {
        /// <summary>
        /// Item type.
        /// </summary>
        public FeedbackTargetTypeEnum TargetType { get; set; } = FeedbackTargetTypeEnum.Finding;

        /// <summary>
        /// Item.
        /// </summary>
        public string? TargetId { get; set; } = null;

        /// <summary>
        /// Action, such as relevant or done; or give explicit ratings.
        /// </summary>
        public string? Action { get; set; } = null;

        /// <summary>
        /// Relevance.
        /// </summary>
        public RelevanceRatingEnum? Relevance { get; set; } = null;

        /// <summary>
        /// Accuracy.
        /// </summary>
        public AccuracyRatingEnum? Accuracy { get; set; } = null;

        /// <summary>
        /// Usefulness.
        /// </summary>
        public UsefulnessRatingEnum? Usefulness { get; set; } = null;

        /// <summary>
        /// Why (up to 2000 characters).
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Dashboard (default), Mobile, or Api.
        /// </summary>
        public FeedbackChannelEnum Channel { get; set; } = FeedbackChannelEnum.Dashboard;
    }
}
