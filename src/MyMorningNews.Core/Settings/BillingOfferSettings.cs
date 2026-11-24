namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>Allowlisted purchase product mapping. Provider identifiers are configured by the operator, never accepted from checkout callers.</summary>
    public class BillingOfferSettings
    {
        /// <summary>Stable public offer key.</summary>
        public string Key { get; set; } = String.Empty;
        /// <summary>Entitlement package key.</summary>
        public string PlanKey { get; set; } = String.Empty;
        /// <summary>Month or Year.</summary>
        public string Interval { get; set; } = "Month";
        /// <summary>Web display price in USD cents. Stripe remains the authority for checkout prices.</summary>
        public int UsdCents { get; set; } = 0;
        /// <summary>Recurring Stripe price identifier; empty disables web purchases of this offer.</summary>
        public string StripePriceId { get; set; } = String.Empty;
        /// <summary>Apple auto-renewing subscription product identifier.</summary>
        public string AppleProductId { get; set; } = String.Empty;
        /// <summary>Google Play subscription product identifier.</summary>
        public string GoogleProductId { get; set; } = String.Empty;
        /// <summary>Google Play base plan identifier distinguishing monthly and annual billing.</summary>
        public string GoogleBasePlanId { get; set; } = String.Empty;
        /// <summary>Whether offered to new purchasers. Existing subscriptions retain their product mapping.</summary>
        public bool Active { get; set; } = true;
    }
}
