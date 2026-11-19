namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Accumulated state of one gateway invocation across retries and repair turns.
    /// </summary>
    public class CallState
    {
        /// <summary>
        /// Request.
        /// </summary>
        public ModelCallRequest Request { get; }

        /// <summary>
        /// Resolved model.
        /// </summary>
        public ResolvedModel Resolved { get; }

        /// <summary>
        /// Rendered prompt of the last attempt.
        /// </summary>
        public string? Prompt { get; set; } = null;

        /// <summary>
        /// Final output.
        /// </summary>
        public string? Output { get; set; } = null;

        /// <summary>
        /// Input tokens across attempts.
        /// </summary>
        public int InputTokens { get; set; } = 0;

        /// <summary>
        /// Output tokens across attempts.
        /// </summary>
        public int OutputTokens { get; set; } = 0;

        /// <summary>
        /// Duration across attempts in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Cached input tokens across attempts.
        /// </summary>
        public int CachedInputTokens { get; set; } = 0;

        /// <summary>
        /// Cache-write tokens across attempts.
        /// </summary>
        public int CacheWriteTokens { get; set; } = 0;

        /// <summary>
        /// Reasoning tokens across attempts.
        /// </summary>
        public int ReasoningTokens { get; set; } = 0;

        /// <summary>
        /// HTTP attempts including retries and repair turns.
        /// </summary>
        public int Attempts { get; set; } = 0;

        /// <summary>
        /// Whether the final attempt streamed.
        /// </summary>
        public bool Streamed { get; set; } = false;

        /// <summary>
        /// Time to first token of the final attempt in milliseconds.
        /// </summary>
        public double? TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Streaming window of the final attempt in milliseconds.
        /// </summary>
        public double? StreamingDurationMs { get; set; } = null;

        /// <summary>
        /// Chunks received across attempts.
        /// </summary>
        public int ChunkCount { get; set; } = 0;

        /// <summary>
        /// Output tokens per second of the final attempt.
        /// </summary>
        public double? OutputTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Tool names called across attempts.
        /// </summary>
        public List<string> ToolNames { get; set; } = new List<string>();

        /// <summary>
        /// Estimated cost in USD, set when the call is logged.
        /// </summary>
        public decimal? EstimatedCostUsd { get; set; } = null;

        /// <summary>
        /// Repair turns used.
        /// </summary>
        public int Repairs { get; set; } = 0;

        /// <summary>
        /// Logged call identifier.
        /// </summary>
        public string? LlmCallId { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <param name="resolved">Resolved model.</param>
        public CallState(ModelCallRequest request, ResolvedModel resolved)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Resolved = resolved ?? throw new ArgumentNullException(nameof(resolved));
        }

        /// <summary>
        /// Copy accumulated metrics onto a result.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="result">Result.</param>
        /// <returns>The same result.</returns>
        public T ToResult<T>(T result) where T : ModelCallResult
        {
            ArgumentNullException.ThrowIfNull(result);
            result.Model = Resolved.Model;
            result.ModelEndpointId = Resolved.Endpoint.Id;
            result.InputTokens = InputTokens;
            result.OutputTokens = OutputTokens;
            result.DurationMs = Math.Round(DurationMs, 2);
            result.Repairs = Repairs;
            result.LlmCallId = LlmCallId;
            result.CachedInputTokens = CachedInputTokens;
            result.ReasoningTokens = ReasoningTokens;
            result.TimeToFirstTokenMs = TimeToFirstTokenMs;
            result.EstimatedCostUsd = EstimatedCostUsd;
            return result;
        }
    }
}
