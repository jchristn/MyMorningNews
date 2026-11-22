namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Workspace setup: whether the workspace has a chat model with a working key and web search, live checks that prove
    /// it, the model providers and sign-in methods the server offers, connecting one, and storing a model API key. Until
    /// Ready is true, refinement sessions and research requests fail with SetupRequiredException (409 SetupRequired).
    /// Thread safety: stateless; safe to share.
    /// </summary>
    public class SetupResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public SetupResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// The workspace setup status. Ready is false until every required check is Ready. CanConfigure tells whether the
        /// caller (a global or tenant administrator) can change the configuration.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SetupStatusResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SetupStatusResponse> GetStatusAsync(CancellationToken token = default)
        {
            return Client.SendAsync<SetupStatusResponse>(HttpMethod.Get, "/v1.0/api/setup", null, null, token);
        }

        /// <summary>
        /// Re-check setup with live calls (administrators only, 403 otherwise). Sends a short probe prompt to each chat
        /// endpoint, a test query to each default web search provider, one embedding request, and one short speech
        /// request. Calls the providers.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SetupStatusResponse after the checks.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error, for example 403 for a non-administrator.</exception>
        public Task<SetupStatusResponse> VerifyAsync(CancellationToken token = default)
        {
            return Client.SendAsync<SetupStatusResponse>(HttpMethod.Post, "/v1.0/api/setup/verify", null, null, token);
        }

        /// <summary>
        /// Re-check one kind of setup check with live calls (administrators only, 403 otherwise), for example only the
        /// speech endpoint. The other checks are reported from stored results.
        /// </summary>
        /// <param name="check">The kind to re-check: Model, Search, Embeddings, or Speech.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SetupStatusResponse after the check.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error, for example 403 for a non-administrator.</exception>
        public Task<SetupStatusResponse> VerifyAsync(SetupCheckKindEnum check, CancellationToken token = default)
        {
            return Client.SendAsync<SetupStatusResponse>(HttpMethod.Post, "/v1.0/api/setup/verify", Query("check", check.ToString()), null, token);
        }

        /// <summary>
        /// Model providers and sign-in methods the Setup page can connect on this server (administrators only, 403
        /// otherwise): hosted APIs (API key), agent CLIs in the server image (Claude Code, Codex, Gemini CLI, Mux), and
        /// self-hosted servers, each with whether it is installed and allowed and its sign-in methods, recommended first.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SetupOptionsResponse.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error, for example 403 for a non-administrator.</exception>
        public Task<SetupOptionsResponse> GetOptionsAsync(CancellationToken token = default)
        {
            return Client.SendAsync<SetupOptionsResponse>(HttpMethod.Get, "/v1.0/api/setup/options", null, null, token);
        }

        /// <summary>
        /// Connect the workspace's chat model (administrators only, 403 otherwise). Stores the secret for the chosen sign-in
        /// method (an API key, a claude setup-token token, or a sign-in file's contents) encrypted in the vault, tests it
        /// with a live call, and points every chat task at the endpoint. When the workspace already has a working model, the
        /// new sign-in is tested first and the working one is kept if the test fails; Connection on the result says what
        /// happened (Succeeded, KeptPrevious, Message). The secret is never returned. 400 ValidationFailed, with
        /// Details["field"] set to provider, authMethod, secret, model, or baseUrl, for a missing provider or secret, a
        /// method the provider does not accept, or an agent CLI that is not allowed or not installed; 404 when the named
        /// endpoint does not exist.
        /// </summary>
        /// <param name="request">Provider, sign-in method, secret, and optional model, base URL, and endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SetupStatusResponse after the endpoint was checked.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SetupStatusResponse> ConnectAsync(SetupConnectRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<SetupStatusResponse>(HttpMethod.Post, "/v1.0/api/setup/connect", null, request, token);
        }

        /// <summary>
        /// Store an API key for a model endpoint and verify it with a live call (administrators only, 403 otherwise). The key
        /// is stored encrypted in the vault, attached to the endpoint, and never returned. 400 ValidationFailed for a
        /// missing value, 404 when the endpoint does not exist.
        /// </summary>
        /// <param name="modelEndpointId">Model endpoint.</param>
        /// <param name="apiKey">API key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SetupStatusResponse after the key was checked.</returns>
        /// <exception cref="ArgumentNullException">modelEndpointId or apiKey is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SetupStatusResponse> SetModelKeyAsync(string modelEndpointId, string apiKey, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(modelEndpointId)) throw new ArgumentNullException(nameof(modelEndpointId));
            if (String.IsNullOrWhiteSpace(apiKey)) throw new ArgumentNullException(nameof(apiKey));
            SetupModelKeyRequest request = new SetupModelKeyRequest { ModelEndpointId = modelEndpointId, ApiKey = apiKey };
            return Client.SendAsync<SetupStatusResponse>(HttpMethod.Post, "/v1.0/api/setup/model-key", null, request, token);
        }
    }
}
