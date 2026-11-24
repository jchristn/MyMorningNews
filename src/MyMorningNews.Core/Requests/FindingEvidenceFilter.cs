namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Enumeration filter for FindingEvidence records. Null properties are not applied.
    /// </summary>
    public class FindingEvidenceFilter : EnumerationQuery
    {
        /// <summary>
        /// Match FindingId exactly when set.
        /// </summary>
        public string? FindingId { get; set; } = null;

        /// <summary>
        /// Match EvidenceId exactly when set.
        /// </summary>
        public string? EvidenceId { get; set; } = null;

        /// <summary>
        /// Match Active exactly when set.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
