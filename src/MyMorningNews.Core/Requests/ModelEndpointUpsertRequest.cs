namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Create or update a model endpoint. Null properties are unchanged on update.
    /// </summary>
    public class ModelEndpointUpsertRequest
    {
        /// <summary>
        /// Name (required on create).
        /// </summary>
        [TextLimit(128, AllowBlank = false)]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Provider.
        /// </summary>
        public ModelProviderEnum? Provider { get; set; } = null;

        /// <summary>
        /// Base URL; empty uses the provider default.
        /// </summary>
        [TextLimit(512)]
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Vault credential for the API key; empty string clears it.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Convenience: create a vault credential from this key and reference it.
        /// </summary>
        public string? ApiKey { get; set; } = null;

        /// <summary>
        /// Chat model.
        /// </summary>
        [TextLimit(256)]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Embedding model.
        /// </summary>
        [TextLimit(256)]
        public string? EmbeddingModel { get; set; } = null;

        /// <summary>
        /// Serves chat.
        /// </summary>
        public bool? SupportsChat { get; set; } = null;

        /// <summary>
        /// Serves embeddings.
        /// </summary>
        public bool? SupportsEmbeddings { get; set; } = null;

        /// <summary>
        /// Temperature (ignored for Anthropic).
        /// </summary>
        public double? Temperature { get; set; } = null;

        /// <summary>
        /// Maximum output tokens.
        /// </summary>
        public int? MaxTokens { get; set; } = null;

        /// <summary>
        /// Default reasoning effort.
        /// </summary>
        public ReasoningEffortEnum? ReasoningEffort { get; set; } = null;

        /// <summary>
        /// Context length.
        /// </summary>
        public int? ContextLength { get; set; } = null;

        /// <summary>
        /// Timeout in milliseconds.
        /// </summary>
        public int? TimeoutMs { get; set; } = null;

        /// <summary>
        /// Input cost per million tokens.
        /// </summary>
        public decimal? InputCostPerMTok { get; set; } = null;

        /// <summary>
        /// Output cost per million tokens.
        /// </summary>
        public decimal? OutputCostPerMTok { get; set; } = null;

        /// <summary>
        /// Cached input (cache read) cost per million tokens. Null falls back to the input rate.
        /// </summary>
        public decimal? CachedInputCostPerMTok { get; set; } = null;

        /// <summary>
        /// Cache write cost per million tokens. Null falls back to the input rate.
        /// </summary>
        public decimal? CacheWriteCostPerMTok { get; set; } = null;

        /// <summary>
        /// Active.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
