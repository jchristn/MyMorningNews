namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The baseline a topic or edition would compare with if it ran now. Nothing is saved.
    /// </summary>
    public class HistoryPreviewResponse
    {
        /// <summary>
        /// Comparison mode previewed.
        /// </summary>
        public string Mode { get; set; } = "Off";

        /// <summary>
        /// Issues that would be compared, newest first. Empty when the mode is Off or nothing usable exists.
        /// </summary>
        public List<ComparisonBaseline> Baselines { get; set; } = new List<ComparisonBaseline>();

        /// <summary>
        /// Size of the comparison context that would be supplied, in UTF-8 bytes.
        /// </summary>
        public int ContextBytes { get; set; } = 0;

        /// <summary>
        /// Configured budget in UTF-8 bytes.
        /// </summary>
        public int BudgetBytes { get; set; } = 0;

        /// <summary>
        /// The baselines are longer than the budget and would be cut.
        /// </summary>
        public bool Truncated { get; set; } = false;

        /// <summary>
        /// Explanation for a standalone result (comparison off, or no earlier issue), otherwise null.
        /// </summary>
        public string? Note { get; set; } = null;

        /// <summary>
        /// The cutoff used: issues created before this time are eligible.
        /// </summary>
        public DateTime CutoffUtc { get; set; } = DateTime.UtcNow;
    }
}
