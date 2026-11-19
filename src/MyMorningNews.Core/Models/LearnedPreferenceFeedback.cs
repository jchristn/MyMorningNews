namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A feedback item that supports a learned preference.
    /// </summary>
    public class LearnedPreferenceFeedback
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix lpf_).
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
        /// Preference.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string LearnedPreferenceId
        {
            get
            {
                return _LearnedPreferenceId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(LearnedPreferenceId));
                _LearnedPreferenceId = value;
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

        private string _Id = IdGenerator.GenerateLearnedPreferenceFeedbackId();
        private string _TenantId = String.Empty;
        private string _LearnedPreferenceId = String.Empty;
        private string _FeedbackId = String.Empty;

        #endregion
    }
}
