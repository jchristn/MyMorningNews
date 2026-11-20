namespace MyMorningNews.Sdk.Resources.Admin
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;
    using MyMorningNews.Sdk.Resources;

    /// <summary>
    /// Plan assignments (global administrators).
    /// </summary>
    public class AdminPlanAssignmentsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminPlanAssignmentsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// A user's plan assignments.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of PlanAssignment items.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<PlanAssignment>> ListAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            return Client.SendAsync<List<PlanAssignment>>(HttpMethod.Get, "/v1.0/api/admin/plan-assignments", Query("tenantId", tenantId, "userId", userId), null, token);
        }

        /// <summary>
        /// Give a user a plan.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PlanAssignment.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PlanAssignment> CreateAsync(PlanGrantRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<PlanAssignment>(HttpMethod.Post, "/v1.0/api/admin/plan-assignments", null, request, token);
        }

        /// <summary>
        /// Revoke a plan assignment.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id or tenantId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/admin/plan-assignments/" + MyMorningNewsClient.Segment(id, nameof(id)), Query("tenantId", tenantId), null, token);
        }
    }
}
