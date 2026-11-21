namespace MyMorningNews.Core.Services
{
    using System;
    using System.Net.Http;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using SyslogLogging;

    /// <summary>
    /// Creates PolyPrompt clients for model endpoints. Every client shares one HttpClient, which clients never dispose.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class ModelClientFactory
    {
        #region Public-Members

        /// <summary>
        /// Default Ollama endpoint when none is configured.
        /// </summary>
        public string DefaultOllamaEndpoint { get; set; } = "http://host.docker.internal:11434";

        #endregion

        #region Private-Members

        private readonly HttpClient _Http;
        private readonly LoggingModule? _Logging;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="http">Shared HTTP client; created when null.</param>
        /// <param name="logging">Logging module handed to PolyPrompt so its messages follow the server's severity and console settings.</param>
        public ModelClientFactory(HttpClient? http = null, LoggingModule? logging = null)
        {
            _Http = http ?? new HttpClient(Security.NetworkGuard.CreateHandler()) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a client. Dispose it after use.
        /// </summary>
        /// <param name="endpoint">Endpoint.</param>
        /// <param name="apiKey">API key, when required.</param>
        /// <returns>Client.</returns>
        /// <exception cref="ArgumentNullException">endpoint is null.</exception>
        /// <exception cref="ArgumentException">A required API key or endpoint URL is missing.</exception>
        public ProviderClientBase Create(ModelEndpoint endpoint, string? apiKey)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ClientOptions options = new ClientOptions { HttpClient = _Http, TimeoutMs = endpoint.TimeoutMs, RedactCredentials = true, Logging = _Logging };
            string? url = String.IsNullOrWhiteSpace(endpoint.Endpoint) ? null : endpoint.Endpoint.Trim();
            switch (endpoint.Provider)
            {
                case ModelProviderEnum.Anthropic:
                    return new AnthropicClient(Require(apiKey, endpoint), url, options);
                case ModelProviderEnum.OpenAi:
                    return new OpenAiClient(Require(apiKey, endpoint), url, options);
                case ModelProviderEnum.Gemini:
                    return new GeminiClient(Require(apiKey, endpoint), url, options);
                case ModelProviderEnum.XAi:
                    return new XAiClient(Require(apiKey, endpoint), url, options);
                case ModelProviderEnum.OllamaCloud:
                    return new OllamaCloudClient(Require(apiKey, endpoint), url, options);
                case ModelProviderEnum.OpenAiCompatible:
                    if (url == null) throw new ArgumentException("Endpoint " + endpoint.Name + " needs a base URL.");
                    return new OpenAiCompatibleClient(url, String.IsNullOrEmpty(apiKey) ? null : apiKey, options);
                case ModelProviderEnum.Ollama:
                    return new OllamaClient(url ?? DefaultOllamaEndpoint, String.IsNullOrEmpty(apiKey) ? null : apiKey, options);
                case ModelProviderEnum.ClaudeCode:
                case ModelProviderEnum.Codex:
                case ModelProviderEnum.GeminiCli:
                case ModelProviderEnum.Mux:
                    throw new ArgumentException(endpoint.Name + " is an agent CLI; it runs through AgentCliService, not an HTTP client.");
                default:
                    throw new ArgumentException("Unsupported provider " + endpoint.Provider + ".", nameof(endpoint));
            }
        }

        #endregion

        #region Private-Methods

        private static string Require(string? apiKey, ModelEndpoint endpoint)
        {
            if (String.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Endpoint " + endpoint.Name + " (" + endpoint.Provider + ") needs an API key. Add one on the Setup page, or store it under Stored credentials and select it on the endpoint.");
            return apiKey;
        }

        #endregion
    }
}
