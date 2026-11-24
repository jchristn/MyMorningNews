namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Durable checkout operation. Its identifier is the provider idempotency key across network retries.
    /// </summary>
    public class BillingCheckout
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix bco_).
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
        /// Allowlisted configured offer.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string OfferKey
        {
            get
            {
                return _OfferKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(OfferKey));
                _OfferKey = value;
            }
        }

        /// <summary>
        /// Provider session identifier. May be null.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Provider-hosted checkout URL. May be null.
        /// </summary>
        public string? CheckoutUrl { get; set; } = null;

        /// <summary>
        /// UTC expiration; no new concurrent operation before this time.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Pending, Completed, or Expired.
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

        /// <summary>Provider price captured when checkout starts. May be null for legacy operations.</summary>
        public string? PriceId { get; set; }

        /// <summary>Email captured when checkout starts. May be null for legacy operations.</summary>
        public string? CustomerEmail { get; set; }

        /// <summary>Allowlisted return URL captured when checkout starts. May be null for legacy operations.</summary>
        public string? ReturnUrl { get; set; }

        /// <summary>Immutable signup offer snapshot, if selected. May be null for legacy operations.</summary>
        public string? SignupBenefitId { get; set; }

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateBillingCheckoutId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _OfferKey = String.Empty;
        private string _Status = "Pending";

        #endregion
    }
}
