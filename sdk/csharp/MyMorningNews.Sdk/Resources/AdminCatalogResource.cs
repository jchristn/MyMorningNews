namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Net.Http;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>Platform account/catalog API. Stateless and safe to share; server authorization is authoritative.</summary>
    public class AdminCatalogResource : ResourceBase
    {
        /// <summary>Bind the resource to a client.</summary>
        public AdminCatalogResource(MyMorningNewsClient client) : base(client) { }

        /// <summary>System administrator: list every publication lifecycle state.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<EnumerationResult<CatalogPublication>> ListAsync(ListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<CatalogPublication>>(HttpMethod.Get, "/v1.0/api/admin/catalog", (options ?? new ListOptions()).ToQuery(), null, token);
        }

        /// <summary>System administrator: read a canonical publication.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<CatalogPublication> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<CatalogPublication>(HttpMethod.Get, "/v1.0/api/admin/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>System administrator: create a canonical system topic and publication.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<CatalogPublication> CreateAsync(CatalogPublicationRequest body, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(body);
            return Client.SendAsync<CatalogPublication>(HttpMethod.Post, "/v1.0/api/admin/catalog", null, body, token);
        }

        /// <summary>System administrator: update publication with current revision for conflict protection.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<CatalogPublication> UpdateAsync(string id, CatalogPublicationRequest body, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(body);
            return Client.SendAsync<CatalogPublication>(HttpMethod.Put, "/v1.0/api/admin/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)), null, body, token);
        }

        /// <summary>System administrator: inspect canonical issue projections.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<EnumerationResult<CatalogIssueResponse>> IssuesAsync(string id, ListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<CatalogIssueResponse>>(HttpMethod.Get, "/v1.0/api/admin/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/issues", (options ?? new ListOptions()).ToQuery(), null, token);
        }

        /// <summary>System administrator: read canonical research and schedule configuration.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<CatalogResearchResponse> ResearchAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<CatalogResearchResponse>(HttpMethod.Get, "/v1.0/api/admin/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/research", null, null, token);
        }

        /// <summary>System administrator: configure canonical research and schedule.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<CatalogResearchResponse> SaveResearchAsync(string id, CatalogResearchRequest body, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(body);
            return Client.SendAsync<CatalogResearchResponse>(HttpMethod.Put, "/v1.0/api/admin/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/research", null, body, token);
        }

        /// <summary>
        /// System administrator: preview which published canonical issues the publication's next research run would
        /// compare with, using optional unsaved settings. Nothing is saved. A null property in the request keeps the
        /// saved value.
        /// </summary>
        /// <param name="id">Publication identifier.</param>
        /// <param name="request">Unsaved comparison settings, or null to preview the saved settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The HistoryPreviewResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error (400 ValidationFailed for out-of-range settings, 403 when the caller is not a system administrator, 404 when the publication is not found, 409 Conflict when a pinned issue is unavailable).</exception>
        public Task<HistoryPreviewResponse> PreviewResearchHistoryAsync(string id, HistoryPreviewRequest? request = null, CancellationToken token = default)
        {
            return Client.SendAsync<HistoryPreviewResponse>(HttpMethod.Post, "/v1.0/api/admin/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/research/history/preview", null, request ?? new HistoryPreviewRequest(), token);
        }

        /// <summary>System administrator: start a canonical research run.</summary>
        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>
        public Task<Run> RunAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Run>(HttpMethod.Post, "/v1.0/api/admin/catalog/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/run", null, null, token);
        }
    }
}
