namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Vault credentials. Reads never return the secret; RevealAsync does, and is audited.
    /// </summary>
    public class VaultResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public VaultResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List vault credentials.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of VaultCredentialResponse items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<VaultCredentialResponse>> ListAsync(VaultCredentialListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<VaultCredentialResponse>>(HttpMethod.Get, "/v1.0/api/vault/credentials", (options ?? new VaultCredentialListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a vault credential.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The VaultCredentialResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<VaultCredentialResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<VaultCredentialResponse>(HttpMethod.Get, "/v1.0/api/vault/credentials/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Create a vault credential.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The VaultCredentialResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<VaultCredentialResponse> CreateAsync(VaultCredentialUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<VaultCredentialResponse>(HttpMethod.Post, "/v1.0/api/vault/credentials", null, request, token);
        }

        /// <summary>
        /// Update a vault credential.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The VaultCredentialResponse.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<VaultCredentialResponse> UpdateAsync(string id, VaultCredentialUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<VaultCredentialResponse>(HttpMethod.Put, "/v1.0/api/vault/credentials/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a vault credential.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/vault/credentials/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Reveal a stored secret (audited, rate limited).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The VaultRevealResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<VaultRevealResponse> RevealAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<VaultRevealResponse>(HttpMethod.Post, "/v1.0/api/vault/credentials/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/reveal", null, null, token);
        }

        /// <summary>
        /// Records that use the credential.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of VaultUsageItem items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<VaultUsageItem>> GetUsageAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<List<VaultUsageItem>>(HttpMethod.Get, "/v1.0/api/vault/credentials/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/usage", null, null, token);
        }
    }
}
