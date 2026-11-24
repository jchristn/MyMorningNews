namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for PromoCode records. Null properties are not applied.
    /// </summary>
    public class PromoCodeFilter : EnumerationQuery
    {
        /// <summary>Computed status: active, disabled, notstarted, expired, or exhausted. Null includes all.</summary>
        public string? Status { get; set; } = null;

        /// <summary>UTC instant used consistently for status filtering and counting.</summary>
        public DateTime StatusAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Match Code exactly when set.
        /// </summary>
        public string? Code { get; set; } = null;

        /// <summary>
        /// Match PlanId exactly when set.
        /// </summary>
        public string? PlanId { get; set; } = null;

        /// <summary>
        /// Match Enabled exactly when set.
        /// </summary>
        public bool? Enabled { get; set; } = null;

        /// <summary>
        /// Match BatchId exactly when set.
        /// </summary>
        public string? BatchId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
