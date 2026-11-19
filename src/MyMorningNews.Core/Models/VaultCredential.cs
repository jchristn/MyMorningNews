namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Encrypted secret stored once per tenant and referenced by model endpoints, search providers, voices, MCP servers, and email settings.
    /// </summary>
    public class VaultCredential
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix vlt_).
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
        /// Display name.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Secret kind.
        /// </summary>
        public VaultCredentialKindEnum Kind { get; set; } = VaultCredentialKindEnum.ApiKey;

        /// <summary>
        /// Service the secret belongs to.
        /// </summary>
        public VaultProviderEnum Provider { get; set; } = VaultProviderEnum.Custom;

        /// <summary>
        /// Header name for CustomHeader secrets. May be null.
        /// </summary>
        public string? HeaderName { get; set; } = null;

        /// <summary>
        /// Username for BasicAuth secrets. May be null.
        /// </summary>
        public string? Username { get; set; } = null;

        /// <summary>
        /// Encrypted secret value. Never returned except through the audited reveal action.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string SecretEncrypted
        {
            get
            {
                return _SecretEncrypted;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SecretEncrypted));
                _SecretEncrypted = value;
            }
        }

        /// <summary>
        /// Last four characters of the secret.
        /// </summary>
        public string SecretLast4
        {
            get
            {
                return _SecretLast4;
            }
            set
            {
                _SecretLast4 = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Description. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// User who created the credential. May be null.
        /// </summary>
        public string? CreatedByUserId { get; set; } = null;

        /// <summary>
        /// UTC time the secret was last used by the server. May be null.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        /// <summary>
        /// UTC time the secret was last revealed. May be null.
        /// </summary>
        public DateTime? LastRevealedUtc { get; set; } = null;

        /// <summary>
        /// Optional expiration. May be null.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateVaultCredentialId();
        private string _TenantId = String.Empty;
        private string _Name = String.Empty;
        private string _SecretEncrypted = String.Empty;
        private string _SecretLast4 = String.Empty;

        #endregion
    }
}
