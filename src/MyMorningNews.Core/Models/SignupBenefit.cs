namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Immutable signup offer captured before checkout; never grants paid access before verification.
    /// </summary>
    public class SignupBenefit
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix sbe_).
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
        /// Source signup code.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string PromoCodeId
        {
            get
            {
                return _PromoCodeId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PromoCodeId));
                _PromoCodeId = value;
            }
        }

        /// <summary>
        /// Custom or standard package selected by the offer.
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
        /// Percentage discount captured at signup, from zero through 100.
        /// </summary>
        public decimal DiscountPercent { get; set; } = 0;

        /// <summary>
        /// Discount billing months; null means ongoing. May be null.
        /// </summary>
        public int? DiscountMonths { get; set; } = null;

        /// <summary>
        /// When the offer was applied to a verified subscription. May be null.
        /// </summary>
        public DateTime? RedeemedUtc { get; set; } = null;

        /// <summary>
        /// Explicit administrator revocation. May be null.
        /// </summary>
        public DateTime? RevokedUtc { get; set; } = null;

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

        /// <summary>Required base subscription package.</summary>
        public string BillingPlanKey { get; set; } = "paid-5";

        /// <summary>Custom tier duration from verified activation; null means subscription lifetime.</summary>
        public int? DurationDays { get; set; } = null;

        /// <summary>Immutable verified subscription binding; null until payment is verified.</summary>
        public string? RedeemedSubscriptionId { get; set; } = null;

        /// <summary>Captured native Apple offer identifier.</summary>
        public string? AppleOfferId { get; set; } = null;

        /// <summary>Captured native Google offer identifier.</summary>
        public string? GoogleOfferId { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateSignupBenefitId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _PromoCodeId = String.Empty;
        private string _PlanId = String.Empty;

        #endregion
    }
}
