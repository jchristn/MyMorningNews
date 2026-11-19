namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// Aggregated model usage for one group.
    /// </summary>
    public class UsageRow
    {
        /// <summary>
        /// Group key (user ID, model, task, and so on).
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Display label (for example the user email).
        /// </summary>
        public string? Label { get; set; } = null;

        /// <summary>
        /// Calls.
        /// </summary>
        public long Calls { get; set; } = 0;

        /// <summary>
        /// Failed calls.
        /// </summary>
        public long Failures { get; set; } = 0;

        /// <summary>
        /// Input tokens.
        /// </summary>
        public long InputTokens { get; set; } = 0;

        /// <summary>
        /// Cached input tokens.
        /// </summary>
        public long CachedInputTokens { get; set; } = 0;

        /// <summary>
        /// Cache-write tokens.
        /// </summary>
        public long CacheWriteTokens { get; set; } = 0;

        /// <summary>
        /// Output tokens.
        /// </summary>
        public long OutputTokens { get; set; } = 0;

        /// <summary>
        /// Reasoning tokens.
        /// </summary>
        public long ReasoningTokens { get; set; } = 0;

        /// <summary>
        /// Total tokens.
        /// </summary>
        public long TotalTokens { get; set; } = 0;

        /// <summary>
        /// Tool calls.
        /// </summary>
        public long ToolCalls { get; set; } = 0;

        /// <summary>
        /// Estimated cost in USD.
        /// </summary>
        public decimal CostUsd { get; set; } = 0;

        /// <summary>
        /// Average duration.
        /// </summary>
        public double AverageDurationMs { get; set; } = 0;

        /// <summary>
        /// Average time to first token.
        /// </summary>
        public double? AverageTimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// 95th percentile time to first token.
        /// </summary>
        public double? P95TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Share of the report total cost, 0 to 1.
        /// </summary>
        public double CostShare { get; set; } = 0;
    }
}
