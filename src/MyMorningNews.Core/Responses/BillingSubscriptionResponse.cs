namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Models;

    /// <summary>Verified subscription status without payment identifiers or verification credentials.</summary>
    public class BillingSubscriptionResponse
    {
        /// <summary>Internal subscription identifier.</summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>Stripe, Apple, or Google.</summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>Verified provider environment.</summary>
        public string Environment { get; set; } = String.Empty;

        /// <summary>Base plan identifier.</summary>
        public string PlanId { get; set; } = String.Empty;

        /// <summary>Active, Grace, Suspended, Canceled, Expired, or Revoked.</summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>Verified paid or grace period end.</summary>
        public DateTime? EndsUtc { get; set; } = null;

        /// <summary>Whether renewal has been canceled.</summary>
        public bool CancelAtPeriodEnd { get; set; } = false;

        /// <summary>Last authoritative provider verification.</summary>
        public DateTime LastVerifiedUtc { get; set; } = DateTime.UtcNow;

    }
}
