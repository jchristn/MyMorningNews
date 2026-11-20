namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Fetched sources.
    /// </summary>
    public class SourcesResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public SourcesResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List fetched sources for the caller's topics (content text is left out).
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of SourceItem items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<SourceItem>> ListAsync(SourceListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<SourceItem>>(HttpMethod.Get, "/v1.0/api/sources", (options ?? new SourceListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a fetched source.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SourceItem.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SourceItem> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<SourceItem>(HttpMethod.Get, "/v1.0/api/sources/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }
    }
}
