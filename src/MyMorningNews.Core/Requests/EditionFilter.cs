namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Edition records. Null properties are not applied.
    /// </summary>
    public class EditionFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match Enabled exactly when set.
        /// </summary>
        public bool? Enabled { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
