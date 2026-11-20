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
    /// Permissions (built-in and tenant). The server has no read-by-id route.
    /// </summary>
    public class AdminPermissionsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminPermissionsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List permissions.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Permission items.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Permission>> ListAsync(string tenantId, PermissionListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Permission>>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/permissions", (options ?? new PermissionListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Create a tenant permission.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Permission.</returns>
        /// <exception cref="ArgumentNullException">tenantId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Permission> CreateAsync(string tenantId, PermissionUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Permission>(HttpMethod.Post, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/permissions", null, request, token);
        }

        /// <summary>
        /// Update a tenant permission.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="permissionId">Permission identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Permission.</returns>
        /// <exception cref="ArgumentNullException">tenantId or permissionId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Permission> UpdateAsync(string tenantId, string permissionId, PermissionUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Permission>(HttpMethod.Put, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/permissions/" + MyMorningNewsClient.Segment(permissionId, nameof(permissionId)), null, request, token);
        }

        /// <summary>
        /// Delete a tenant permission.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="permissionId">Permission identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">tenantId or permissionId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string tenantId, string permissionId, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/permissions/" + MyMorningNewsClient.Segment(permissionId, nameof(permissionId)), null, null, token);
        }
    }
}
