namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// One time bucket in a request history summary.
    /// </summary>
    public class RequestHistoryBucket
    {
        /// <summary>
        /// Bucket start (UTC, inclusive).
        /// </summary>
        public DateTime BucketStartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket end (UTC, exclusive).
        /// </summary>
        public DateTime BucketEndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Requests with status below 400.
        /// </summary>
        public long SuccessCount { get; set; } = 0;

        /// <summary>
        /// Requests with status 400 or above.
        /// </summary>
        public long FailureCount { get; set; } = 0;

        /// <summary>
        /// Average duration in milliseconds.
        /// </summary>
        public double AverageDurationMs { get; set; } = 0;
    }
}
