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
    /// Feedback on findings, predictions, suggestions, briefings, sources, and learned preferences.
    /// </summary>
    public class FeedbackResource : ResourceBase
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public FeedbackResource(MyMorningNewsClient client) : base(client)
        {
        }

        /// <summary>
        /// Give feedback: an action (relevant, somewhat, not-relevant, accurate, partially, inaccurate, too-early, useful, not-useful, done, snooze, apply, dismiss) or explicit ratings, with an optional reason.
        /// </summary>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Feedback.</returns>
        /// <exception cref="ArgumentNullException">request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Feedback> CreateAsync(FeedbackCreateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Feedback>(HttpMethod.Post, "/v1.0/api/feedback", null, request, token);
        }

        /// <summary>
        /// List feedback (current ratings unless IncludeHistory).
        /// </summary>
        /// <param name="options">Filters and paging, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of Feedback items.</returns>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<EnumerationResult<Feedback>> ListAsync(FeedbackListOptions? options = null, CancellationToken token = default)
        {
            return Client.SendAsync<EnumerationResult<Feedback>>(HttpMethod.Get, "/v1.0/api/feedback", (options ?? new FeedbackListOptions()).ToQuery(), null, token);
        }

        /// <summary>
        /// Edit the reason of the caller's feedback.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="request">Request body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Feedback.</returns>
        /// <exception cref="ArgumentNullException">id or request is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task<Feedback> UpdateAsync(string id, FeedbackUpdateRequest request, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Client.SendAsync<Feedback>(HttpMethod.Put, "/v1.0/api/feedback/" + MyMorningNewsClient.Segment(id, nameof(id)), null, request, token);
        }

        /// <summary>
        /// Retract the caller's feedback (the earlier rating, if any, becomes current).
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">id is null or empty.</exception>
        /// <exception cref="MyMorningNewsException">The server returned an error.</exception>
        public Task DeleteAsync(string id, CancellationToken token = default)
        {
            return Client.SendAsync(HttpMethod.Delete, "/v1.0/api/feedback/" + MyMorningNewsClient.Segment(id, nameof(id)), null, null, token);
        }

        /// <summary>
        /// Give feedback (same as CreateAsync).
        /// </summary>
        /// <param name="request">Feedback.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Saved feedback.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="MyMorningNewsException">The request failed (400 ValidationFailed when the rating does not fit the item).</exception>
        public Task<Feedback> SubmitAsync(FeedbackCreateRequest request, CancellationToken token = default)
        {
            return CreateAsync(request, token);
        }
    }
}
