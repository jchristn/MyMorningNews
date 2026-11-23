namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A public share link for one briefing. One record yields two links: a reading page and a listening page.
    /// Only the SHA-256 hash of the link secret is stored. Every share expires.
    /// </summary>
    public class BriefingShare
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix shr_).
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
        /// The user who created the share.
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
        /// The shared briefing.
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
        /// SHA-256 of the link secret, lowercase hex.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TokenHash
        {
            get
            {
                return _TokenHash;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TokenHash));
                _TokenHash = value;
            }
        }

        /// <summary>
        /// First characters of the link secret, for recognition.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TokenPrefix
        {
            get
            {
                return _TokenPrefix;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TokenPrefix));
                _TokenPrefix = value;
            }
        }

        /// <summary>
        /// Show the sharing user's display name on the public pages. Default false. The email address is never shown.
        /// </summary>
        public bool ShowOwnerName { get; set; } = false;

        /// <summary>
        /// When the links stop working. Always set. Default seven days after creation.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow.AddDays(7);

        /// <summary>
        /// When the share was revoked. May be null.
        /// </summary>
        public DateTime? RevokedUtc { get; set; } = null;

        /// <summary>
        /// Times a public page of this share was opened. Minimum 0.
        /// </summary>
        public long ViewCount
        {
            get
            {
                return _ViewCount;
            }
            set
            {
                _ViewCount = Math.Max(0, value);
            }
        }

        /// <summary>
        /// When a public page of this share was last opened. May be null.
        /// </summary>
        public DateTime? LastViewedUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateBriefingShareId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _BriefingId = String.Empty;
        private string _TokenHash = String.Empty;
        private string _TokenPrefix = String.Empty;
        private long _ViewCount = 0;

        #endregion
    }
}
