namespace MyMorningNews.Core.Responses
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// A keep-or-delete review with the topic's name, for the in-app prompt.
    /// </summary>
    public class TopicReviewResponse
    {
        /// <summary>
        /// Review identifier (prefix trv_).
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Topic identifier.
        /// </summary>
        public string TopicId { get; set; } = String.Empty;

        /// <summary>
        /// Topic name, or null when the topic is gone.
        /// </summary>
        public string? TopicName { get; set; } = null;

        /// <summary>
        /// Status.
        /// </summary>
        public TopicReviewStatusEnum Status { get; set; } = TopicReviewStatusEnum.Pending;

        /// <summary>
        /// Completion time of the last run with new findings, or null when there was none.
        /// </summary>
        public DateTime? LastNewFindingUtc { get; set; } = null;

        /// <summary>
        /// Days without new findings that triggered the review.
        /// </summary>
        public int StaleDays { get; set; } = 7;

        /// <summary>
        /// When the owner was asked.
        /// </summary>
        public DateTime PromptedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When a pending review deletes the topic.
        /// </summary>
        public DateTime DeleteAfterUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the review was answered or closed, or null.
        /// </summary>
        public DateTime? ResolvedUtc { get; set; } = null;

        /// <summary>
        /// Build from a review.
        /// </summary>
        /// <param name="review">Review.</param>
        /// <param name="topicName">Topic name, or null.</param>
        /// <param name="staleDays">Configured stale days.</param>
        /// <returns>Response.</returns>
        /// <exception cref="ArgumentNullException">review is null.</exception>
        public static TopicReviewResponse From(TopicReview review, string? topicName, int staleDays)
        {
            ArgumentNullException.ThrowIfNull(review);
            return new TopicReviewResponse
            {
                Id = review.Id,
                TopicId = review.TopicId,
                TopicName = topicName,
                Status = review.Status,
                LastNewFindingUtc = review.LastNewFindingUtc,
                StaleDays = staleDays,
                PromptedUtc = review.PromptedUtc,
                DeleteAfterUtc = review.DeleteAfterUtc,
                ResolvedUtc = review.ResolvedUtc
            };
        }
    }
}
