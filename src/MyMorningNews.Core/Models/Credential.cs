namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Non-interactive API credential (access key and secret) owned by a user.
    /// </summary>
    public class Credential
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix crd_).
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
        /// Owning user identifier.
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
        /// Display name, for example "CI pipeline".
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
        /// Public access key (access_ plus 32 random characters).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string AccessKey
        {
            get
            {
                return _AccessKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(AccessKey));
                _AccessKey = value;
            }
        }

        /// <summary>
        /// Encrypted secret key. Never returned by the API.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string SecretKeyEncrypted
        {
            get
            {
                return _SecretKeyEncrypted;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SecretKeyEncrypted));
                _SecretKeyEncrypted = value;
            }
        }

        /// <summary>
        /// Last four characters of the secret, for display.
        /// </summary>
        public string SecretKeyLast4
        {
            get
            {
                return _SecretKeyLast4;
            }
            set
            {
                _SecretKeyLast4 = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Authentication mode.
        /// </summary>
        public CredentialAuthModeEnum AuthMode { get; set; } = CredentialAuthModeEnum.DirectHeader;

        /// <summary>
        /// Legacy coarse scopes such as read or write. Empty means no coarse restriction.
        /// </summary>
        public List<string> Scopes
        {
            get
            {
                return _Scopes;
            }
            set
            {
                _Scopes = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Optional resource allow-list. Empty means all resources in the tenant.
        /// </summary>
        public List<string> ResourceIds
        {
            get
            {
                return _ResourceIds;
            }
            set
            {
                _ResourceIds = value ?? new List<string>();
            }
        }

        /// <summary>
        /// UTC time the credential was last used. May be null.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        /// <summary>
        /// UTC expiration time. May be null.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

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

        private string _Id = IdGenerator.GenerateCredentialId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _Name = String.Empty;
        private string _AccessKey = String.Empty;
        private string _SecretKeyEncrypted = String.Empty;
        private string _SecretKeyLast4 = String.Empty;
        private List<string> _Scopes = new List<string>();
        private List<string> _ResourceIds = new List<string>();

        #endregion
    }
}
