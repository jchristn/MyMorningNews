namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A preference the research agent follows: written by the reader or distilled from feedback and approved.
    /// </summary>
    public class LearnedPreference
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix lrn_).
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
        /// Owner.
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
        /// Topic, or null for all of the owner topics. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Kind.
        /// </summary>
        public PreferenceKindEnum Kind { get; set; } = PreferenceKindEnum.Prefer;

        /// <summary>
        /// The preference, as an instruction.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Statement
        {
            get
            {
                return _Statement;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Statement));
                _Statement = value;
            }
        }

        /// <summary>
        /// Status.
        /// </summary>
        public PreferenceStatusEnum Status { get; set; } = PreferenceStatusEnum.Proposed;

        /// <summary>
        /// Distilled from feedback or written by the reader.
        /// </summary>
        public PreferenceOriginEnum Origin { get; set; } = PreferenceOriginEnum.User;

        /// <summary>
        /// Feedback items that support it.
        /// </summary>
        public int SupportCount { get; set; } = 0;

        /// <summary>
        /// Confidence (0 to 1). Minimum 0.0, maximum 1.0; values outside the range are clamped.
        /// </summary>
        public double Confidence
        {
            get
            {
                return _Confidence;
            }
            set
            {
                _Confidence = Math.Clamp(value, 0.0, 1.0);
            }
        }

        /// <summary>
        /// Newest supporting feedback. May be null.
        /// </summary>
        public DateTime? LastEvidenceUtc { get; set; } = null;

        /// <summary>
        /// When it was accepted, rejected, or disabled. May be null.
        /// </summary>
        public DateTime? DecidedUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateLearnedPreferenceId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _Statement = String.Empty;
        private double _Confidence = 0.5;

        #endregion
    }
}
