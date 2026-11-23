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
    /// Data access methods for BriefingShare records. Every method is tenant scoped unless it says otherwise.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IBriefingShareMethods
    {
        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<BriefingShare> CreateAsync(BriefingShare obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<BriefingShare?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier without tenant scoping. Only for control-plane and internal use.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<BriefingShare?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation. The view counter is not written; use RecordViewAsync.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        /// <exception cref="KeyNotFoundException">The record does not exist.</exception>
        Task<BriefingShare> UpdateAsync(BriefingShare obj, CancellationToken token = default);

        /// <summary>
        /// Delete a record.
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
        Task<EnumerationResult<BriefingShare>> EnumerateAsync(BriefingShareFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(BriefingShareFilter filter, CancellationToken token = default);

        /// <summary>
        /// Find a share by the hash of its link secret across tenants (public requests carry no tenant).
        /// </summary>
        /// <param name="tokenHash">SHA-256 of the secret, lowercase hex.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Share, or null.</returns>
        Task<BriefingShare?> ReadByHashAsync(string tokenHash, CancellationToken token = default);

        /// <summary>
        /// Add one view to a share and stamp the time, atomically. Not tenant scoped.
        /// </summary>
        /// <param name="id">Share identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the share exists.</returns>
        Task<bool> RecordViewAsync(string id, CancellationToken token = default);
    }
}
