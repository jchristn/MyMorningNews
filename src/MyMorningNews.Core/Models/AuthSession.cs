namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Revocable server-side session backing an opaque bearer token.
    /// </summary>
    public class AuthSession
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix ses_).
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
        /// User principal, when the session belongs to a user. May be null.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential principal, when the session belongs to a credential. May be null.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Principal type.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.User;

        /// <summary>
        /// Scheme used to create the session.
        /// </summary>
        public AuthSchemeEnum AuthScheme { get; set; } = AuthSchemeEnum.PasswordHeaders;

        /// <summary>
        /// Random token identifier embedded in the token.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TokenId
        {
            get
            {
                return _TokenId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TokenId));
                _TokenId = value;
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
        /// UTC time the session was last used. May be null.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        /// <summary>
        /// UTC time the session was revoked. May be null.
        /// </summary>
        public DateTime? RevokedUtc { get; set; } = null;

        /// <summary>
        /// Reason for revocation. May be null.
        /// </summary>
        public string? RevocationReason { get; set; } = null;

        /// <summary>
        /// Protected records cannot be deleted through the API.
        /// </summary>
        public bool IsProtected { get; set; } = false;

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

        private string _Id = IdGenerator.GenerateAuthSessionId();
        private string _TenantId = String.Empty;
        private string _TokenId = String.Empty;

        #endregion
    }
}
