namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Links a finding to a piece of evidence.
    /// </summary>
    public class FindingEvidence
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix fev_).
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
        /// Finding.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string FindingId
        {
            get
            {
                return _FindingId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(FindingId));
                _FindingId = value;
            }
        }

        /// <summary>
        /// Evidence.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string EvidenceId
        {
            get
            {
                return _EvidenceId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(EvidenceId));
                _EvidenceId = value;
            }
        }

        /// <summary>
        /// Role.
        /// </summary>
        public EvidenceRoleEnum Role { get; set; } = EvidenceRoleEnum.Supports;

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

        private string _Id = IdGenerator.GenerateFindingEvidenceId();
        private string _TenantId = String.Empty;
        private string _FindingId = String.Empty;
        private string _EvidenceId = String.Empty;

        #endregion
    }
}
