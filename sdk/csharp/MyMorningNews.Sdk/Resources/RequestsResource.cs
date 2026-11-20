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
    /// One-time research requests. Creating and replanning call the model.
    /// </summary>
    public class RequestsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public RequestsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// Create and plan a one-time request; returns at most three clarifying questions. Calls the model.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ResearchRequestResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ResearchRequestResponse> CreateAsync(ResearchRequestCreateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<ResearchRequestResponse>(HttpMethod.Post, "/v1.0/api/requests", null, request, token);
        }

        /// <summary>
        /// List one-time requests.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of ResearchRequest items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<ResearchRequest>> ListAsync(ResearchRequestListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<ResearchRequest>>(HttpMethod.Get, "/v1.0/api/requests", (options ?? new ResearchRequestListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a request with its plan and questions.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ResearchRequestResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ResearchRequestResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<ResearchRequestResponse>(HttpMethod.Get, "/v1.0/api/requests/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Answer a clarifying question.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="questionId">Question identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RefinementQuestionResponse.</returns>
        /// <exception cref="ArgumentNullException">id or questionId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RefinementQuestionResponse> AnswerAsync(string id, string questionId, QuestionAnswerRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<RefinementQuestionResponse>(HttpMethod.Put, "/v1.0/api/requests/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/questions/" + MyMorningNewsClient.Segment(questionId, nameof(questionId)), null, request, token);
        }

        /// <summary>
        /// Plan again with the answers given. Calls the model.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ResearchRequestResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ResearchRequestResponse> ReplanAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<ResearchRequestResponse>(HttpMethod.Post, "/v1.0/api/requests/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/replan", null, null, token);
        }

        /// <summary>
        /// Confirm: run once now, or at RunAtUtc. This starts a real run.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Optional run time, or null to run now.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ResearchRequestResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ResearchRequestResponse> ConfirmAsync(string id, ResearchRequestConfirmRequest? request = null, CancellationToken token = default)
        {
            return Client.SendAsync<ResearchRequestResponse>(HttpMethod.Post, "/v1.0/api/requests/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/confirm", null, request, token);
        }

        /// <summary>
        /// Cancel a request that has not started.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The ResearchRequestResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<ResearchRequestResponse> CancelAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<ResearchRequestResponse>(HttpMethod.Post, "/v1.0/api/requests/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/cancel", null, null, token);
        }

        /// <summary>
        /// Make the request a recurring topic.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Topic.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Topic> ConvertAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Topic>(HttpMethod.Post, "/v1.0/api/requests/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/convert", null, null, token);
        }

        /// <summary>
        /// Delete a request.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/requests/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }
    }
}
