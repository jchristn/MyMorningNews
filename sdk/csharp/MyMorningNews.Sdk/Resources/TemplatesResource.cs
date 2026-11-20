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
    /// Briefing templates.
    /// </summary>
    public class TemplatesResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public TemplatesResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List templates the caller can use (shared tenant templates plus their own).
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of BriefingTemplate items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<BriefingTemplate>> ListAsync(TemplateListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<BriefingTemplate>>(HttpMethod.Get, "/v1.0/api/briefing-templates", (options ?? new TemplateListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a template with its current content.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TemplateDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TemplateDetailResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<TemplateDetailResponse>(HttpMethod.Get, "/v1.0/api/briefing-templates/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Create (import) a template from Markdown.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TemplateDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TemplateDetailResponse> CreateAsync(TemplateCreateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<TemplateDetailResponse>(HttpMethod.Post, "/v1.0/api/briefing-templates", null, request, token);
        }

        /// <summary>
        /// Save a new template version.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TemplateDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TemplateDetailResponse> UpdateAsync(string id, TemplateSaveRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<TemplateDetailResponse>(HttpMethod.Put, "/v1.0/api/briefing-templates/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a template.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/briefing-templates/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// List template versions.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of BriefingTemplateVersion items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<BriefingTemplateVersion>> ListVersionsAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<List<BriefingTemplateVersion>>(HttpMethod.Get, "/v1.0/api/briefing-templates/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/versions", null, null, token);
        }

        /// <summary>
        /// Validate template Markdown without saving.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TemplateValidationResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TemplateValidationResponse> ValidateAsync(TemplateValidateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<TemplateValidationResponse>(HttpMethod.Post, "/v1.0/api/briefing-templates/validate", null, request, token);
        }

        /// <summary>
        /// Restore an earlier version (saved as a new version).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="version">Version number to restore.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TemplateDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TemplateDetailResponse> RollbackAsync(string id, int version, CancellationToken token = default)
        {
            return Client.SendAsync<TemplateDetailResponse>(HttpMethod.Post, "/v1.0/api/briefing-templates/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/rollback", null, new TemplateRollbackRequest { Version = version }, token);
        }

        /// <summary>
        /// Reset a seeded template to the shipped default.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TemplateDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TemplateDetailResponse> ResetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<TemplateDetailResponse>(HttpMethod.Post, "/v1.0/api/briefing-templates/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/reset", null, null, token);
        }

        /// <summary>
        /// Export the current version as Markdown text.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<string> ExportAsync(string id, CancellationToken token = default)
        {
            return Client.SendTextAsync(HttpMethod.Get, "/v1.0/api/briefing-templates/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/export", null, null, token);
        }
    }
}
