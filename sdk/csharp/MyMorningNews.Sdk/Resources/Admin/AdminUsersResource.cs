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
    /// Users within a tenant.
    /// </summary>
    public class AdminUsersResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminUsersResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List users.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of UserResponse items.</returns>
        /// <exception cref="ArgumentNullException">tenantId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<UserResponse>> ListAsync(string tenantId, UserListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<UserResponse>>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users", (options ?? new UserListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The UserResponse.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<UserResponse> GetAsync(string tenantId, string userId, CancellationToken token = default)
        {
            return Client.SendAsync<UserResponse>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users/" + MyMorningNewsClient.Segment(userId, nameof(userId)), null, null, token);
        }

        /// <summary>
        /// Create a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The UserResponse.</returns>
        /// <exception cref="ArgumentNullException">tenantId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<UserResponse> CreateAsync(string tenantId, UserCreateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<UserResponse>(HttpMethod.Post, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users", null, request, token);
        }

        /// <summary>
        /// Update a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The UserResponse.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<UserResponse> UpdateAsync(string tenantId, string userId, UserUpdateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<UserResponse>(HttpMethod.Put, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users/" + MyMorningNewsClient.Segment(userId, nameof(userId)), null, request, token);
        }

        /// <summary>
        /// Delete a user and their credentials.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string tenantId, string userId, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users/" + MyMorningNewsClient.Segment(userId, nameof(userId)), null, null, token);
        }

        /// <summary>
        /// Effective permissions for a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EffectivePermissionsResponse.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EffectivePermissionsResponse> GetEffectivePermissionsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            return Client.SendAsync<EffectivePermissionsResponse>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users/" + MyMorningNewsClient.Segment(userId, nameof(userId)) + "/permissions", null, null, token);
        }
    }
}
