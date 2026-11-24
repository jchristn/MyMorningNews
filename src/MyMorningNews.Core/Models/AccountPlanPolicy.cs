namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Account signup policy, snapshotted so future server defaults do not change existing accounts.
    /// </summary>
    public class AccountPlanPolicy
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix app_).
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
        /// Whether paid access requires a verified subscription. False permits the assigned non-billed tier.
        /// </summary>
        public bool SubscriptionRequired { get; set; } = true;

        /// <summary>
        /// Fallback plan selected when the account was created.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string DefaultPlanId
        {
            get
            {
                return _DefaultPlanId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(DefaultPlanId));
                _DefaultPlanId = value;
            }
        }

        /// <summary>
        /// Platform, Personal, or SelfHosted. Selected explicitly, never inferred from a saved key.
        /// </summary>
        public string FundingMode
        {
            get
            {
                return _FundingMode;
            }
            set
            {
                _FundingMode = value ?? String.Empty;
            }
        }

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

        private string _Id = IdGenerator.GenerateAccountPlanPolicyId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _DefaultPlanId = String.Empty;
        private string _FundingMode = "Platform";

        #endregion
    }
}
