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
    /// Search providers and the page fetch tester.
    /// </summary>
    public class SearchProvidersResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public SearchProvidersResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List search providers.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of SearchProvider items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<SearchProvider>> ListAsync(SearchProviderListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<SearchProvider>>(HttpMethod.Get, "/v1.0/api/search-providers", (options ?? new SearchProviderListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a search provider.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SearchProvider.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SearchProvider> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<SearchProvider>(HttpMethod.Get, "/v1.0/api/search-providers/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Create a search provider.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SearchProvider.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SearchProvider> CreateAsync(SearchProviderUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<SearchProvider>(HttpMethod.Post, "/v1.0/api/search-providers", null, request, token);
        }

        /// <summary>
        /// Update a search provider.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SearchProvider.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SearchProvider> UpdateAsync(string id, SearchProviderUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<SearchProvider>(HttpMethod.Put, "/v1.0/api/search-providers/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a search provider.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/search-providers/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Run a test query against the provider.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Optional query, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The SearchOutcome.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<SearchOutcome> TestAsync(string id, SearchTestRequest? request = null, CancellationToken token = default)
        {
            return Client.SendAsync<SearchOutcome>(HttpMethod.Post, "/v1.0/api/search-providers/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/test", null, request, token);
        }

        /// <summary>
        /// Fetch a page as the research pipeline would.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The FetchResult.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<FetchResult> FetchTestAsync(FetchTestRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<FetchResult>(HttpMethod.Post, "/v1.0/api/fetch/test", null, request, token);
        }
    }
}
