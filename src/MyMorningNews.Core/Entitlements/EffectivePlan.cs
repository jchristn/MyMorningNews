namespace MyMorningNews.Core.Entitlements
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// The plan a user has right now and its entitlement values.
    /// </summary>
    public class EffectivePlan
    {
        /// <summary>Verified subscription supplying this plan, or null.</summary>
        public string? BillingSubscriptionId { get; set; } = null;
        /// <summary>Payment provider supplying this plan, or null.</summary>
        public string? BillingProvider { get; set; } = null;

        /// <summary>
        /// Plan.
        /// </summary>
        public Plan Plan { get; set; } = new Plan();

        /// <summary>
        /// Whether this account must purchase a subscription for paid access. False does not remove its assigned limits.
        /// </summary>
        public bool SubscriptionRequired { get; set; } = true;

        /// <summary>
        /// Funding mode: Platform, Personal, or SelfHosted.
        /// </summary>
        public string FundingMode { get; set; } = "Platform";

        /// <summary>
        /// Source of each effective feature: plan or user override.
        /// </summary>
        public Dictionary<string, string> FeatureSources { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Entitlement values by key.
        /// </summary>
        public Dictionary<string, string> Features { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// The assignment that gives it, or null for the default plan.
        /// </summary>
        public PlanAssignment? Assignment { get; set; } = null;

        /// <summary>
        /// When it ends, or null.
        /// </summary>
        public DateTime? EndsUtc { get; set; } = null;

        /// <summary>
        /// Assignments that start later (stacked codes).
        /// </summary>
        public List<PlanAssignment> Upcoming { get; set; } = new List<PlanAssignment>();

        /// <summary>
        /// Plan names by plan ID for the current and upcoming assignments.
        /// </summary>
        public Dictionary<string, string> PlanNames { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
