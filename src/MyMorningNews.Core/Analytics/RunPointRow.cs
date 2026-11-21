namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// One run: its findings by tier, its duplicate share, and the sources it examined, fetched, and used.
    /// </summary>
    public class RunPointRow
    {
        /// <summary>
        /// Run.
        /// </summary>
        public string RunId { get; set; } = String.Empty;

        /// <summary>
        /// Topic.
        /// </summary>
        public string TopicId { get; set; } = String.Empty;

        /// <summary>
        /// Topic name. Null when the topic is gone.
        /// </summary>
        public string? TopicName { get; set; } = null;

        /// <summary>
        /// When the run was created, UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Status.
        /// </summary>
        public RunStatusEnum Status { get; set; } = RunStatusEnum.Queued;

        /// <summary>
        /// Total duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Estimated cost in US dollars. Null when none of the run's model calls was priced.
        /// </summary>
        public decimal? EstimatedCostUsd { get; set; } = null;

        /// <summary>
        /// Model calls the run made.
        /// </summary>
        public int LlmCallCount { get; set; } = 0;

        /// <summary>
        /// Model calls with no price (left out of the cost).
        /// </summary>
        public int UnpricedCallCount { get; set; } = 0;

        /// <summary>
        /// Non-duplicate findings by tier. Every tier is present.
        /// </summary>
        public Dictionary<string, int> Tiers { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// New findings the run kept (Run.NewFindingCount).
        /// </summary>
        public int NewFindings { get; set; } = 0;

        /// <summary>
        /// Candidates that repeated an earlier finding (Run.DuplicateCount).
        /// </summary>
        public int Duplicates { get; set; } = 0;

        /// <summary>
        /// Duplicates over duplicates plus new findings, from 0 to 1. Null when the run had neither.
        /// </summary>
        public double? DuplicateShare { get; set; } = null;

        /// <summary>
        /// Search results examined: the Search stage's items out, or the run's stored search results when the stage
        /// row is missing.
        /// </summary>
        public int Examined { get; set; } = 0;

        /// <summary>
        /// Pages read: the Fetch stage's items out, or Run.SourceCount when the stage row is missing.
        /// </summary>
        public int Fetched { get; set; } = 0;

        /// <summary>
        /// Distinct sources behind at least one non-duplicate finding at or above the display threshold.
        /// </summary>
        public int Used { get; set; } = 0;
    }
}
