namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// What a briefing was compared with and the structured changes found (plan section 6.2).
    /// </summary>
    public class BriefingComparisonResponse
    {
        /// <summary>
        /// Briefing.
        /// </summary>
        public string BriefingId { get; set; } = String.Empty;

        /// <summary>
        /// True when at least one earlier issue was compared.
        /// </summary>
        public bool Compared { get; set; } = false;

        /// <summary>
        /// Comparison mode used (Off, PreviousIssue, RecentIssues, SinceDate, SpecificIssue). Edition-level comparisons are
        /// prefixed "Edition:"; for example "Edition:PreviousIssue".
        /// </summary>
        public string Mode { get; set; } = "Off";

        /// <summary>
        /// Explanation for a standalone briefing (comparison off, or no earlier issue), otherwise null.
        /// </summary>
        public string? Note { get; set; } = null;

        /// <summary>
        /// The baseline was cut to its budget.
        /// </summary>
        public bool Truncated { get; set; } = false;

        /// <summary>
        /// Issues created before this time were eligible, or null when no comparison was recorded.
        /// </summary>
        public DateTime? CutoffUtc { get; set; } = null;

        /// <summary>
        /// Issues compared with, newest first.
        /// </summary>
        public List<ComparisonBaseline> Baselines { get; set; } = new List<ComparisonBaseline>();

        /// <summary>
        /// Changes by kind (NewDevelopment, ChangedFact, Resolved, Correction, Unchanged, Unavailable). Every kind is present.
        /// </summary>
        public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// True when compared and nothing new, changed, corrected, or resolved was found.
        /// </summary>
        public bool NoMaterialChanges { get; set; } = false;

        /// <summary>
        /// One-line summary, the same sentence the briefing text, email, and audio carry. Null when not compared.
        /// </summary>
        public string? Summary { get; set; } = null;

        /// <summary>
        /// Structured changes in display order.
        /// </summary>
        public List<BriefingDelta> Deltas { get; set; } = new List<BriefingDelta>();
    }
}
