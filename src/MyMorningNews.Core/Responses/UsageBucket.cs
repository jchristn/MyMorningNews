namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

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
        /// Cache-write tokens.
        /// </summary>
        public long CacheWriteTokens { get; set; } = 0;

        /// <summary>
        /// Reasoning tokens.
        /// </summary>
        public long ReasoningTokens { get; set; } = 0;

        /// <summary>
        /// Tool calls.
        /// </summary>
        public long ToolCalls { get; set; } = 0;

        /// <summary>
        /// Cost in USD.
        /// </summary>
        public decimal CostUsd { get; set; } = 0;

        /// <summary>
        /// Average time to first token.
        /// </summary>
        public double? AverageTimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// 95th percentile time to first token. Null when no call in the bucket streamed.
        /// </summary>
        public double? P95TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// One row per group with calls in this bucket, in the same order as the series groups. Empty when the series is not grouped.
        /// The bucket's own fields always cover every call in the bucket, whatever the grouping.
        /// </summary>
        public List<UsageRow> Rows
        {
            get
            {
                return _Rows;
            }
            set
            {
                _Rows = value ?? new List<UsageRow>();
            }
        }

        private List<UsageRow> _Rows = new List<UsageRow>();
    }
}
