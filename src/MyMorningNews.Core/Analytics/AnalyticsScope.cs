namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Who and what an analytics query covers.
    /// </summary>
    public class AnalyticsScope
    {
        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// User, or null for the tenant.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Topic, or null for all.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Start.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow.AddDays(-90);

        /// <summary>
        /// End.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;
    }
}
