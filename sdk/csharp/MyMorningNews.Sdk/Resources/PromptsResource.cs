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
    /// Prompt templates, versions, topic overrides, and the prompt lab.
    /// </summary>
    public class PromptsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public PromptsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List prompt templates.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of PromptTemplate items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<PromptTemplate>> ListAsync(PromptListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<PromptTemplate>>(HttpMethod.Get, "/v1.0/api/prompts", (options ?? new PromptListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a prompt template with its content.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromptTemplateResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromptTemplateResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<PromptTemplateResponse>(HttpMethod.Get, "/v1.0/api/prompts/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Save a new version (400 ValidationFailed with details when the prompt is not valid).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromptTemplateResponse.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromptTemplateResponse> UpdateAsync(string id, PromptTemplateSaveRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<PromptTemplateResponse>(HttpMethod.Put, "/v1.0/api/prompts/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a topic override (409 for tenant prompts; reset them instead).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/prompts/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Version history.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of PromptTemplateVersion items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        /// <param name="options">Pagination, sorting, and filters; null uses defaults.</param>
        public Task<EnumerationResult<PromptTemplateVersion>> ListVersionsAsync(string id, EnumerationQuery? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<PromptTemplateVersion>>(HttpMethod.Get, "/v1.0/api/prompts/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/versions", (options ?? new EnumerationQuery()).ToQuery(), null, token);
        }

        /// <summary>
        /// Restore an earlier version.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="version">Version number to restore.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromptTemplateResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromptTemplateResponse> RollbackAsync(string id, int version, CancellationToken token = default)
        {
            return Client.SendAsync<PromptTemplateResponse>(HttpMethod.Post, "/v1.0/api/prompts/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/rollback", Query("version", version), null, token);
        }

        /// <summary>
        /// Restore the shipped default.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromptTemplateResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromptTemplateResponse> ResetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<PromptTemplateResponse>(HttpMethod.Post, "/v1.0/api/prompts/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/reset", null, null, token);
        }

        /// <summary>
        /// Create a topic override of a prompt.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromptTemplateResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromptTemplateResponse> CreateOverrideAsync(PromptOverrideRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<PromptTemplateResponse>(HttpMethod.Post, "/v1.0/api/prompts/overrides", null, request, token);
        }

        /// <summary>
        /// Render a prompt and run it as a dry run in the prompt lab. Calls the model.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PromptLabResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PromptLabResponse> LabAsync(PromptLabRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<PromptLabResponse>(HttpMethod.Post, "/v1.0/api/prompts/lab", null, request, token);
        }
    }
}
