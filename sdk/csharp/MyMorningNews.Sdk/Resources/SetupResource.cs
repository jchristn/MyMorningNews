namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Workspace setup: whether the workspace has a chat model with a working key and web search, live checks that prove
    /// it, and storing a model API key. Until Ready is true, refinement sessions and research requests fail with
    /// SetupRequiredException (409 SetupRequired).
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
        /// endpoint, a test query to each default web search provider, and one embedding request. Calls the providers.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SetupStatusResponse after the checks.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error, for example 403 for a non-administrator.</exception>
        public Task<SetupStatusResponse> VerifyAsync(CancellationToken token = default)
        {
            return Client.SendAsync<SetupStatusResponse>(HttpMethod.Post, "/v1.0/api/setup/verify", null, null, token);
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
