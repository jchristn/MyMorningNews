namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>Shared issue content projection. Always authorized through the caller's subscription.</summary>
    public class CatalogIssueResponse
    {
        /// <summary>Canonical issue identifier.</summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>Publication identifier.</summary>
        public string PublicationId { get; set; } = String.Empty;

        /// <summary>Issue title.</summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>Composed content on detail reads.</summary>
        public string? Markdown { get; set; } = null;

        /// <summary>Issue timestamp.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Authenticated shared audio path when ready.</summary>
        public string? AudioPath { get; set; } = null;

        /// <summary>Shared episode duration when known.</summary>
        public double? AudioSeconds { get; set; } = null;

        /// <summary>
        /// What the issue was compared with and the changes found, without canonical item identifiers. Filled on the single-issue read; null in issue lists.
        /// </summary>
        public BriefingComparisonResponse? Comparison { get; set; } = null;

    }
}
