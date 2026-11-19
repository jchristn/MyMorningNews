namespace MyMorningNews.Core.Models
{
    using System;

    /// <summary>
    /// A user and topic with enough new feedback for a learning pass.
    /// </summary>
    public class LearningCandidate
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
        /// Topic.
        /// </summary>
        public string TopicId { get; set; } = String.Empty;

        /// <summary>
        /// Feedback items since the checkpoint.
        /// </summary>
        public int NewFeedback { get; set; } = 0;

        /// <summary>
        /// Newest of them.
        /// </summary>
        public DateTime NewestUtc { get; set; } = DateTime.UtcNow;
    }
}
