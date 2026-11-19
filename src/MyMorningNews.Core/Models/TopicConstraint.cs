namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A rule or source on a topic: include or exclude terms and entities, domains, tracked questions, feeds, seed URLs.
    /// </summary>
    public class TopicConstraint
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix tcn_).
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
        /// Topic.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TopicId
        {
            get
            {
                return _TopicId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TopicId));
                _TopicId = value;
            }
        }

        /// <summary>
        /// Dimension it belongs to, when any. May be null.
        /// </summary>
        public string? DimensionId { get; set; } = null;

        /// <summary>
        /// Kind.
        /// </summary>
        public ConstraintKindEnum Kind { get; set; } = ConstraintKindEnum.IncludeKeyword;

        /// <summary>
        /// Value (term, entity, domain, question, or URL).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Value
        {
            get
            {
                return _Value;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Value));
                _Value = value;
            }
        }

        /// <summary>
        /// Note or reason. May be null.
        /// </summary>
        public string? Note { get; set; } = null;

        /// <summary>
        /// Locked by the reader; regeneration keeps it.
        /// </summary>
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// Who created it.
        /// </summary>
        public OriginEnum Origin { get; set; } = OriginEnum.Model;

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

        private string _Id = IdGenerator.GenerateTopicConstraintId();
        private string _TenantId = String.Empty;
        private string _TopicId = String.Empty;
        private string _Value = String.Empty;

        #endregion
    }
}
