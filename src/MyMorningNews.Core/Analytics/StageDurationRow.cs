namespace MyMorningNews.Core.Analytics
{
    using System;

    /// <summary>
    /// How long one pipeline stage takes. Counts finished stage rows (succeeded or failed) of the runs in the window;
    /// percentiles interpolate linearly between the nearest ranks.
    /// </summary>
    public class StageDurationRow
    {
        /// <summary>
        /// Stage name, for example Search.
        /// </summary>
        public string Stage { get; set; } = String.Empty;

        /// <summary>
        /// Position in the pipeline (the stage's lowest sort order).
        /// </summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Finished stage rows measured.
        /// </summary>
        public int Count { get; set; } = 0;

        /// <summary>
        /// Stage rows that failed.
        /// </summary>
        public int Failed { get; set; } = 0;

        /// <summary>
        /// Median duration in milliseconds.
        /// </summary>
        public double P50Ms { get; set; } = 0;

        /// <summary>
        /// 95th percentile duration in milliseconds.
        /// </summary>
        public double P95Ms { get; set; } = 0;

        /// <summary>
        /// Mean duration in milliseconds.
        /// </summary>
        public double MeanMs { get; set; } = 0;

        /// <summary>
        /// Longest duration in milliseconds.
        /// </summary>
        public double MaxMs { get; set; } = 0;

        /// <summary>
        /// Total estimated cost of this stage across the runs, in US dollars. Null when no call was priced.
        /// </summary>
        public decimal? TotalCostUsd { get; set; } = null;

        /// <summary>
        /// Mean estimated cost per run of this stage, in US dollars, over the runs where it was priced. Null when none was.
        /// </summary>
        public decimal? MeanCostUsd { get; set; } = null;

        /// <summary>
        /// Model calls this stage made across the runs.
        /// </summary>
        public int LlmCallCount { get; set; } = 0;
    }
}
