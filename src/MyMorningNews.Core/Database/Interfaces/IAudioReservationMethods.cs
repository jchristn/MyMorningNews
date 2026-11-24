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
    /// Data access methods for AudioReservation records. Every method is tenant scoped.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IAudioReservationMethods
    {
        /// <summary>Exact successful usage and outstanding reservations for a UTC day.</summary>
        Task<AudioUsageSummary> UsageAsync(string tenantId, string userId, DateTime periodStartUtc, CancellationToken token = default);

        /// <summary>Release expired orphaned or terminal reservations without touching live jobs or committed usage.</summary>
        Task RecoverAsync(DateTime nowUtc, CancellationToken token = default);

        /// <summary>Reserve daily audio capacity under an account lock. Same operation is idempotent; null means full. Null maximum is unlimited.</summary>
        Task<AudioReservation?> TryReserveAsync(AudioReservation reservation, long? maximumSeconds, CancellationToken token = default);

        /// <summary>Atomically queue one episode and reserve capacity. Reuses existing live media on duplicate requests, including when the allowance is full. Null means capacity is unavailable.</summary>
        Task<MediaAsset?> TryQueueAsync(MediaAsset asset, AudioReservation reservation, long? maximumSeconds, bool force, CancellationToken token = default);

        /// <summary>Settle a transient voice sample into the durable ledger. Rejects operations belonging to stored media; null means capacity or reservation is unavailable.</summary>
        Task<AudioReservation?> TryCommitSampleAsync(string tenantId, string userId, string operationId, long milliseconds, long? maximumSeconds, CancellationToken token = default);

        /// <summary>Atomically settle exact duration and publish the ready media row. Null means unavailable capacity or no active reservation.</summary>
        Task<AudioReservation?> TryCommitAsync(MediaAsset asset, long milliseconds, long? maximumSeconds, CancellationToken token = default);

        /// <summary>Release uncommitted capacity. Committed consumption is immutable and survives media deletion.</summary>
        Task ReleaseAsync(string tenantId, string userId, string operationId, CancellationToken token = default);

        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<AudioReservation> CreateAsync(AudioReservation obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<AudioReservation?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier without tenant scoping. Only for control-plane and internal use.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<AudioReservation?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<AudioReservation> UpdateAsync(AudioReservation obj, CancellationToken token = default);

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
        Task<EnumerationResult<AudioReservation>> EnumerateAsync(AudioReservationFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(AudioReservationFilter filter, CancellationToken token = default);
    }
}
