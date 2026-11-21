namespace MyMorningNews.Core.Responses
{
    /// <summary>
    /// Model cost and token totals for a set of model calls.
    /// </summary>
    public class CostTotals
    {
        /// <summary>
        /// Estimated cost in US dollars, from the endpoints' prices. Null when none of the calls was priced; when some
        /// calls are unpriced, this covers only the priced ones (see <see cref="UnpricedCallCount"/>).
        /// </summary>
        public decimal? EstimatedCostUsd { get; set; } = null;

        /// <summary>
        /// Cost of input tokens, in US dollars. Null when unpriced.
        /// </summary>
        public decimal? InputCostUsd { get; set; } = null;

        /// <summary>
        /// Cost of output tokens, in US dollars. Null when unpriced.
        /// </summary>
        public decimal? OutputCostUsd { get; set; } = null;

        /// <summary>
        /// Model calls.
        /// </summary>
        public int LlmCallCount { get; set; } = 0;

        /// <summary>
        /// Calls with no price: the endpoint has no prices set, or a subscription covers it (agent CLIs signed in with a
        /// subscription).
        /// </summary>
        public int UnpricedCallCount { get; set; } = 0;

        /// <summary>
        /// Failed calls (they can still cost input tokens).
        /// </summary>
        public int FailedCallCount { get; set; } = 0;

        /// <summary>
        /// Input tokens, including the cached and cache-write tokens counted below.
        /// </summary>
        public int InputTokens { get; set; } = 0;

        /// <summary>
        /// Output tokens.
        /// </summary>
        public int OutputTokens { get; set; } = 0;

        /// <summary>
        /// Input tokens read from the provider's prompt cache (part of <see cref="InputTokens"/>).
        /// </summary>
        public int CachedInputTokens { get; set; } = 0;

        /// <summary>
        /// Input tokens written to the provider's prompt cache (part of <see cref="InputTokens"/>).
        /// </summary>
        public int CacheWriteTokens { get; set; } = 0;

        /// <summary>
        /// Reasoning (thinking) tokens.
        /// </summary>
        public int ReasoningTokens { get; set; } = 0;

        /// <summary>
        /// Time spent in model calls, in milliseconds.
        /// </summary>
        public double ModelDurationMs { get; set; } = 0;
    }
}
