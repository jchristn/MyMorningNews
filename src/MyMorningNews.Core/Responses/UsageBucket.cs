namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// Model usage in one time bucket.
    /// </summary>
    public class UsageBucket
    {
        /// <summary>
        /// Bucket start.
        /// </summary>
        public DateTime BucketStartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket end.
        /// </summary>
        public DateTime BucketEndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Calls.
        /// </summary>
        public long Calls { get; set; } = 0;

        /// <summary>
        /// Failures.
        /// </summary>
        public long Failures { get; set; } = 0;

        /// <summary>
        /// Input tokens.
        /// </summary>
        public long InputTokens { get; set; } = 0;

        /// <summary>
        /// Output tokens.
        /// </summary>
        public long OutputTokens { get; set; } = 0;

        /// <summary>
        /// Cached input tokens.
        /// </summary>
        public long CachedInputTokens { get; set; } = 0;

        /// <summary>
        /// Cost in USD.
        /// </summary>
        public decimal CostUsd { get; set; } = 0;

        /// <summary>
        /// Average time to first token.
        /// </summary>
        public double? AverageTimeToFirstTokenMs { get; set; } = null;
    }
}
