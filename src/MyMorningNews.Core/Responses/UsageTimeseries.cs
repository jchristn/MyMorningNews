namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Gap-filled model usage time series.
    /// </summary>
    public class UsageTimeseries
    {
        /// <summary>
        /// Range start.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Range end.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket width.
        /// </summary>
        public int BucketMinutes { get; set; } = 1440;

        /// <summary>
        /// Grouping applied to the series, or null when the series is not grouped.
        /// </summary>
        public UsageGroupByEnum? GroupBy { get; set; } = null;

        /// <summary>
        /// Range totals for each group, highest cost first, limited to the requested top; the remainder is combined into one
        /// row with the key (other), listed last. Empty when the series is not grouped.
        /// </summary>
        public List<UsageRow> Groups { get; set; } = new List<UsageRow>();

        /// <summary>
        /// Buckets in ascending order, including empty ones.
        /// </summary>
        public List<UsageBucket> Buckets { get; set; } = new List<UsageBucket>();
    }
}
