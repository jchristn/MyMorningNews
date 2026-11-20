namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;

    /// <summary>
    /// Model call records with full accounting.
    /// </summary>
    public class LlmCallsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public LlmCallsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List model calls (payloads are left out).
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of LlmCall items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<LlmCall>> ListAsync(LlmCallListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<LlmCall>>(HttpMethod.Get, "/v1.0/api/llm-calls", (options ?? new LlmCallListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a model call with its payloads.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The LlmCall.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<LlmCall> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<LlmCall>(HttpMethod.Get, "/v1.0/api/llm-calls/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }
    }
}
