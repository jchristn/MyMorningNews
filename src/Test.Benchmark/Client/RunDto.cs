namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// A research run.
    /// </summary>
    public class RunDto
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Topic.
        /// </summary>
        public string TopicId { get; set; } = String.Empty;

        /// <summary>
        /// Status: Queued, Running, Completed, PartiallySucceeded, Failed, Cancelled, or Skipped.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Whether this is a dry run.
        /// </summary>
        public bool IsDryRun { get; set; } = false;

        /// <summary>
        /// Queued time.
        /// </summary>
        public DateTime QueuedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Start time.
        /// </summary>
        public DateTime? StartedUtc { get; set; } = null;

        /// <summary>
        /// Completion time.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Error, or null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Skip reason, or null.
        /// </summary>
        public string? SkippedReason { get; set; } = null;

        /// <summary>
        /// Summary, or null.
        /// </summary>
        public string? Summary { get; set; } = null;

        /// <summary>
        /// Server-side pipeline duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// New findings.
        /// </summary>
        public int NewFindingCount { get; set; } = 0;

        /// <summary>
        /// Duplicates found.
        /// </summary>
        public int DuplicateCount { get; set; } = 0;

        /// <summary>
        /// Sources read.
        /// </summary>
        public int SourceCount { get; set; } = 0;

        /// <summary>
        /// Input tokens.
        /// </summary>
        public int InputTokens { get; set; } = 0;

        /// <summary>
        /// Output tokens.
        /// </summary>
        public int OutputTokens { get; set; } = 0;

        /// <summary>
        /// Estimated cost in USD, or null.
        /// </summary>
        public decimal? EstimatedCostUsd { get; set; } = null;

        /// <summary>
        /// Whether the run has finished (any status other than Queued and Running).
        /// </summary>
        public bool Finished
        {
            get { return Status != "Queued" && Status != "Running" && Status.Length > 0; }
        }
    }
}
