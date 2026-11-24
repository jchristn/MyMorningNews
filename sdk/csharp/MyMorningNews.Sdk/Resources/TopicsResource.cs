namespace MyMorningNews.Sdk.Resources
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Topics: CRUD, lifecycle, run now, learning, dimensions, constraints, recipients, and search providers.
    /// </summary>
    public class TopicsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public TopicsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List topics. Members see their own; administrators see all unless filtered.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Topic items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Topic>> ListAsync(TopicListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Topic>>(HttpMethod.Get, "/v1.0/api/topics", (options ?? new TopicListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a topic with its dimensions, constraints, and recipients.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TopicDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TopicDetailResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<TopicDetailResponse>(HttpMethod.Get, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Create a topic without the wizard (Draft status).
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Topic.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Topic> CreateAsync(TopicUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Topic>(HttpMethod.Post, "/v1.0/api/topics", null, request, token);
        }

        /// <summary>
        /// Update a topic. Null fields are left alone.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Topic.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Topic> UpdateAsync(string id, TopicUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Topic>(HttpMethod.Put, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Delete a topic and everything under it.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Pause a topic's schedules.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Topic.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Topic> PauseAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Topic>(HttpMethod.Post, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/pause", null, null, token);
        }

        /// <summary>
        /// Resume a paused or archived topic (409 for a draft without a brief).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Topic.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Topic> ResumeAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Topic>(HttpMethod.Post, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/resume", null, null, token);
        }

        /// <summary>
        /// Archive a topic.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Topic.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Topic> ArchiveAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<Topic>(HttpMethod.Post, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/archive", null, null, token);
        }

        /// <summary>
        /// Duplicate a topic as a draft, with its dimensions and constraints.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TopicDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TopicDetailResponse> DuplicateAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<TopicDetailResponse>(HttpMethod.Post, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/duplicate", null, null, token);
        }

        /// <summary>
        /// Start a refinement session for an existing topic.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RefinementStateResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RefinementStateResponse> RefineAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<RefinementStateResponse>(HttpMethod.Post, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/refine", null, null, token);
        }

        /// <summary>
        /// Queue a run now (HTTP 202). A dry run sends no email and is not counted in history. A real run calls the configured model and search providers.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="dryRun">Queue a dry run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Run.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Run> RunAsync(string id, bool dryRun = false, CancellationToken token = default)
        {
            return Client.SendAsync<Run>(HttpMethod.Post, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/run", Query("dryRun", dryRun ? (object)true : null), null, token);
        }

        /// <summary>
        /// Run a learning pass for the topic now (calls the model).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The LearnResult.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<LearnResult> LearnAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<LearnResult>(HttpMethod.Post, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/learn", null, null, token);
        }

        /// <summary>
        /// Suggested ranking weights from the reader's ratings.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The RankingRecommendation.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<RankingRecommendation> GetRankingRecommendationAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<RankingRecommendation>(HttpMethod.Get, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/ranking-recommendation", null, null, token);
        }

        /// <summary>
        /// List a topic's dimensions.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of TopicDimension items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        /// <param name="options">Pagination, sorting, and filters; null uses defaults.</param>
        public Task<EnumerationResult<TopicDimension>> ListDimensionsAsync(string id, EnumerationQuery? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<TopicDimension>>(HttpMethod.Get, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/dimensions", (options ?? new EnumerationQuery()).ToQuery(), null, token);
        }

        /// <summary>
        /// Add a dimension.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TopicDimension.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TopicDimension> CreateDimensionAsync(string id, DimensionUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<TopicDimension>(HttpMethod.Post, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/dimensions", null, request, token);
        }

        /// <summary>
        /// Update a dimension (it becomes the reader's own and survives regeneration).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="dimensionId">Dimension identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TopicDimension.</returns>
        /// <exception cref="ArgumentNullException">id or dimensionId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TopicDimension> UpdateDimensionAsync(string id, string dimensionId, DimensionUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<TopicDimension>(HttpMethod.Put, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/dimensions/" + MyMorningNewsClient.Segment(dimensionId, nameof(dimensionId)), null, request, token);
        }

        /// <summary>
        /// Delete a dimension.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="dimensionId">Dimension identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id or dimensionId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteDimensionAsync(string id, string dimensionId, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/dimensions/" + MyMorningNewsClient.Segment(dimensionId, nameof(dimensionId)), null, null, token);
        }

        /// <summary>
        /// List a topic's constraints.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="kind">Only this kind, or null for all.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of TopicConstraint items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        /// <param name="options">Pagination, sorting, and filters; null uses defaults.</param>
        public Task<EnumerationResult<TopicConstraint>> ListConstraintsAsync(string id, ConstraintKindEnum? kind = null, EnumerationQuery? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<TopicConstraint>>(HttpMethod.Get, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/constraints", MergeQuery(options, Query("kind", kind)), null, token);
        }

        /// <summary>
        /// Add a constraint.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TopicConstraint.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TopicConstraint> CreateConstraintAsync(string id, ConstraintUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<TopicConstraint>(HttpMethod.Post, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/constraints", null, request, token);
        }

        /// <summary>
        /// Update a constraint.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="constraintId">Constraint identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TopicConstraint.</returns>
        /// <exception cref="ArgumentNullException">id or constraintId or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TopicConstraint> UpdateConstraintAsync(string id, string constraintId, ConstraintUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<TopicConstraint>(HttpMethod.Put, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/constraints/" + MyMorningNewsClient.Segment(constraintId, nameof(constraintId)), null, request, token);
        }

        /// <summary>
        /// Delete a constraint.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="constraintId">Constraint identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id or constraintId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteConstraintAsync(string id, string constraintId, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/constraints/" + MyMorningNewsClient.Segment(constraintId, nameof(constraintId)), null, null, token);
        }

        /// <summary>
        /// List a topic's email recipients.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of TopicRecipient items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        /// <param name="options">Pagination, sorting, and filters; null uses defaults.</param>
        public Task<EnumerationResult<TopicRecipient>> ListRecipientsAsync(string id, EnumerationQuery? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<TopicRecipient>>(HttpMethod.Get, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/recipients", (options ?? new EnumerationQuery()).ToQuery(), null, token);
        }

        /// <summary>
        /// Add an email recipient (an existing address is updated instead).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The TopicRecipient.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<TopicRecipient> AddRecipientAsync(string id, RecipientUpsertRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<TopicRecipient>(HttpMethod.Post, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/recipients", null, request, token);
        }

        /// <summary>
        /// Remove an email recipient.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="recipientId">Recipient identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id or recipientId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteRecipientAsync(string id, string recipientId, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/recipients/" + MyMorningNewsClient.Segment(recipientId, nameof(recipientId)), null, null, token);
        }

        /// <summary>
        /// Search providers the topic uses.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of SearchProvider items.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        /// <param name="options">Pagination, sorting, and filters; null uses defaults.</param>
        public Task<EnumerationResult<SearchProvider>> GetSearchProvidersAsync(string id, EnumerationQuery? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<SearchProvider>>(HttpMethod.Get, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/search-providers", (options ?? new EnumerationQuery()).ToQuery(), null, token);
        }

        /// <summary>
        /// Choose a topic's search providers (an empty list uses the tenant defaults).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="providers">Providers in use.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of SearchProvider items.</returns>
        /// <exception cref="ArgumentNullException">id or providers is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<List<SearchProvider>> SetSearchProvidersAsync(string id, List<TopicSearchProviderRequest> providers, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(providers);
            return Client.SendAsync<List<SearchProvider>>(HttpMethod.Put, "/v1.0/api/topics/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/search-providers", null, providers, token);
        }
    }
}
