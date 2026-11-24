namespace MyMorningNews.Core.Briefings
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// What a composition covers: topics, the period, the optional run, and the template.
    /// </summary>
    public class ComposeScope
    {
        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Reader.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Reader display name for the prompts, or null.
        /// </summary>
        public string? ReaderName { get; set; } = null;

        /// <summary>
        /// IANA time zone for dates in the title. Default UTC.
        /// </summary>
        public string TimeZone { get; set; } = "UTC";

        /// <summary>
        /// Topics in order.
        /// </summary>
        public List<Topic> Topics { get; set; } = new List<Topic>();

        /// <summary>
        /// Limit findings, predictions, and suggestions to one run, or null for the period.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Period start.
        /// </summary>
        public DateTime PeriodStartUtc { get; set; } = DateTime.UtcNow.AddDays(-1);

        /// <summary>
        /// Period end.
        /// </summary>
        public DateTime PeriodEndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Edition name for the title, or null.
        /// </summary>
        public string? EditionName { get; set; } = null;

        /// <summary>
        /// Template.
        /// </summary>
        public ResolvedTemplate Template { get; set; } = new ResolvedTemplate();

        /// <summary>
        /// Use dry-run data and mark model calls as dry runs.
        /// </summary>
        public bool IsDryRun { get; set; } = false;

        /// <summary>Frozen comparison instructions assembled once for this composition. Empty until composition starts.</summary>
        public string HistoryInstructions { get; set; } = String.Empty;

        /// <summary>
        /// Save sections, items, and files. False for previews.
        /// </summary>
        public bool Save { get; set; } = true;
    }
}
