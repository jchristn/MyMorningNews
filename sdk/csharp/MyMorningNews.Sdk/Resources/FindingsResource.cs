namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Findings. The server sorts by relevance score by default.
    /// </summary>
    public class FindingsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public FindingsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List findings.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Finding items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Finding>> ListAsync(FindingListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Finding>>(HttpMethod.Get, "/v1.0/api/findings", (options ?? new FindingListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a finding with its evidence and dimension.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The FindingDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<FindingDetailResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<FindingDetailResponse>(HttpMethod.Get, "/v1.0/api/findings/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Pin or hide a finding (hidden findings are left out of lists, briefings, and emails).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Finding.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Finding> UpdateAsync(string id, FindingUpdateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Finding>(HttpMethod.Put, "/v1.0/api/findings/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a finding and its evidence links (shared evidence, sources, and ratings stay).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/findings/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Read one piece of evidence with its source.
        /// </summary>
        /// <param name="evidenceId">Evidence identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EvidenceView.</returns>
        /// <exception cref="ArgumentNullException">evidenceId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EvidenceView> GetEvidenceAsync(string evidenceId, CancellationToken token = default)
        {
            return Client.SendAsync<EvidenceView>(HttpMethod.Get, "/v1.0/api/evidence/" + MyMorningNewsClient.Segment(evidenceId, nameof(evidenceId)), null, null, token);
        }
    }
}
