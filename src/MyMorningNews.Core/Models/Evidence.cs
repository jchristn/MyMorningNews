namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A verbatim quote from a source, verified against the stored text.
    /// </summary>
    public class Evidence
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix evd_).
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
        /// Source.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string SourceItemId
        {
            get
            {
                return _SourceItemId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SourceItemId));
                _SourceItemId = value;
            }
        }

        /// <summary>
        /// Run. May be null.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Quote as found in the source.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Quote
        {
            get
            {
                return _Quote;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Quote));
                _Quote = value;
            }
        }

        /// <summary>
        /// Start offset in the source text.
        /// </summary>
        public int StartOffset { get; set; } = -1;

        /// <summary>
        /// End offset in the source text.
        /// </summary>
        public int EndOffset { get; set; } = -1;

        /// <summary>
        /// Surrounding paragraph. May be null.
        /// </summary>
        public string? ContextText { get; set; } = null;

        /// <summary>
        /// Hash of the normalized quote.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string QuoteHash
        {
            get
            {
                return _QuoteHash;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(QuoteHash));
                _QuoteHash = value;
            }
        }

        /// <summary>
        /// Found verbatim in the source.
        /// </summary>
        public bool Verified { get; set; } = false;

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

        private string _Id = IdGenerator.GenerateEvidenceId();
        private string _TenantId = String.Empty;
        private string _SourceItemId = String.Empty;
        private string _Quote = String.Empty;
        private string _QuoteHash = String.Empty;

        #endregion
    }
}
