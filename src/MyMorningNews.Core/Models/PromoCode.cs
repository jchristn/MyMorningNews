namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A promotional code that gives a plan (a free subscription) for a number of days, with usage limits.
    /// </summary>
    public class PromoCode
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix prc_).
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
        /// Code people type, uppercase letters, digits, and dashes (4 to 40).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Code
        {
            get
            {
                return _Code;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Code));
                _Code = value;
            }
        }

        /// <summary>
        /// What the code is for (campaign, partner). May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Plan the code gives.
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
        /// Days the plan lasts after redemption, or null for no end. May be null.
        /// </summary>
        public int? DurationDays { get; set; } = null;

        /// <summary>
        /// Most redemptions in total, or null for unlimited. May be null.
        /// </summary>
        public int? MaxRedemptions { get; set; } = null;

        /// <summary>
        /// Redemptions so far (revoked ones still count).
        /// </summary>
        public int RedemptionCount { get; set; } = 0;

        /// <summary>
        /// Redemptions allowed per user (1 to 100). Minimum 1, maximum 100; values outside the range are clamped.
        /// </summary>
        public int PerUserLimit
        {
            get
            {
                return _PerUserLimit;
            }
            set
            {
                _PerUserLimit = Math.Clamp(value, 1, 100);
            }
        }

        /// <summary>
        /// Usable from, or null for now. May be null.
        /// </summary>
        public DateTime? StartsUtc { get; set; } = null;

        /// <summary>
        /// Usable until, or null for no end. May be null.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Can be redeemed.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Only at sign-up.
        /// </summary>
        public bool NewAccountsOnly { get; set; } = false;

        /// <summary>
        /// Generated batch, for bulk codes. May be null.
        /// </summary>
        public string? BatchId { get; set; } = null;

        /// <summary>
        /// Administrator who created it. May be null.
        /// </summary>
        public string? CreatedByUserId { get; set; } = null;

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

        /// <summary>Complimentary grants access; PaidOffer captures a subscription offer without granting access.</summary>
        public string BenefitKind { get; set; } = "Complimentary";

        /// <summary>Purchased package required for this paid offer, normally paid-5 or paid-20.</summary>
        public string? BillingPlanKey { get; set; } = null;

        /// <summary>Percentage discount from zero through 100.</summary>
        public decimal DiscountPercent { get; set; } = 0;

        /// <summary>Discount months; null means ongoing.</summary>
        public int? DiscountMonths { get; set; } = null;

        /// <summary>Configured Apple promotional offer identifier; null when unavailable.</summary>
        public string? AppleOfferId { get; set; } = null;

        /// <summary>Configured Google subscription offer identifier; null when unavailable.</summary>
        public string? GoogleOfferId { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GeneratePromoCodeId();
        private string _Code = String.Empty;
        private string _PlanId = String.Empty;
        private int _PerUserLimit = 1;

        #endregion
    }
}
