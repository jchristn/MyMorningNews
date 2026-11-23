namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Enumeration filter for BriefingShare records. Null properties are not applied.
    /// </summary>
    public class BriefingShareFilter : EnumerationFilter
    {
        /// <summary>
        /// Match UserId exactly when set.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Match BriefingId exactly when set.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
