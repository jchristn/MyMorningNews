namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Collections.Generic;
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

        /// <summary>
        /// Preview which earlier issues of this edition the next composition would compare with, using optional unsaved
        /// settings. Nothing is saved. A null property in the request keeps the saved value.
        /// </summary>
        /// <param name="id">Edition identifier.</param>
        /// <param name="request">Unsaved comparison settings, or null to preview the saved settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The HistoryPreviewResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error (400 ValidationFailed for out-of-range settings, 409 Conflict when a pinned issue is unavailable, 404 when not found).</exception>
        public Task<HistoryPreviewResponse> PreviewHistoryAsync(string id, HistoryPreviewRequest? request = null, CancellationToken token = default)
        {
            return Client.SendAsync<HistoryPreviewResponse>(HttpMethod.Post, "/v1.0/api/editions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/history/preview", null, request ?? new HistoryPreviewRequest(), token);
        }

        /// <summary>
        /// System briefings (catalog publications) included in the edition, in edition order, one page at a time. Entries
        /// with Available false are skipped at composition.
        /// </summary>
        /// <param name="id">Edition identifier.</param>
        /// <param name="options">Paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of EditionPublicationResponse items in edition order.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<EditionPublicationResponse>> GetPublicationsAsync(string id, ListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<EditionPublicationResponse>>(HttpMethod.Get, "/v1.0/api/editions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/publications", (options ?? new ListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Replace the system briefings included in the edition (ordered, distinct, at most 20, each an active
        /// subscription of the caller). An empty list removes them all and is always allowed.
        /// </summary>
        /// <param name="id">Edition identifier.</param>
        /// <param name="request">Ordered publication identifiers.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The EditionResponse.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="CapReachedException">The plan does not include custom research (402) and the list is not empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error (400 ValidationFailed for unsubscribed, duplicate, or too many entries).</exception>
        public Task<EditionResponse> SetPublicationsAsync(string id, EditionPublicationsRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<EditionResponse>(HttpMethod.Put, "/v1.0/api/editions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/publications", null, request, token);
        }
    }
}
