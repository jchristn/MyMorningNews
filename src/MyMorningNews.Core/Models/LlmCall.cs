namespace MyMorningNews.Core.Models
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// One model call made through the model gateway, with tokens, latency, cost, and optional diagnostic payloads.
    /// </summary>
    public class LlmCall
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix llm_).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// Task role.
        /// </summary>
        public ModelTaskEnum Task { get; set; } = ModelTaskEnum.Synthesis;

        /// <summary>
        /// Research run, when any. May be null.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Refinement session, when any. May be null.
        /// </summary>
        public string? RefinementSessionId { get; set; } = null;

        /// <summary>
        /// Briefing, when any. May be null.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        /// <summary>
        /// Endpoint used. May be null.
        /// </summary>
        public string? ModelEndpointId { get; set; } = null;

        /// <summary>Private connection identifier used for this call, or null for a workspace endpoint. Retained after key deletion; contains no credentials.</summary>
        public string? ProviderConnectionId { get; set; } = null;

        /// <summary>
        /// Provider name. May be null.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Model name. May be null.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Prompt template key. May be null.
        /// </summary>
        public string? PromptKey { get; set; } = null;

        /// <summary>
        /// Prompt version used. May be null.
        /// </summary>
        public string? PromptTemplateVersionId { get; set; } = null;

        /// <summary>
        /// User the call is attributed to (the caller, or the topic owner for scheduled work). May be null.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Topic, when any. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Whether the response was streamed.
        /// </summary>
        public bool Streamed { get; set; } = false;

        /// <summary>
        /// HTTP attempts including retries and repair turns.
        /// </summary>
        public int Attempts { get; set; } = 1;

        /// <summary>
        /// Input (prompt) tokens, including cached tokens.
        /// </summary>
        public int InputTokens { get; set; } = 0;

        /// <summary>
        /// Input tokens served from the provider prompt cache.
        /// </summary>
        public int CachedInputTokens { get; set; } = 0;

        /// <summary>
        /// Input tokens written to the provider prompt cache.
        /// </summary>
        public int CacheWriteTokens { get; set; } = 0;

        /// <summary>
        /// Output (completion) tokens, including reasoning tokens.
        /// </summary>
        public int OutputTokens { get; set; } = 0;

        /// <summary>
        /// Reasoning tokens reported by the provider.
        /// </summary>
        public int ReasoningTokens { get; set; } = 0;

        /// <summary>
        /// Total tokens.
        /// </summary>
        public int TotalTokens { get; set; } = 0;

        /// <summary>
        /// Wall-clock duration across attempts in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Time to the first streamed token of the final attempt, in milliseconds. May be null.
        /// </summary>
        public double? TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Time from the first to the last streamed token of the final attempt, in milliseconds. May be null.
        /// </summary>
        public double? StreamingDurationMs { get; set; } = null;

        /// <summary>
        /// Streamed chunks received.
        /// </summary>
        public int ChunkCount { get; set; } = 0;

        /// <summary>
        /// Output tokens per second over the streaming window. May be null.
        /// </summary>
        public double? OutputTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Tool calls returned by the model.
        /// </summary>
        public int ToolCallCount { get; set; } = 0;

        /// <summary>
        /// Names of tools the model called.
        /// </summary>
        public List<string> ToolNames
        {
            get
            {
                return _ToolNames;
            }
            set
            {
                _ToolNames = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Whether the call succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Provider HTTP status. May be null.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Finish or stop reason. May be null.
        /// </summary>
        public string? FinishReason { get; set; } = null;

        /// <summary>
        /// Error message. May be null.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Repair turns needed to obtain valid structured output.
        /// </summary>
        public int StructuredOutputRepairs { get; set; } = 0;

        /// <summary>
        /// Estimated input cost in USD (uncached input plus cache reads and writes). May be null.
        /// </summary>
        public decimal? InputCostUsd { get; set; } = null;

        /// <summary>
        /// Estimated output cost in USD. May be null.
        /// </summary>
        public decimal? OutputCostUsd { get; set; } = null;

        /// <summary>
        /// Estimated total cost in USD. May be null.
        /// </summary>
        public decimal? EstimatedCostUsd { get; set; } = null;

        /// <summary>
        /// Prompt lab or dry-run call.
        /// </summary>
        public bool IsDryRun { get; set; } = false;

        /// <summary>
        /// Rendered prompt (diagnostic, retention bound). May be null.
        /// </summary>
        public string? PromptText { get; set; } = null;

        /// <summary>
        /// Raw response (diagnostic, retention bound). May be null.
        /// </summary>
        public string? ResponseText { get; set; } = null;

        /// <summary>
        /// Whether the record is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time of the last update.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Run stage the call was made in, when it was made during a research run.
        /// </summary>
        public string? RunStageId { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateLlmCallId();
        private string _TenantId = String.Empty;
        private List<string> _ToolNames = new List<string>();

        #endregion
    }
}
