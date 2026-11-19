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
    /// Data access methods for Briefing records. Every method is tenant scoped.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IBriefingMethods
    {
        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<Briefing> CreateAsync(Briefing obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<Briefing?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier without tenant scoping. Only for control-plane and internal use.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<Briefing?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<Briefing> UpdateAsync(Briefing obj, CancellationToken token = default);

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
        Task<EnumerationResult<Briefing>> EnumerateAsync(BriefingFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(BriefingFilter filter, CancellationToken token = default);

        /// <summary>
        /// Claim queued briefings whose compose time has come, marking them Composing with a lock expiry.
        /// Concurrent workers never claim the same briefing (FOR UPDATE SKIP LOCKED).
        /// </summary>
        /// <param name="nowUtc">Now.</param>
        /// <param name="lockedUntilUtc">Claim expiry.</param>
        /// <param name="limit">Maximum claims (1 to 50).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Claimed briefings.</returns>
        Task<List<Briefing>> ClaimQueuedAsync(DateTime nowUtc, DateTime lockedUntilUtc, int limit, CancellationToken token = default);

        /// <summary>
        /// Fail briefings whose composition claim expired (the worker stopped).
        /// </summary>
        /// <param name="nowUtc">Now.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows failed.</returns>
        Task<int> FailAbandonedAsync(DateTime nowUtc, CancellationToken token = default);

        /// <summary>
        /// Whether an edition has a briefing queued or composing.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="editionId">Edition.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when one is active.</returns>
        Task<bool> HasActiveForEditionAsync(string tenantId, string editionId, CancellationToken token = default);
    }
}
