namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Models;

    /// <summary>Account-bound purchase offers, captured signup benefit and verified access.</summary>
    public class BillingStatusResponse
    {
        /// <summary>Whether new purchases are enabled.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Account binding UUID for native purchases.</summary>
        public string PurchaseAccountToken { get; set; } = String.Empty;

        /// <summary>Whether Apple verification is configured.</summary>
        public bool AppleAvailable { get; set; } = false;

        /// <summary>Whether Google verification is configured.</summary>
        public bool GoogleAvailable { get; set; } = false;

        /// <summary>Available package offers.</summary>
        public List<BillingOfferResponse> Offers { get; set; } = new List<BillingOfferResponse>();

        /// <summary>Captured signup offer, if any. Payment verification is still required.</summary>
        public SignupBenefit? SignupBenefit { get; set; } = null;

        /// <summary>Verified subscriptions owned by this account.</summary>
        public List<BillingSubscriptionResponse> Subscriptions { get; set; } = new List<BillingSubscriptionResponse>();

        /// <summary>Effective package including administrator overrides.</summary>
        public EffectivePlan Plan { get; set; } = new EffectivePlan();

    }
}
