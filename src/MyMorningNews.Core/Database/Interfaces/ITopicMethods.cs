namespace MyMorningNews.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Data access methods for Topic records. Every method is tenant scoped.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface ITopicMethods
    {
        /// <summary>Whether a custom topic is within the oldest retained slots after a downgrade. Archived topics do not occupy slots.</summary>
        Task<bool> WithinSlotsAsync(Topic topic, long maximum, CancellationToken token = default);

        /// <summary>
        /// Save the owner's kept order for a smaller plan in one statement: each listed recurring topic gets its 1-based
        /// position, every other recurring topic of the owner is cleared. Callers validate the list first.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner.</param>
        /// <param name="keepTopicIds">Topic IDs in keep order. Empty clears the selection.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows changed.</returns>
        /// <exception cref="System.ArgumentNullException">An argument is null or empty.</exception>
        Task<int> SetSlotPrioritiesAsync(string tenantId, string userId, List<string> keepTopicIds, CancellationToken token = default);

        /// <summary>
        /// The owner's recurring, non-archived topics in slot order: kept order first, then oldest first.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Topics in slot order.</returns>
        /// <exception cref="System.ArgumentNullException">An argument is null or empty.</exception>
        Task<List<Topic>> SlotOrderAsync(string tenantId, string userId, CancellationToken token = default);
        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<Topic> CreateAsync(Topic obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<Topic?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier without tenant scoping. Only for control-plane and internal use.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<Topic?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<Topic> UpdateAsync(Topic obj, CancellationToken token = default);

        /// <summary>
        /// Delete a record. Dependent rows are removed by cascading foreign keys.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a record was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete a record by identifier without tenant scoping. Only for control-plane and internal use.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a record was deleted.</returns>
        Task<bool> DeleteByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Check whether a record exists.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the record exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate records with paging, sorting, and filters. TenantId on the filter is required.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>One page of results.</returns>
        Task<EnumerationResult<Topic>> EnumerateAsync(TopicFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(TopicFilter filter, CancellationToken token = default);

        /// <summary>
        /// Delete a topic and everything that belongs to it: schedules, runs (with their stages, queries, and results),
        /// findings, predictions, suggestions, one-time requests, and embeddings. Dimensions, constraints, recipients,
        /// refinement sessions, and provider choices go with the topic row. Feedback, reason tags, learned preferences,
        /// learning checkpoints, email action tokens and suppressions, topic prompt overrides, and topic model assignments are
        /// removed. Sent email records stay in the outbox with the topic cleared. Model call records are kept for cost accounting.
        /// Stored file rows are removed; callers delete file content from the store first (BriefingService.DeleteTopicFilesAsync).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the topic existed.</returns>
        Task<bool> DeleteCascadeAsync(string tenantId, string topicId, CancellationToken token = default);
    }
}
