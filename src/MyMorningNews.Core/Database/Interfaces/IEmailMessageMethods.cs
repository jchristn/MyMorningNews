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
    /// Data access methods for EmailMessage records. Every method is tenant scoped.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IEmailMessageMethods
    {
        /// <summary>Find an exact scoped outbox idempotency key, or null.</summary>
        Task<EmailMessage?> ReadByKeyAsync(string tenantId, string key, CancellationToken token = default);
        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<EmailMessage> CreateAsync(EmailMessage obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<EmailMessage?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier without tenant scoping. Only for control-plane and internal use.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<EmailMessage?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<EmailMessage> UpdateAsync(EmailMessage obj, CancellationToken token = default);

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
        Task<EnumerationResult<EmailMessage>> EnumerateAsync(EmailMessageFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(EmailMessageFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count messages matching the filter, grouped by creation day, template, and status, with the mean time to send.
        /// </summary>
        /// <param name="filter">Filter (tenant required).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Groups ordered by day, template, and status.</returns>
        Task<List<EmailAggregateRow>> AggregateAsync(EmailMessageFilter filter, CancellationToken token = default);

        /// <summary>
        /// Claim queued emails that are due (oldest first), marking them Sending with a lock expiry and counting the attempt.
        /// </summary>
        /// <param name="nowUtc">Now.</param>
        /// <param name="lockedUntilUtc">Claim expiry.</param>
        /// <param name="limit">Maximum claims (1 to 200).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Claimed emails.</returns>
        Task<List<EmailMessage>> ClaimDueAsync(DateTime nowUtc, DateTime lockedUntilUtc, int limit, CancellationToken token = default);

        /// <summary>
        /// Put emails whose Sending claim expired back in the queue.
        /// </summary>
        /// <param name="nowUtc">Now.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows changed.</returns>
        Task<int> RecoverAbandonedAsync(DateTime nowUtc, CancellationToken token = default);

        /// <summary>
        /// Delete emails (and their action tokens) created before a cutoff that are no longer queued or sending.
        /// </summary>
        /// <param name="beforeUtc">Cutoff.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Emails deleted.</returns>
        Task<int> PruneAsync(DateTime beforeUtc, CancellationToken token = default);
    }
}
