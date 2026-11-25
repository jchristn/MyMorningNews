namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Filter for topic reviews. TenantId is required.
    /// </summary>
    public class TopicReviewFilter : EnumerationQuery
    {
        /// <summary>
        /// Owner, or null for any.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Topic, or null for any.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Status, or null for any.
        /// </summary>
        public TopicReviewStatusEnum? Status { get; set; } = null;
    }
}
