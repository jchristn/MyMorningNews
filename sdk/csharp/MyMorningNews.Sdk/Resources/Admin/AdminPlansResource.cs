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
    /// Plans and their entitlement values (global administrators).
    /// </summary>
    public class AdminPlansResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminPlansResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List plans with entitlement values.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of PlanResponse items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<PlanResponse>> ListAsync(CancellationToken token = default)
        {
            return Client.SendAsync<List<PlanResponse>>(HttpMethod.Get, "/v1.0/api/admin/plans", null, null, token);
        }

        /// <summary>
        /// Create a plan.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PlanResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PlanResponse> CreateAsync(PlanUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<PlanResponse>(HttpMethod.Post, "/v1.0/api/admin/plans", null, request, token);
        }

        /// <summary>
        /// Update a plan and its values.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PlanResponse.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PlanResponse> UpdateAsync(string id, PlanUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<PlanResponse>(HttpMethod.Put, "/v1.0/api/admin/plans/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }
    }
}
