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
    /// Suggestions.
    /// </summary>
    public class SuggestionsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public SuggestionsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List suggestions.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Suggestion items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Suggestion>> ListAsync(SuggestionListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Suggestion>>(HttpMethod.Get, "/v1.0/api/suggestions", (options ?? new SuggestionListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a suggestion.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Suggestion.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Suggestion> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Suggestion>(HttpMethod.Get, "/v1.0/api/suggestions/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Accept, snooze (with a future SnoozedUntilUtc), dismiss, or complete a suggestion.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Suggestion.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Suggestion> SetStatusAsync(string id, SuggestionStatusRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Suggestion>(HttpMethod.Put, "/v1.0/api/suggestions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/status", null, request, token);
        }
    }
}
