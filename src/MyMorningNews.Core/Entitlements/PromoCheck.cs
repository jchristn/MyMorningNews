namespace MyMorningNews.Core.Entitlements
{
    using System;

    /// <summary>
    /// Whether a code can be redeemed.
    /// </summary>
    public class PromoCheck
    {
        /// <summary>
        /// Can be redeemed.
        /// </summary>
        public bool Valid { get; set; } = false;

        /// <summary>
        /// Why not: NotFound, Disabled, NotStarted, Expired, Exhausted, AlreadyRedeemed, or NewAccountsOnly.
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Plan it gives.
        /// </summary>
        public string? PlanName { get; set; } = null;

        /// <summary>
        /// Days, or null for no end.
        /// </summary>
        public int? DurationDays { get; set; } = null;

        /// <summary>
        /// Message for people.
        /// </summary>
        public string Message { get; set; } = String.Empty;
        /// <summary>Complimentary or PaidOffer when valid. A paid offer alone grants no access.</summary>
        public string? BenefitKind { get; set; }
        /// <summary>Discount percent when valid.</summary>
        public decimal? DiscountPercent { get; set; }
        /// <summary>Discount months; null means ongoing.</summary>
        public int? DiscountMonths { get; set; }
        /// <summary>Required paid package key.</summary>
        public string? BillingPlanKey { get; set; }
    }
}
