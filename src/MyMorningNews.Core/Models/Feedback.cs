namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A reader rating of an item with an optional reason. The newest rating per user and item is current.
    /// </summary>
    public class Feedback
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix fbk_).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// User.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(UserId));
                _UserId = value;
            }
        }

        /// <summary>
        /// Item type.
        /// </summary>
        public FeedbackTargetTypeEnum TargetType { get; set; } = FeedbackTargetTypeEnum.Finding;

        /// <summary>
        /// Item.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TargetId
        {
            get
            {
                return _TargetId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TargetId));
                _TargetId = value;
            }
        }

        /// <summary>
        /// Topic. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Relevance rating. May be null.
        /// </summary>
        public RelevanceRatingEnum? Relevance { get; set; } = null;

        /// <summary>
        /// Accuracy rating (predictions). May be null.
        /// </summary>
        public AccuracyRatingEnum? Accuracy { get; set; } = null;

        /// <summary>
        /// Usefulness rating (suggestions). May be null.
        /// </summary>
        public UsefulnessRatingEnum? Usefulness { get; set; } = null;

        /// <summary>
        /// Why, in the reader words (up to 2000 characters). May be null.
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Where it came from.
        /// </summary>
        public FeedbackChannelEnum Channel { get; set; } = FeedbackChannelEnum.Dashboard;

        /// <summary>
        /// Email, for email feedback. May be null.
        /// </summary>
        public string? EmailMessageId { get; set; } = null;

        /// <summary>
        /// Earlier rating this one replaces. May be null.
        /// </summary>
        public string? SupersedesFeedbackId { get; set; } = null;

        /// <summary>
        /// The newest rating for this user and item.
        /// </summary>
        public bool IsCurrent { get; set; } = true;

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

        private string _Id = IdGenerator.GenerateFeedbackId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _TargetId = String.Empty;

        #endregion
    }
}
