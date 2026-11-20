namespace MyMorningNews.Sdk.Resources.Admin
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Resources;

    /// <summary>
    /// Captured request history.
    /// </summary>
    public class AdminRequestHistoryResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public AdminRequestHistoryResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List captured requests.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of RequestHistoryEntry items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<RequestHistoryEntry>> ListAsync(RequestHistoryListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<RequestHistoryEntry>>(HttpMethod.Get, "/v1.0/api/request-history", (options ?? new RequestHistoryListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a captured request with headers and bodies.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RequestHistoryEntry.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RequestHistoryEntry> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<RequestHistoryEntry>(HttpMethod.Get, "/v1.0/api/request-history/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Delete a captured request.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/request-history/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Delete captured requests matching a filter.
        /// </summary>
        /// <param name="options">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The DeleteManyResponse.</returns>
        /// <exception cref="ArgumentNullException">options is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<DeleteManyResponse> DeleteManyAsync(RequestHistoryListOptions options, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            return Client.SendAsync<DeleteManyResponse>(HttpMethod.Delete, "/v1.0/api/request-history", options.ToQuery(), null, token);
        }

        /// <summary>
        /// Time-bucketed request counts. FromUtc, ToUtc, and BucketMinutes are required.
        /// </summary>
        /// <param name="options">Filter with FromUtc, ToUtc, and BucketMinutes.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RequestHistorySummary.</returns>
        /// <exception cref="ArgumentNullException">options is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RequestHistorySummary> GetSummaryAsync(RequestHistoryListOptions options, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            return Client.SendAsync<RequestHistorySummary>(HttpMethod.Get, "/v1.0/api/request-history/summary", options.ToQuery(), null, token);
        }
    }
}
