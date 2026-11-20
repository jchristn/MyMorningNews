namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// One recorded model call.
    /// </summary>
    public class LlmCallDto
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Task role.
        /// </summary>
        public string Task { get; set; } = String.Empty;

        /// <summary>
        /// Endpoint used, or null.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>
        /// Model, or null.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Prompt key, or null.
        /// </summary>
        public string? PromptKey { get; set; } = null;

        /// <summary>
        /// HTTP attempts including retries.
        /// </summary>
        public int Attempts { get; set; } = 0;

        /// <summary>
        /// Input tokens.
        /// </summary>
        public int InputTokens { get; set; } = 0;

        /// <summary>
        /// Output tokens.
        /// </summary>
        public int OutputTokens { get; set; } = 0;

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Whether the call succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Estimated cost in USD, or null.
        /// </summary>
        public decimal? EstimatedCostUsd { get; set; } = null;
    }
}
