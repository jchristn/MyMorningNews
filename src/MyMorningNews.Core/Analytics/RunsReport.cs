namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Research runs for a window (plan section 25.6, charts 2 to 5 and 21 to 24): runs by day and status, stage
    /// duration percentiles, findings by tier, duplicates, and sources per run, the relevance score distribution,
    /// activity by day of week and hour, and search provider yield. Dry runs are excluded. Days and hours are in
    /// <see cref="TimeZone"/>; every day and every day-and-hour cell is present, with zeros where nothing ran.
    /// </summary>
    public class RunsReport
    {
        /// <summary>
        /// Start of the window (inclusive), UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End of the window (exclusive), UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// IANA time zone the days and hours are in. Defaults to UTC.
        /// </summary>
        public string TimeZone { get; set; } = "UTC";

        /// <summary>
        /// Runs created in the window.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Estimated cost of the runs in the range, in US dollars. Null when none was priced.
        /// </summary>
        public decimal? TotalCostUsd { get; set; } = null;

        /// <summary>
        /// Mean estimated cost per priced run, in US dollars. Null when none was priced.
        /// </summary>
        public decimal? MeanCostPerRunUsd { get; set; } = null;

        /// <summary>
        /// Runs by status. Every run status is present.
        /// </summary>
        public Dictionary<string, int> ByStatus { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Runs per day by status, oldest first, gap-filled.
        /// </summary>
        public List<RunDayRow> ByDay { get; set; } = new List<RunDayRow>();

        /// <summary>
        /// Duration percentiles per stage, in pipeline order.
        /// </summary>
        public List<StageDurationRow> Stages { get; set; } = new List<StageDurationRow>();

        /// <summary>
        /// The most recent runs (at most 60 by default, see <see cref="RunAnalyticsService.MaxRunPoints"/>), oldest first:
        /// findings by tier, duplicate share, and sources examined, fetched, and used.
        /// </summary>
        public List<RunPointRow> Runs { get; set; } = new List<RunPointRow>();

        /// <summary>
        /// True when the window holds more runs than the runs list shows.
        /// </summary>
        public bool RunsTruncated { get; set; } = false;

        /// <summary>
        /// Non-duplicate findings created in the window, by relevance score in ten buckets of ten points (0 to 100).
        /// </summary>
        public List<ScoreBucket> ScoreHistogram { get; set; } = new List<ScoreBucket>();

        /// <summary>
        /// Runs by day of week and hour, 168 cells ordered Sunday first and then by hour.
        /// </summary>
        public List<ActivityCell> Activity { get; set; } = new List<ActivityCell>();

        /// <summary>
        /// Search results and the findings they led to, per provider, most results first.
        /// </summary>
        public List<ProviderYieldRow> Providers { get; set; } = new List<ProviderYieldRow>();
    }
}
