namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Explicit administrator publication of a canonical system topic. Reads use an allowlisted projection.
    /// </summary>
    public class CatalogPublication
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix cat_).
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
        /// Canonical topic; publishing does not transfer subscriber ownership.
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
        /// Protected workspace of the canonical topic.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string OwnerTenantId
        {
            get
            {
                return _OwnerTenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(OwnerTenantId));
                _OwnerTenantId = value;
            }
        }

        /// <summary>
        /// Public catalog title.
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
                _Title = value;
            }
        }

        /// <summary>
        /// Public scope description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Catalog category.
        /// </summary>
        public string Category
        {
            get
            {
                return _Category;
            }
            set
            {
                _Category = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Output language.
        /// </summary>
        public string Language
        {
            get
            {
                return _Language;
            }
            set
            {
                _Language = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Draft, Published, Retired, or Retracted.
        /// </summary>
        public string Status
        {
            get
            {
                return _Status;
            }
            set
            {
                _Status = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Whether free catalog members may subscribe.
        /// </summary>
        public bool FreeEligible { get; set; } = false;

        /// <summary>
        /// Administrator display ordering.
        /// </summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Publication revision for optimistic concurrency.
        /// </summary>
        public long Revision { get; set; } = 1;

        /// <summary>
        /// First publication timestamp. May be null.
        /// </summary>
        public DateTime? PublishedUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateCatalogPublicationId();
        private string _TopicId = String.Empty;
        private string _OwnerTenantId = String.Empty;
        private string _Title = String.Empty;
        private string _Category = "General";
        private string _Language = "en";
        private string _Status = "Draft";

        #endregion
    }
}
