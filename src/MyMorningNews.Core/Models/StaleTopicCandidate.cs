namespace MyMorningNews.Core.Models
{
    using System;

    /// <summary>
    /// A recurring topic that qualifies for a keep-or-delete review.
    /// </summary>
    public class StaleTopicCandidate
    {
        /// <summary>
        /// Topic identifier.
        /// </summary>
        public string TopicId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Topic owner.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Completion time of the last run with new findings, or null when there was none.
        /// </summary>
        public DateTime? LastNewFindingUtc { get; set; } = null;
    }
}
