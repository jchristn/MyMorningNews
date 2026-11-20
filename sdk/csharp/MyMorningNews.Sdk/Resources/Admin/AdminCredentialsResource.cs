namespace MyMorningNews.Sdk.Resources.Admin
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;
    using MyMorningNews.Sdk.Resources;

    /// <summary>
    /// Access key credentials within a tenant.
    /// </summary>
    public class AdminCredentialsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminCredentialsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List credentials (secrets are never returned).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of CredentialResponse items.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<CredentialResponse>> ListAsync(string tenantId, CredentialListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<CredentialResponse>>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/credentials", (options ?? new CredentialListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The CredentialResponse.</returns>
        /// <exception cref="ArgumentNullException">tenantId or credentialId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<CredentialResponse> GetAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            return Client.SendAsync<CredentialResponse>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/credentials/" + MyMorningNewsClient.Segment(credentialId, nameof(credentialId)), null, null, token);
        }

        /// <summary>
        /// Create a credential. The secret key is returned once.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The CredentialCreatedResponse.</returns>
        /// <exception cref="ArgumentNullException">tenantId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<CredentialCreatedResponse> CreateAsync(string tenantId, CredentialCreateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<CredentialCreatedResponse>(HttpMethod.Post, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/credentials", null, request, token);
        }

        /// <summary>
        /// Update a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The CredentialResponse.</returns>
        /// <exception cref="ArgumentNullException">tenantId or credentialId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<CredentialResponse> UpdateAsync(string tenantId, string credentialId, CredentialUpdateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<CredentialResponse>(HttpMethod.Put, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/credentials/" + MyMorningNewsClient.Segment(credentialId, nameof(credentialId)), null, request, token);
        }

        /// <summary>
        /// Delete a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">tenantId or credentialId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/credentials/" + MyMorningNewsClient.Segment(credentialId, nameof(credentialId)), null, null, token);
        }

        /// <summary>
        /// Effective permissions for a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EffectivePermissionsResponse.</returns>
        /// <exception cref="ArgumentNullException">tenantId or credentialId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EffectivePermissionsResponse> GetEffectivePermissionsAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            return Client.SendAsync<EffectivePermissionsResponse>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/credentials/" + MyMorningNewsClient.Segment(credentialId, nameof(credentialId)) + "/permissions", null, null, token);
        }
    }
}
