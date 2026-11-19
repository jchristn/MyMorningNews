namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A fetched document, shared across runs in a tenant and reused while its content is unchanged.
    /// </summary>
    public class SourceItem
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix src_).
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
        /// Canonical URL.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string CanonicalUrl
        {
            get
            {
                return _CanonicalUrl;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(CanonicalUrl));
                _CanonicalUrl = value;
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
        /// Domain. May be null.
        /// </summary>
        public string? Domain { get; set; } = null;

        /// <summary>
        /// Title. May be null.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// Published time when known. May be null.
        /// </summary>
        public DateTime? PublishedUtc { get; set; } = null;

        /// <summary>
        /// Last fetch time.
        /// </summary>
        public DateTime FetchedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// HTTP status. May be null.
        /// </summary>
        public int? HttpStatus { get; set; } = null;

        /// <summary>
        /// Content type. May be null.
        /// </summary>
        public string? ContentType { get; set; } = null;

        /// <summary>
        /// SHA-256 of the content. May be null.
        /// </summary>
        public string? ContentHash { get; set; } = null;

        /// <summary>
        /// Extracted text (Markdown). May be null.
        /// </summary>
        public string? ContentText { get; set; } = null;

        /// <summary>
        /// Bytes downloaded.
        /// </summary>
        public long ContentBytes { get; set; } = 0;

        /// <summary>
        /// Run that first fetched it. May be null.
        /// </summary>
        public string? FirstSeenRunId { get; set; } = null;

        /// <summary>
        /// Runs that used it.
        /// </summary>
        public int TimesSeen { get; set; } = 1;

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

        private string _Id = IdGenerator.GenerateSourceItemId();
        private string _TenantId = String.Empty;
        private string _CanonicalUrl = String.Empty;
        private string _CanonicalUrlHash = String.Empty;

        #endregion
    }
}
