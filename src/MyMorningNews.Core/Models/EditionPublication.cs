namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A catalog publication an edition includes. The edition reads the publication's already published issues; it never
    /// starts system research. The owner must hold an active subscription when the edition is composed.
    /// </summary>
    public class EditionPublication
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix edp_).
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
        /// Tenant of the edition.
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
        /// Edition.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string EditionId
        {
            get
            {
                return _EditionId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(EditionId));
                _EditionId = value;
            }
        }

        /// <summary>
        /// Catalog publication (prefix cat_).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string PublicationId
        {
            get
            {
                return _PublicationId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PublicationId));
                _PublicationId = value;
            }
        }

        /// <summary>
        /// Order within the edition, starting at 0.
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

        private string _Id = IdGenerator.GenerateEditionPublicationId();
        private string _TenantId = String.Empty;
        private string _EditionId = String.Empty;
        private string _PublicationId = String.Empty;

        #endregion
    }
}
