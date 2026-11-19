namespace MyMorningNews.Core.Settings
{
    using System;

    /// <summary>
    /// Defaults used when seeding a tenant's model endpoints. Existing endpoints are never changed by these settings.
    /// </summary>
    public class ModelSettings
    {
        #region Public-Members

        /// <summary>
        /// Default chat model for the seeded Anthropic endpoint. Default claude-opus-5-5.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Chat model for the seeded Anthropic endpoint.")]
        public string DefaultChatModel
        {
            get { return _DefaultChatModel; }
            set { _DefaultChatModel = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(DefaultChatModel)) : value.Trim(); }
        }

        /// <summary>
        /// Ollama base URL for the seeded embedding endpoint. Default http://host.docker.internal:11434.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Ollama URL for the seeded embedding endpoint.", EnvironmentVariable = "OLLAMA_ENDPOINT")]
        public string DefaultOllamaEndpoint
        {
            get { return _DefaultOllamaEndpoint; }
            set { _DefaultOllamaEndpoint = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(DefaultOllamaEndpoint)) : value.Trim(); }
        }

        /// <summary>
        /// Embedding model for the seeded Ollama endpoint. Default all-minilm (384 dimensions).
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        [Setting(Description = "Embedding model for the seeded Ollama endpoint.")]
        public string DefaultEmbeddingModel
        {
            get { return _DefaultEmbeddingModel; }
            set { _DefaultEmbeddingModel = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(DefaultEmbeddingModel)) : value.Trim(); }
        }

        #endregion

        #region Private-Members

        private string _DefaultChatModel = "claude-opus-5-5";
        private string _DefaultOllamaEndpoint = "http://host.docker.internal:11434";
        private string _DefaultEmbeddingModel = "all-minilm";

        #endregion
    }
}
