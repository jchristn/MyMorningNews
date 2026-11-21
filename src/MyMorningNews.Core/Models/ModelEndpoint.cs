namespace MyMorningNews.Core.Models
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// A model provider endpoint reached through PolyPrompt.
    /// </summary>
    public class ModelEndpoint
    {
        #region Public-Members

        /// <summary>
        /// Identifier (prefix mdl_).
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
        /// Display name.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Provider.
        /// </summary>
        public ModelProviderEnum Provider { get; set; } = ModelProviderEnum.Anthropic;

        /// <summary>
        /// Base URL; null uses the provider default. May be null.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Vault credential holding the API key. May be null.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Chat model name, for example claude-opus-5-5. May be null.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Embedding model name, for example all-minilm. May be null.
        /// </summary>
        public string? EmbeddingModel { get; set; } = null;

        /// <summary>
        /// Detected embedding dimensionality. May be null.
        /// </summary>
        public int? EmbeddingDimensions { get; set; } = null;

        /// <summary>
        /// Endpoint serves chat.
        /// </summary>
        public bool SupportsChat { get; set; } = true;

        /// <summary>
        /// Model supports tool calls (probed during validation; null when unknown). May be null.
        /// </summary>
        public bool? SupportsTools { get; set; } = null;

        /// <summary>
        /// Endpoint serves embeddings.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// Sampling temperature; null sends none (always null for Anthropic). Minimum 0, maximum 2; values outside the range are clamped. May be null.
        /// </summary>
        public double? Temperature
        {
            get
            {
                return _Temperature;
            }
            set
            {
                _Temperature = value.HasValue ? Math.Clamp(value.Value, 0, 2) : null;
            }
        }

        /// <summary>
        /// Maximum output tokens. Minimum 1, maximum 128000; values outside the range are clamped.
        /// </summary>
        public int MaxTokens
        {
            get
            {
                return _MaxTokens;
            }
            set
            {
                _MaxTokens = Math.Clamp(value, 1, 128000);
            }
        }

        /// <summary>
        /// Default reasoning effort; roles may override. May be null.
        /// </summary>
        public ReasoningEffortEnum? ReasoningEffort { get; set; } = null;

        /// <summary>
        /// Context length for local models. May be null.
        /// </summary>
        public int? ContextLength { get; set; } = null;

        /// <summary>
        /// Request timeout in milliseconds. Minimum 1000, maximum 3600000; values outside the range are clamped.
        /// </summary>
        public int TimeoutMs
        {
            get
            {
                return _TimeoutMs;
            }
            set
            {
                _TimeoutMs = Math.Clamp(value, 1000, 3600000);
            }
        }

        /// <summary>
        /// Estimated input cost in USD per million tokens. May be null.
        /// </summary>
        public decimal? InputCostPerMTok { get; set; } = null;

        /// <summary>
        /// Estimated output cost in USD per million tokens. May be null.
        /// </summary>
        public decimal? OutputCostPerMTok { get; set; } = null;

        /// <summary>
        /// Estimated cost in USD per million cached input tokens (cache reads). May be null.
        /// </summary>
        public decimal? CachedInputCostPerMTok { get; set; } = null;

        /// <summary>
        /// Estimated cost in USD per million cache-write tokens. May be null.
        /// </summary>
        public decimal? CacheWriteCostPerMTok { get; set; } = null;

        /// <summary>
        /// UTC time of the last validation. May be null.
        /// </summary>
        public DateTime? LastValidatedUtc { get; set; } = null;

        /// <summary>
        /// Result of the last validation. May be null.
        /// </summary>
        public string? LastValidationResult { get; set; } = null;

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
        /// Whether the endpoint last proved it works: true after a successful validation or model call, false after a failed validation, null when never checked or after its connection settings changed.
        /// </summary>
        public bool? LastValidationSucceeded { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateModelEndpointId();
        private string _TenantId = String.Empty;
        private string _Name = String.Empty;
        private double? _Temperature = null;
        private int _MaxTokens = 16000;
        private int _TimeoutMs = 300000;

        #endregion
    }
}
