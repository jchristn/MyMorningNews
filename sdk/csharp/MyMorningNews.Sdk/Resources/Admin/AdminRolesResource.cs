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
    /// Roles (built-in and tenant).
    /// </summary>
    public class AdminRolesResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminRolesResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List roles.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of UserRole items.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<UserRole>> ListAsync(string tenantId, RoleListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<UserRole>>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/roles", (options ?? new RoleListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a role with its permissions.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="roleId">Role identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RoleDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">tenantId or roleId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RoleDetailResponse> GetAsync(string tenantId, string roleId, CancellationToken token = default)
        {
            return Client.SendAsync<RoleDetailResponse>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/roles/" + MyMorningNewsClient.Segment(roleId, nameof(roleId)), null, null, token);
        }

        /// <summary>
        /// Create a tenant role.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The UserRole.</returns>
        /// <exception cref="ArgumentNullException">tenantId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<UserRole> CreateAsync(string tenantId, RoleUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<UserRole>(HttpMethod.Post, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/roles", null, request, token);
        }

        /// <summary>
        /// Update a tenant role (409 for built-in roles).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="roleId">Role identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The UserRole.</returns>
        /// <exception cref="ArgumentNullException">tenantId or roleId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<UserRole> UpdateAsync(string tenantId, string roleId, RoleUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<UserRole>(HttpMethod.Put, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/roles/" + MyMorningNewsClient.Segment(roleId, nameof(roleId)), null, request, token);
        }

        /// <summary>
        /// Delete a tenant role.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="roleId">Role identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">tenantId or roleId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string tenantId, string roleId, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/roles/" + MyMorningNewsClient.Segment(roleId, nameof(roleId)), null, null, token);
        }
    }
}
