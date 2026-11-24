namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Create codes in bulk, each with a random suffix (single use unless maxRedemptions says otherwise).
    /// </summary>
    public class PromoCodeGenerateRequest
    {
        /// <summary>
        /// Prefix (up to 16 letters and digits).
        /// </summary>
        public string? Prefix { get; set; } = null;

        /// <summary>
        /// Codes to create (1 to 1000).
        /// </summary>
        public int Count { get; set; } = 10;

        /// <summary>
        /// What the code is for.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Plan it gives (default: the complimentary plan).
        /// </summary>
        public string? PlanId { get; set; } = null;

        /// <summary>
        /// Days the plan lasts (1 to 3650); 0 means no end.
        /// </summary>
        public int? DurationDays { get; set; } = null;

        /// <summary>
        /// Most redemptions in total; 0 means unlimited.
        /// </summary>
        public int? MaxRedemptions { get; set; } = null;

        /// <summary>
        /// Redemptions per user (1 to 100).
        /// </summary>
        public int? PerUserLimit { get; set; } = null;

        /// <summary>
        /// Usable from.
        /// </summary>
        public DateTime? StartsUtc { get; set; } = null;

        /// <summary>
        /// Usable until.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Can be redeemed.
        /// </summary>
        public bool? Enabled { get; set; } = null;

        /// <summary>
        /// Only at sign-up.
        /// </summary>
        public bool? NewAccountsOnly { get; set; } = null;
        /// <summary>Complimentary grants access; PaidOffer captures a subscription offer without granting access. Null leaves the existing value unchanged.</summary>
        public string? BenefitKind { get; set; } = null;

        /// <summary>Purchased package required for this paid offer, normally paid-5 or paid-20. Null leaves the existing value unchanged.</summary>
        public string? BillingPlanKey { get; set; } = null;

        /// <summary>Percentage discount from zero through 100. Null leaves the existing value unchanged.</summary>
        public decimal? DiscountPercent { get; set; } = null;

        /// <summary>Discount months; null means ongoing. Null leaves the existing value unchanged.</summary>
        public int? DiscountMonths { get; set; } = null;

        /// <summary>Configured Apple promotional offer identifier; null when unavailable. Null leaves the existing value unchanged.</summary>
        public string? AppleOfferId { get; set; } = null;

        /// <summary>Configured Google subscription offer identifier; null when unavailable. Null leaves the existing value unchanged.</summary>
        public string? GoogleOfferId { get; set; } = null;

    }
}
