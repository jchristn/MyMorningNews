namespace MyMorningNews.Core.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Time-bucketed request history summary. Every bucket in the range is present, including empty ones.
    /// </summary>
    public class RequestHistorySummary
    {
        /// <summary>
        /// Total requests.
        /// </summary>
        public long TotalCount { get; set; } = 0;

        /// <summary>
        /// Successful requests.
        /// </summary>
        public long TotalSuccess { get; set; } = 0;

        /// <summary>
        /// Failed requests.
        /// </summary>
        public long TotalFailure { get; set; } = 0;

        /// <summary>
        /// Average duration in milliseconds.
        /// </summary>
        public double AverageDurationMs { get; set; } = 0;

        /// <summary>
        /// Buckets in ascending time order.
        /// </summary>
        public List<RequestHistoryBucket> Buckets { get; set; } = new List<RequestHistoryBucket>();
    }
}
