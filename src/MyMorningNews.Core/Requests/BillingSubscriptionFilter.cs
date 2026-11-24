namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for BillingSubscription records. Null properties are not applied.
    /// </summary>
    public class BillingSubscriptionFilter : EnumerationQuery
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match Provider exactly when set.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Match Environment exactly when set.
        /// </summary>
        public string? Environment { get; set; } = null;

        /// <summary>
        /// Match ProviderId exactly when set.
        /// </summary>
        public string? ProviderId { get; set; } = null;

        /// <summary>
        /// Match Status exactly when set.
        /// </summary>
        public string? Status { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
