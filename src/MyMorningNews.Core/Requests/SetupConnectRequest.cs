namespace MyMorningNews.Core.Requests
{
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Connect the workspace's chat model: choose a provider and how it signs in, then verify it with a live call.
    /// </summary>
    public class SetupConnectRequest
    {
        /// <summary>
        /// Provider. Required.
        /// </summary>
        public ModelProviderEnum? Provider { get; set; } = null;

        /// <summary>
        /// Sign-in method. Null uses the provider's recommended method.
        /// </summary>
        public ModelAuthMethodEnum? AuthMethod { get; set; } = null;

        /// <summary>
        /// The API key, the access token (claude setup-token), or the sign-in file's contents (auth.json,
        /// oauth_creds.json, endpoints.json). Required unless the method is ServerSignIn or None, or the endpoint already
        /// has one stored for the same method. Stored encrypted and never returned.
        /// </summary>
        [TextLimit(65536)]
        public string? Secret { get; set; } = null;

        /// <summary>
        /// Model. Null uses the provider's default (an agent CLI's own default when it has one).
        /// </summary>
        [TextLimit(256)]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Base URL for Ollama and OpenAI-compatible servers.
        /// </summary>
        [TextLimit(512)]
        public string? BaseUrl { get; set; } = null;

        /// <summary>
        /// Endpoint to change. Null reuses the workspace's endpoint for the same provider, or creates one.
        /// </summary>
        [TextLimit(64)]
        public string? ModelEndpointId { get; set; } = null;
    }
}
