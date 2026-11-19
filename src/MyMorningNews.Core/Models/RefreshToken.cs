namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Rotating refresh token for mobile clients. Only a SHA-256 hash of the secret is stored. Tokens issued from one sign-in share a family; reusing a rotated token revokes the family.
    /// </summary>
    public class RefreshToken
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix rft_).
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
        /// User.
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
        /// Session issued alongside this token. May be null.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Family shared by every rotation of one sign-in.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string FamilyId
        {
            get
            {
                return _FamilyId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(FamilyId));
                _FamilyId = value;
            }
        }

        /// <summary>
        /// SHA-256 hash of the token secret (hex).
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
        /// Client kind, for example mobile.
        /// </summary>
        public string Client
        {
            get
            {
                return _Client;
            }
            set
            {
                _Client = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Source IP at issuance. May be null.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// User agent at issuance. May be null.
        /// </summary>
        public string? UserAgent { get; set; } = null;

        /// <summary>
        /// UTC expiration time.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time the token was rotated (single use). May be null.
        /// </summary>
        public DateTime? UsedUtc { get; set; } = null;

        /// <summary>
        /// UTC time the token was revoked. May be null.
        /// </summary>
        public DateTime? RevokedUtc { get; set; } = null;

        /// <summary>
        /// Reason for revocation. May be null.
        /// </summary>
        public string? RevocationReason { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateRefreshTokenId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _FamilyId = String.Empty;
        private string _TokenHash = String.Empty;
        private string _Client = "mobile";

        #endregion
    }
}
