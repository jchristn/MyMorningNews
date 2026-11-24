namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for Evidence records. Null properties are not applied.
    /// </summary>
    public class EvidenceFilter : EnumerationQuery
    {
        /// <summary>
        /// Match SourceItemId exactly when set.
        /// </summary>
        public string? SourceItemId { get; set; } = null;

        /// <summary>
        /// Match RunId exactly when set.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Match Verified exactly when set.
        /// </summary>
        public bool? Verified { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
