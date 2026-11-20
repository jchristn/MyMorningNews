namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for PlanAssignment records. Null properties are not applied.
    /// </summary>
    public class PlanAssignmentFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match PlanId exactly when set.
        /// </summary>
        public string? PlanId { get; set; } = null;

        /// <summary>
        /// Match Source exactly when set.
        /// </summary>
        public PlanSourceEnum? Source { get; set; } = null;

        /// <summary>
        /// Match PromoCodeId exactly when set.
        /// </summary>
        public string? PromoCodeId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
