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
    /// Editions: named briefings composed from several topics.
    /// </summary>
    public class EditionsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public EditionsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List editions.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Edition items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Edition>> ListAsync(EditionListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Edition>>(HttpMethod.Get, "/v1.0/api/editions", (options ?? new EditionListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a edition.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EditionResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EditionResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<EditionResponse>(HttpMethod.Get, "/v1.0/api/editions/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Create a edition.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EditionResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EditionResponse> CreateAsync(EditionUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<EditionResponse>(HttpMethod.Post, "/v1.0/api/editions", null, request, token);
        }

        /// <summary>
        /// Update a edition.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EditionResponse.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EditionResponse> UpdateAsync(string id, EditionUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<EditionResponse>(HttpMethod.Put, "/v1.0/api/editions/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a edition.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/editions/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Set an edition's topics in order (an empty list means all of the owner's active recurring topics).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EditionResponse.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EditionResponse> SetTopicsAsync(string id, EditionTopicsRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<EditionResponse>(HttpMethod.Put, "/v1.0/api/editions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/topics", null, request, token);
        }

        /// <summary>
        /// Compose the edition now (HTTP 202 with the queued briefing; 409 when one is already on the way). Composing calls the model.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Briefing.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Briefing> ComposeAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Briefing>(HttpMethod.Post, "/v1.0/api/editions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/compose", null, null, token);
        }

        /// <summary>
        /// Preview the edition from the latest data without saving.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Optional unsaved template Markdown, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Briefing.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Briefing> PreviewAsync(string id, EditionPreviewRequest? request = null, CancellationToken token = default)
        {
            return Client.SendAsync<Briefing>(HttpMethod.Post, "/v1.0/api/editions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/preview", null, request, token);
        }
    }
}
