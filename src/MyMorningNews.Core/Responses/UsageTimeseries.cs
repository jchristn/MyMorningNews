namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

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
        /// Buckets in ascending order, including empty ones.
        /// </summary>
        public List<UsageBucket> Buckets { get; set; } = new List<UsageBucket>();
    }
}
