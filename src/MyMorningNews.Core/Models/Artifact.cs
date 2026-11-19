namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A stored file: briefing Markdown or HTML, audio, chapters, or an export. Kept up to the retention window unless pinned.
    /// </summary>
    public class Artifact
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix art_).
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
        /// Owner.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(UserId));
                _UserId = value;
            }
        }

        /// <summary>
        /// What the file is.
        /// </summary>
        public ArtifactKindEnum Kind { get; set; } = ArtifactKindEnum.BriefingMarkdown;

        /// <summary>
        /// Topic, when the artifact belongs to one. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Briefing, when the artifact belongs to one. May be null.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// One-time request, when the artifact belongs to one. May be null.
        /// </summary>
        public string? ResearchRequestId { get; set; } = null;

        /// <summary>
        /// Suggested download file name.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string FileName
        {
            get
            {
                return _FileName;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(FileName));
                _FileName = value;
            }
        }

        /// <summary>
        /// Media type.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string ContentType
        {
            get
            {
                return _ContentType;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ContentType));
                _ContentType = value;
            }
        }

        /// <summary>
        /// Size in bytes.
        /// </summary>
        public long Bytes { get; set; } = 0;

        /// <summary>
        /// SHA-256 of the content, lowercase hex.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Sha256
        {
            get
            {
                return _Sha256;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Sha256));
                _Sha256 = value;
            }
        }

        /// <summary>
        /// Store holding the content.
        /// </summary>
        public StorageProviderEnum StorageProvider { get; set; } = StorageProviderEnum.Local;

        /// <summary>
        /// Key in the store.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string StorageKey
        {
            get
            {
                return _StorageKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(StorageKey));
                _StorageKey = value;
            }
        }

        /// <summary>
        /// When retention deletes the content (ignored while pinned). May be null.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Kept past the retention window.
        /// </summary>
        public bool Pinned { get; set; } = false;

        /// <summary>
        /// When the content was deleted. May be null.
        /// </summary>
        public DateTime? DeletedUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateArtifactId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _FileName = String.Empty;
        private string _ContentType = String.Empty;
        private string _Sha256 = String.Empty;
        private string _StorageKey = String.Empty;

        #endregion
    }
}
