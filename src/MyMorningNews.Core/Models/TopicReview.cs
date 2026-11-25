namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A keep-or-delete review of a recurring topic that produced no new findings for a while. A pending review
    /// whose deadline passes deletes the topic; the review stays as the record of that deletion.
    /// </summary>
    public class TopicReview
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix trv_).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Id
        {
            get { return _Id; }
            set { _Id = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(Id)) : value; }
        }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TenantId
        {
            get { return _TenantId; }
            set { _TenantId = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(TenantId)) : value; }
        }

        /// <summary>
        /// Topic owner.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string UserId
        {
            get { return _UserId; }
            set { _UserId = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(UserId)) : value; }
        }

        /// <summary>
        /// Topic under review.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TopicId
        {
            get { return _TopicId; }
            set { _TopicId = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(TopicId)) : value; }
        }

        /// <summary>
        /// Status. Default Pending.
        /// </summary>
        public TopicReviewStatusEnum Status { get; set; } = TopicReviewStatusEnum.Pending;

        /// <summary>
        /// Completion time of the last run with new findings, or null when the topic never had one.
        /// </summary>
        public DateTime? LastNewFindingUtc { get; set; } = null;

        /// <summary>
        /// UTC time the owner was asked.
        /// </summary>
        public DateTime PromptedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time after which a pending review deletes the topic.
        /// </summary>
        public DateTime DeleteAfterUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time the reminder before deletion was sent, or null.
        /// </summary>
        public DateTime? ReminderSentUtc { get; set; } = null;

        /// <summary>
        /// UTC time the review was answered or closed, or null while pending.
        /// </summary>
        public DateTime? ResolvedUtc { get; set; } = null;

        /// <summary>
        /// Whether the record is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time of the last update.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateTopicReviewId();
        private string _TenantId = "default";
        private string _UserId = "none";
        private string _TopicId = "none";

        #endregion
    }
}
