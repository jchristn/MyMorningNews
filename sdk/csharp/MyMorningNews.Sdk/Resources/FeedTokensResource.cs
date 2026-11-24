namespace MyMorningNews.Sdk.Resources
{
    using System;
    using MyMorningNews.Sdk.Options;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Private podcast feed tokens. The server has no read-by-id route; list and filter instead.
    /// </summary>
    public class FeedTokensResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public FeedTokensResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List the caller's feed tokens (hashes redacted).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of FeedToken items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        /// <param name="options">Pagination, sorting, and filters; null uses defaults.</param>
        public Task<EnumerationResult<FeedToken>> ListAsync(EnumerationQuery? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<FeedToken>>(HttpMethod.Get, "/v1.0/api/feed-tokens", (options ?? new EnumerationQuery()).ToQuery(), null, token);
        }

        /// <summary>
        /// Create a private podcast feed. The secret and feed URL are returned once.
        /// </summary>
        /// <param name="request">Optional name and edition, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The FeedTokenCreated.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<FeedTokenCreated> CreateAsync(FeedTokenCreateRequest? request = null, CancellationToken token = default)
        {
            return Client.SendAsync<FeedTokenCreated>(HttpMethod.Post, "/v1.0/api/feed-tokens", null, request, token);
        }

        /// <summary>
        /// Revoke an active token; with purge, delete an already revoked one.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="purge">Delete a revoked token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, bool purge = false, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/feed-tokens/" + MyMorningNewsClient.Segment(id, nameof(id)), Query("purge", purge ? (object)true : null), null, token);
        }
    }
}
