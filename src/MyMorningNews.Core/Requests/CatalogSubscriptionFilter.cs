namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for CatalogSubscription records. Null properties are not applied.
    /// </summary>
    public class CatalogSubscriptionFilter : EnumerationQuery
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match PublicationId exactly when set.
        /// </summary>
        public string? PublicationId { get; set; } = null;

        /// <summary>
        /// Match Suspended exactly when set.
        /// </summary>
        public bool? Suspended { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
