namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for PromoCode records. Null properties are not applied.
    /// </summary>
    public class PromoCodeFilter : EnumerationFilter
    {
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
