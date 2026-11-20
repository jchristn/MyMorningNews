namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for PlanFeature records. Null properties are not applied.
    /// </summary>
    public class PlanFeatureFilter : EnumerationFilter
    {
        /// <summary>
        /// Match PlanId exactly when set.
        /// </summary>
        public string? PlanId { get; set; } = null;

        /// <summary>
        /// Match Key exactly when set.
        /// </summary>
        public string? Key { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
