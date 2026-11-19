namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A search result considered by a run, with what happened to it.
    /// </summary>
    public class SearchResult
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix srh_).
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
        /// Query.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string SearchQueryId
        {
            get
            {
                return _SearchQueryId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SearchQueryId));
                _SearchQueryId = value;
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
        /// URL.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Url
        {
            get
            {
                return _Url;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Url));
                _Url = value;
            }
        }

        /// <summary>
        /// Canonical URL hash.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string CanonicalUrlHash
        {
            get
            {
                return _CanonicalUrlHash;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(CanonicalUrlHash));
                _CanonicalUrlHash = value;
            }
        }

        /// <summary>
        /// Title. May be null.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// Snippet. May be null.
        /// </summary>
        public string? Snippet { get; set; } = null;

        /// <summary>
        /// Published time when known. May be null.
        /// </summary>
        public DateTime? PublishedUtc { get; set; } = null;

        /// <summary>
        /// Provider rank.
        /// </summary>
        public int Rank { get; set; } = 0;

        /// <summary>
        /// Provider kind. May be null.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Disposition.
        /// </summary>
        public SearchResultDispositionEnum Disposition { get; set; } = SearchResultDispositionEnum.Pending;

        /// <summary>
        /// Fetched source. May be null.
        /// </summary>
        public string? SourceItemId { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateSearchResultId();
        private string _TenantId = String.Empty;
        private string _SearchQueryId = String.Empty;
        private string _RunId = String.Empty;
        private string _TopicId = String.Empty;
        private string _Url = String.Empty;
        private string _CanonicalUrlHash = String.Empty;

        #endregion
    }
}
