namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// One section of a composed briefing.
    /// </summary>
    public class BriefingSection
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix bsc_).
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
        /// Briefing.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string BriefingId
        {
            get
            {
                return _BriefingId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(BriefingId));
                _BriefingId = value;
            }
        }

        /// <summary>
        /// Stable key from the heading.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string SectionKey
        {
            get
            {
                return _SectionKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SectionKey));
                _SectionKey = value;
            }
        }

        /// <summary>
        /// Heading.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Heading
        {
            get
            {
                return _Heading;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Heading));
                _Heading = value;
            }
        }

        /// <summary>
        /// Body Markdown. May be null.
        /// </summary>
        public string? Markdown { get; set; } = null;

        /// <summary>
        /// Directive source (findings, predictions, suggestions, citations, run-summaries, none, opening).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Source
        {
            get
            {
                return _Source;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Source));
                _Source = value;
            }
        }

        /// <summary>
        /// Order.
        /// </summary>
        public int SortOrder { get; set; } = 0;

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

        private string _Id = IdGenerator.GenerateBriefingSectionId();
        private string _TenantId = String.Empty;
        private string _BriefingId = String.Empty;
        private string _SectionKey = String.Empty;
        private string _Heading = String.Empty;
        private string _Source = String.Empty;

        #endregion
    }
}
