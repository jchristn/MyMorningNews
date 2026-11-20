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
    /// Predictions and prediction reviews.
    /// </summary>
    public class PredictionsResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public PredictionsResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// List predictions.
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Prediction items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Prediction>> ListAsync(PredictionListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Prediction>>(HttpMethod.Get, "/v1.0/api/predictions", (options ?? new PredictionListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Read a prediction with its reviews.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The PredictionDetailResponse.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<PredictionDetailResponse> GetAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync<PredictionDetailResponse>(HttpMethod.Get, "/v1.0/api/predictions/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Resolve a prediction (Accurate, PartiallyAccurate, Inaccurate, Unresolvable, or Withdrawn).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Prediction.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Prediction> ResolveAsync(string id, PredictionResolveRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Prediction>(HttpMethod.Post, "/v1.0/api/predictions/" + MyMorningNewsClient.Segment(id, nameof(id)) + "/resolve", null, request, token);
        }

        /// <summary>
        /// Accept a proposed resolution.
        /// </summary>
        /// <param name="reviewId">Review identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Prediction.</returns>
        /// <exception cref="ArgumentNullException">reviewId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Prediction> AcceptReviewAsync(string reviewId, CancellationToken token = default)
        {
            return Client.SendAsync<Prediction>(HttpMethod.Post, "/v1.0/api/prediction-reviews/" + MyMorningNewsClient.Segment(reviewId, nameof(reviewId)) + "/accept", null, null, token);
        }

        /// <summary>
        /// Reject a proposed resolution.
        /// </summary>
        /// <param name="reviewId">Review identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Prediction.</returns>
        /// <exception cref="ArgumentNullException">reviewId is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Prediction> RejectReviewAsync(string reviewId, CancellationToken token = default)
        {
            return Client.SendAsync<Prediction>(HttpMethod.Post, "/v1.0/api/prediction-reviews/" + MyMorningNewsClient.Segment(reviewId, nameof(reviewId)) + "/reject", null, null, token);
        }
    }
}
