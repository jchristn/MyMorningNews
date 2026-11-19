namespace MyMorningNews.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Data access methods for Tenant records.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface ITenantMethods
    {
        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<Tenant> CreateAsync(Tenant obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<Tenant?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<Tenant> UpdateAsync(Tenant obj, CancellationToken token = default);

        /// <summary>
        /// Delete a record. Dependent rows are removed by cascading foreign keys.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a record was deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Check whether a record exists.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the record exists.</returns>
        Task<bool> ExistsAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate records with paging, sorting, and filters.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>One page of results.</returns>
        Task<EnumerationResult<Tenant>> EnumerateAsync(TenantFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(TenantFilter filter, CancellationToken token = default);
    }
}
