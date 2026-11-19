namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A reusable theme tagged onto a feedback reason (for example "too basic").
    /// </summary>
    public class FeedbackReasonTag
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix frt_).
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
        /// Feedback.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string FeedbackId
        {
            get
            {
                return _FeedbackId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(FeedbackId));
                _FeedbackId = value;
            }
        }

        /// <summary>
        /// Topic. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Tag (lowercase, 2 to 4 words).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Tag
        {
            get
            {
                return _Tag;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Tag));
                _Tag = value;
            }
        }

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

        private string _Id = IdGenerator.GenerateFeedbackReasonTagId();
        private string _TenantId = String.Empty;
        private string _FeedbackId = String.Empty;
        private string _Tag = String.Empty;

        #endregion
    }
}
