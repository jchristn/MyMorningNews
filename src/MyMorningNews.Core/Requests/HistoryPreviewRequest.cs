namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Unsaved comparison settings to preview. Every property is optional; null keeps the saved value.
    /// </summary>
    public class HistoryPreviewRequest
    {
        /// <summary>
        /// Comparison mode, or null for the saved mode.
        /// </summary>
        public HistoryModeEnum? HistoryMode { get; set; } = null;

        /// <summary>
        /// Maximum baseline issues (1 to 20), or null.
        /// </summary>
        public int? HistoryCount { get; set; } = null;

        /// <summary>
        /// Earliest baseline time for SinceDate, or null.
        /// </summary>
        public DateTime? HistorySinceUtc { get; set; } = null;

        /// <summary>
        /// Pinned baseline for SpecificIssue, an empty string to clear it, or null.
        /// </summary>
        public string? HistoryBriefingId { get; set; } = null;

        /// <summary>
        /// Comparison budget in UTF-8 bytes (256 to 32000), or null.
        /// </summary>
        public int? HistoryTokenBudget { get; set; } = null;
    }
}
