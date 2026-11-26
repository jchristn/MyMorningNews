namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for CatalogPublication records. Null properties are not applied.
    /// </summary>
    public class CatalogPublicationFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Category exactly when set.
        /// </summary>
        public string? Category { get; set; } = null;

        /// <summary>
        /// Match Language exactly when set.
        /// </summary>
        public string? Language { get; set; } = null;

        /// <summary>
        /// Match Status exactly when set.
        /// </summary>
        public string? Status { get; set; } = null;

        /// <summary>
        /// Match FreeEligible exactly when set.
        /// </summary>
        public bool? FreeEligible { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;

        /// <summary>
        /// Tenant of the subscriber that Subscribed is evaluated for. Required with SubscriberUserId when Subscribed is set.
        /// </summary>
        public string? SubscriberTenantId { get; set; } = null;

        /// <summary>
        /// User whose subscriptions Subscribed is evaluated for. Required with SubscriberTenantId when Subscribed is set.
        /// </summary>
        public string? SubscriberUserId { get; set; } = null;

        /// <summary>
        /// When true, only publications the subscriber has an active subscription to; when false, only those without
        /// one. Ignored when null or when the subscriber is not set.
        /// </summary>
        public bool? Subscribed { get; set; } = null;
    }
}
