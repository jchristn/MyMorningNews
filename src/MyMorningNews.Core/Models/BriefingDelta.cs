namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// One structured change between a briefing and its comparison baseline. Every asserted change names the current
    /// evidence (a finding or prediction of this briefing) and the baseline issue and item it compares with. Earlier
    /// briefing prose is never used as evidence.
    /// </summary>
    public class BriefingDelta
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix bdl_).
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
        /// Tenant of the briefing.
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
        /// Briefing the change belongs to.
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
        /// Kind of change.
        /// </summary>
        public BriefingDeltaKindEnum Kind { get; set; } = BriefingDeltaKindEnum.NewDevelopment;

        /// <summary>
        /// Short label: the current item's title, or the baseline item's title when nothing current exists. 1 to 512 characters.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Title
        {
            get
            {
                return _Title;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Title));
                _Title = value.Length > 512 ? value.Substring(0, 512) : value;
            }
        }

        /// <summary>
        /// One-line explanation, or null.
        /// </summary>
        public string? Summary { get; set; } = null;

        /// <summary>
        /// Current finding that is the evidence for this change, or null.
        /// </summary>
        public string? FindingId { get; set; } = null;

        /// <summary>
        /// Current prediction that is the evidence for a Resolved change, or null.
        /// </summary>
        public string? PredictionId { get; set; } = null;

        /// <summary>
        /// Baseline issue compared with, or null for a new development.
        /// </summary>
        public string? BaselineBriefingId { get; set; } = null;

        /// <summary>
        /// Baseline finding or prediction compared with, or null.
        /// </summary>
        public string? BaselineItemId { get; set; } = null;

        /// <summary>
        /// Display order, starting at 0.
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

        private string _Id = IdGenerator.GenerateBriefingDeltaId();
        private string _TenantId = String.Empty;
        private string _BriefingId = String.Empty;
        private string _Title = String.Empty;

        #endregion
    }
}
