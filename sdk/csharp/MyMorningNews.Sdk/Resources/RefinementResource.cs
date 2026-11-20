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
    /// Topic refinement (wizard) sessions. Generate calls (dimensions, questions, context, sources) call the model.
    /// </summary>
    public class RefinementResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public RefinementResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// Start the wizard for a new topic. Creates the topic in Draft status.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RefinementStateResponse.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RefinementStateResponse> StartAsync(RefinementStartRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<RefinementStateResponse>(HttpMethod.Post, "/v1.0/api/refinement-sessions", null, request, token);
        }

        /// <summary>
        /// List refinement sessions.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of RefinementSession items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<RefinementSession>> ListAsync(RefinementSessionListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<RefinementSession>>(HttpMethod.Get, "/v1.0/api/refinement-sessions", (options ?? new RefinementSessionListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read the session state.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RefinementStateResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RefinementStateResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<RefinementStateResponse>(HttpMethod.Get, "/v1.0/api/refinement-sessions/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Propose dimensions (keeps locked and the reader's own). Calls the model.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Optional guidance, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RefinementStateResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RefinementStateResponse> GenerateDimensionsAsync(string id, RefinementGenerateRequest? request = null, CancellationToken token = default)
        {
            return Client.SendAsync<RefinementStateResponse>(HttpMethod.Post, "/v1.0/api/refinement-sessions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/dimensions", null, request, token);
        }

        /// <summary>
        /// Ask the next round of questions. Calls the model.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Optional guidance, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RefinementStateResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RefinementStateResponse> GenerateQuestionsAsync(string id, RefinementGenerateRequest? request = null, CancellationToken token = default)
        {
            return Client.SendAsync<RefinementStateResponse>(HttpMethod.Post, "/v1.0/api/refinement-sessions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/questions", null, request, token);
        }

        /// <summary>
        /// Answer or skip a question.
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
            return Client.SendAsync<RefinementQuestionResponse>(HttpMethod.Put, "/v1.0/api/refinement-sessions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/questions/" + MyMorningNewsClient.Segment(questionId, nameof(questionId)), null, request, token);
        }

        /// <summary>
        /// Draft the brief and constraints. Calls the model.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RefinementStateResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RefinementStateResponse> GenerateContextAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<RefinementStateResponse>(HttpMethod.Post, "/v1.0/api/refinement-sessions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/context", null, null, token);
        }

        /// <summary>
        /// Suggest feeds and seed pages, each checked with a live fetch. Calls the model.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of SourceCheck items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<SourceCheck>> SuggestSourcesAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<List<SourceCheck>>(HttpMethod.Post, "/v1.0/api/refinement-sessions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/sources", null, null, token);
        }

        /// <summary>
        /// Add accepted feeds and seed pages to the topic.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RefinementStateResponse.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RefinementStateResponse> AcceptSourcesAsync(string id, SourceAcceptRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<RefinementStateResponse>(HttpMethod.Post, "/v1.0/api/refinement-sessions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/sources/accept", null, request, token);
        }

        /// <summary>
        /// Apply the topic (Draft becomes Active).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RefinementStateResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RefinementStateResponse> CompleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<RefinementStateResponse>(HttpMethod.Post, "/v1.0/api/refinement-sessions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/complete", null, null, token);
        }

        /// <summary>
        /// Abandon the session (deletes a new draft topic).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task AbandonAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Post, "/v1.0/api/refinement-sessions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/abandon", null, null, token);
        }
    }
}
