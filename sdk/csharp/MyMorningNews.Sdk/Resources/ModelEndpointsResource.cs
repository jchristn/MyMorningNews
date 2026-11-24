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
    /// Model endpoints and task role assignments.
    /// </summary>
    public class ModelEndpointsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public ModelEndpointsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List model endpoints.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of ModelEndpoint items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<ModelEndpoint>> ListAsync(ModelEndpointListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<ModelEndpoint>>(HttpMethod.Get, "/v1.0/api/model-endpoints", (options ?? new ModelEndpointListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a model endpoint.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ModelEndpoint.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ModelEndpoint> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<ModelEndpoint>(HttpMethod.Get, "/v1.0/api/model-endpoints/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Create a model endpoint.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ModelEndpoint.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ModelEndpoint> CreateAsync(ModelEndpointUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<ModelEndpoint>(HttpMethod.Post, "/v1.0/api/model-endpoints", null, request, token);
        }

        /// <summary>
        /// Update a model endpoint.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ModelEndpoint.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ModelEndpoint> UpdateAsync(string id, ModelEndpointUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<ModelEndpoint>(HttpMethod.Put, "/v1.0/api/model-endpoints/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a model endpoint.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/model-endpoints/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Validate connectivity, tool support, and embeddings. Calls the endpoint.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ModelValidationResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ModelValidationResponse> ValidateAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<ModelValidationResponse>(HttpMethod.Post, "/v1.0/api/model-endpoints/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/validate", null, null, token);
        }

        /// <summary>
        /// List models offered by the endpoint (502 UpstreamError when it cannot be reached).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of ModelListItem items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        /// <param name="options">Pagination, sorting, and filters; null uses defaults.</param>
        public Task<EnumerationResult<ModelListItem>> ListModelsAsync(string id, EnumerationQuery? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<ModelListItem>>(HttpMethod.Get, "/v1.0/api/model-endpoints/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/models", (options ?? new EnumerationQuery()).ToQuery(), null, token);
        }

        /// <summary>
        /// Send a sample prompt as a dry run. Calls the endpoint.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Optional prompt and model, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ModelTestResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ModelTestResponse> TestAsync(string id, ModelTestRequest? request = null, CancellationToken token = default)
        {
            return Client.SendAsync<ModelTestResponse>(HttpMethod.Post, "/v1.0/api/model-endpoints/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/test", null, request, token);
        }

        /// <summary>
        /// List task role assignments.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of ModelAssignment items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<ModelAssignment>> ListAssignmentsAsync(ModelAssignmentListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<ModelAssignment>>(HttpMethod.Get, "/v1.0/api/model-assignments", (options ?? new ModelAssignmentListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Assign a task role to an endpoint (creates or updates).
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ModelAssignment.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ModelAssignment> SetAssignmentAsync(ModelAssignmentUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<ModelAssignment>(HttpMethod.Put, "/v1.0/api/model-assignments", null, request, token);
        }

        /// <summary>
        /// Remove a topic assignment override (409 for tenant defaults).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAssignmentAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/model-assignments/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }
    }
}
