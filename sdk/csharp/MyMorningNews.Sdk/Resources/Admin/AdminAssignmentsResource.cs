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
    /// Role assignments for users and scope assignments for credentials.
    /// </summary>
    public class AdminAssignmentsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminAssignmentsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List a user's role assignments.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of UserRoleAssignment items.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<UserRoleAssignment>> ListUserAssignmentsAsync(string tenantId, string userId, ListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<UserRoleAssignment>>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users/" + MyMorningNewsClient.Segment(userId, nameof(userId)) + "/assignments", (options ?? new ListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Assign a role to a user.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The UserRoleAssignment.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<UserRoleAssignment> CreateUserAssignmentAsync(string tenantId, string userId, AssignmentUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<UserRoleAssignment>(HttpMethod.Post, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users/" + MyMorningNewsClient.Segment(userId, nameof(userId)) + "/assignments", null, request, token);
        }

        /// <summary>
        /// Remove a role assignment.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="assignmentId">Assignment identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId or assignmentId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteUserAssignmentAsync(string tenantId, string userId, string assignmentId, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/users/" + MyMorningNewsClient.Segment(userId, nameof(userId)) + "/assignments/" + MyMorningNewsClient.Segment(assignmentId, nameof(assignmentId)), null, null, token);
        }

        /// <summary>
        /// List a credential's scope assignments.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of CredentialScopeAssignment items.</returns>
        /// <exception cref="ArgumentNullException">tenantId or credentialId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<CredentialScopeAssignment>> ListCredentialScopesAsync(string tenantId, string credentialId, ListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<CredentialScopeAssignment>>(HttpMethod.Get, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/credentials/" + MyMorningNewsClient.Segment(credentialId, nameof(credentialId)) + "/scopes", (options ?? new ListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Add a credential scope assignment.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The CredentialScopeAssignment.</returns>
        /// <exception cref="ArgumentNullException">tenantId or credentialId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<CredentialScopeAssignment> CreateCredentialScopeAsync(string tenantId, string credentialId, AssignmentUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<CredentialScopeAssignment>(HttpMethod.Post, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/credentials/" + MyMorningNewsClient.Segment(credentialId, nameof(credentialId)) + "/scopes", null, request, token);
        }

        /// <summary>
        /// Remove a credential scope assignment.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="scopeId">Scope assignment identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">tenantId or credentialId or scopeId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteCredentialScopeAsync(string tenantId, string credentialId, string scopeId, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/tenants/" + MyMorningNewsClient.Segment(tenantId, nameof(tenantId)) + "/credentials/" + MyMorningNewsClient.Segment(credentialId, nameof(credentialId)) + "/scopes/" + MyMorningNewsClient.Segment(scopeId, nameof(scopeId)), null, null, token);
        }
    }
}
