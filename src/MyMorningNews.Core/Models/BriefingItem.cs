namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// An item a briefing section covers, for feedback and analytics.
    /// </summary>
    public class BriefingItem
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix bit_).
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
        /// Section.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string SectionId
        {
            get
            {
                return _SectionId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SectionId));
                _SectionId = value;
            }
        }

        /// <summary>
        /// Item kind.
        /// </summary>
        public BriefingItemTypeEnum ItemType { get; set; } = BriefingItemTypeEnum.Finding;

        /// <summary>
        /// Item.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string ItemId
        {
            get
            {
                return _ItemId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ItemId));
                _ItemId = value;
            }
        }

        /// <summary>
        /// Reference number of the item source, when cited. May be null.
        /// </summary>
        public int? CitationNumber { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateBriefingItemId();
        private string _TenantId = String.Empty;
        private string _BriefingId = String.Empty;
        private string _SectionId = String.Empty;
        private string _ItemId = String.Empty;

        #endregion
    }
}
