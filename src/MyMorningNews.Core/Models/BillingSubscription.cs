namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Verified provider subscription bound to one platform account.
    /// </summary>
    public class BillingSubscription
    {
        #region Public-Members

        /// <summary>Encrypted provider credential used only for server reconciliation; null for Stripe and Apple.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string? VerificationSecretEncrypted { get; set; } = null;


        /// <summary>
        /// Identifier (prefix bsu_).
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
        /// Stripe, Apple, or Google.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Provider
        {
            get
            {
                return _Provider;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Provider));
                _Provider = value;
            }
        }

        /// <summary>
        /// Live or Sandbox.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Environment
        {
            get
            {
                return _Environment;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Environment));
                _Environment = value;
            }
        }

        /// <summary>
        /// Stable provider subscription or transaction-lineage identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string ProviderId
        {
            get
            {
                return _ProviderId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ProviderId));
                _ProviderId = value;
            }
        }

        /// <summary>
        /// Provider customer identifier where available. May be null.
        /// </summary>
        public string? CustomerId { get; set; } = null;

        /// <summary>
        /// Entitlement package.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string PlanId
        {
            get
            {
                return _PlanId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PlanId));
                _PlanId = value;
            }
        }

        /// <summary>
        /// Normalized verified provider status.
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
        /// Verified end of entitled access. May be null.
        /// </summary>
        public DateTime? EndsUtc { get; set; } = null;

        /// <summary>
        /// Renewal is canceled but current period may remain entitled.
        /// </summary>
        public bool CancelAtPeriodEnd { get; set; } = false;

        /// <summary>
        /// Last authoritative reconciliation.
        /// </summary>
        public DateTime LastVerifiedUtc { get; set; } = DateTime.UtcNow;

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

        private string _Id = IdGenerator.GenerateBillingSubscriptionId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _Provider = String.Empty;
        private string _Environment = String.Empty;
        private string _ProviderId = String.Empty;
        private string _PlanId = String.Empty;
        private string _Status = "Pending";

        #endregion
    }
}
