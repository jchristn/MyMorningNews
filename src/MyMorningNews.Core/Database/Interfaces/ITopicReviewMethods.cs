namespace MyMorningNews.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Data access methods for keep-or-delete reviews of stale topics.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface ITopicReviewMethods
    {
        /// <summary>
        /// Create a review. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Review.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created review.</returns>
        /// <exception cref="ArgumentNullException">obj is null.</exception>
        /// <exception cref="Npgsql.PostgresException">The topic already has a pending review (unique violation).</exception>
        Task<TopicReview> CreateAsync(TopicReview obj, CancellationToken token = default);

        /// <summary>
        /// Read a review within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Review, or null.</returns>
        /// <exception cref="ArgumentNullException">tenantId or id is null or empty.</exception>
        Task<TopicReview?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Update a review's mutable fields.
        /// </summary>
        /// <param name="obj">Review.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated review.</returns>
        /// <exception cref="ArgumentNullException">obj is null.</exception>
        /// <exception cref="KeyNotFoundException">The review does not exist.</exception>
        Task<TopicReview> UpdateAsync(TopicReview obj, CancellationToken token = default);

        /// <summary>
        /// Enumerate reviews in a tenant.
        /// </summary>
        /// <param name="filter">Filter; TenantId is required.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Page.</returns>
        /// <exception cref="ArgumentNullException">filter or its TenantId is null.</exception>
        Task<EnumerationResult<TopicReview>> EnumerateAsync(TopicReviewFilter filter, CancellationToken token = default);

        /// <summary>
        /// Pending reviews across tenants, earliest deadline first.
        /// </summary>
        /// <param name="limit">Maximum rows, 1 to 5000.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Reviews.</returns>
        Task<List<TopicReview>> ListPendingAsync(int limit, CancellationToken token = default);

        /// <summary>
        /// Recurring topics that qualify for a new review (see <see cref="Postgresql.Queries.TopicReviewQueries.SelectStaleCandidates"/>).
        /// </summary>
        /// <param name="windowStartUtc">Start of the quiet window (now minus the stale days).</param>
        /// <param name="snoozeCutoffUtc">Reviews answered Kept or Recovered after this time hold off a new one.</param>
        /// <param name="limit">Maximum rows, 1 to 5000.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Candidates, oldest topic first.</returns>
        Task<List<StaleTopicCandidate>> ListStaleCandidatesAsync(DateTime windowStartUtc, DateTime snoozeCutoffUtc, int limit, CancellationToken token = default);

        /// <summary>
        /// Completion time of the newest real run with new findings for a topic.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Time, or null when the topic never had new findings.</returns>
        /// <exception cref="ArgumentNullException">tenantId or topicId is null or empty.</exception>
        Task<DateTime?> ReadLastNewFindingAsync(string tenantId, string topicId, CancellationToken token = default);
    }
}
