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
    /// Tenants (control plane). Global administrators see and manage every tenant; others see their own.
    /// </summary>
    public class AdminTenantsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminTenantsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List tenants.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Tenant items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Tenant>> ListAsync(TenantListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Tenant>>(HttpMethod.Get, "/v1.0/tenants", (options ?? new TenantListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Tenant.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Tenant> GetAsync(string tenantId, CancellationToken token = default)
        {
            return Client.SendAsync<Tenant>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)), null, null, token);
        }

        /// <summary>
        /// Create a tenant (global administrators).
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Tenant.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Tenant> CreateAsync(TenantUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Tenant>(HttpMethod.Post, "/v1.0/tenants", null, request, token);
        }

        /// <summary>
        /// Update a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Tenant.</returns>
        /// <exception cref="ArgumentNullException">tenantId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Tenant> UpdateAsync(string tenantId, TenantUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Tenant>(HttpMethod.Put, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)), null, request, token);
        }

        /// <summary>
        /// Delete a tenant and all of its data (global administrators).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string tenantId, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)), null, null, token);
        }
    }
}
