namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Plan records. Null properties are not applied.
    /// </summary>
    public class PlanFilter : EnumerationQuery
    {
        /// <summary>
        /// Match Key exactly when set.
        /// </summary>
        public string? Key { get; set; } = null;

        /// <summary>
        /// Match IsDefault exactly when set.
        /// </summary>
        public bool? IsDefault { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
