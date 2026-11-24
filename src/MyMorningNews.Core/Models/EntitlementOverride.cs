namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Audited per-user override of one validated plan feature; deleting restores inheritance.
    /// </summary>
    public class EntitlementOverride
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix eov_).
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
        /// Registered entitlement key.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string FeatureKey
        {
            get
            {
                return _FeatureKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(FeatureKey));
                _FeatureKey = value;
            }
        }

        /// <summary>
        /// Validated feature value; unlimited is distinct from zero.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Value
        {
            get
            {
                return _Value;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Value));
                _Value = value;
            }
        }

        /// <summary>
        /// Administrator explanation for this override.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Reason
        {
            get
            {
                return _Reason;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Reason));
                _Reason = value;
            }
        }

        /// <summary>
        /// Administrator author. May be null.
        /// </summary>
        public string? GrantedByUserId { get; set; } = null;

        /// <summary>
        /// UTC expiration, or null for no expiration. May be null.
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

        private string _Id = IdGenerator.GenerateEntitlementOverrideId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _FeatureKey = String.Empty;
        private string _Value = String.Empty;
        private string _Reason = String.Empty;

        #endregion
    }
}
