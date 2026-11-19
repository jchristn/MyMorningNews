namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A search query planned for a run and its outcome per provider.
    /// </summary>
    public class SearchQuery
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix qry_).
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
        /// Run.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string RunId
        {
            get
            {
                return _RunId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(RunId));
                _RunId = value;
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
        /// Dimension it serves. May be null.
        /// </summary>
        public string? DimensionId { get; set; } = null;

        /// <summary>
        /// Query.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string QueryText
        {
            get
            {
                return _QueryText;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(QueryText));
                _QueryText = value;
            }
        }

        /// <summary>
        /// Why. May be null.
        /// </summary>
        public string? Rationale { get; set; } = null;

        /// <summary>
        /// Provider searched. May be null.
        /// </summary>
        public string? SearchProviderId { get; set; } = null;

        /// <summary>
        /// Provider kind. May be null.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Results returned.
        /// </summary>
        public int ResultCount { get; set; } = 0;

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Whether the provider answered.
        /// </summary>
        public bool Succeeded { get; set; } = true;

        /// <summary>
        /// Error. May be null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateSearchQueryId();
        private string _TenantId = String.Empty;
        private string _RunId = String.Empty;
        private string _TopicId = String.Empty;
        private string _QueryText = String.Empty;

        #endregion
    }
}
