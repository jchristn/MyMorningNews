namespace MyMorningNews.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Data access methods for PromoCode records.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IPromoCodeMethods
    {
        /// <summary>Atomically enforce code and per-account limits and create either a complimentary assignment or a paid offer snapshot. Returns null if no longer eligible.</summary>
        Task<PromoRedemption?> RedeemAsync(string tenantId, string userId, string codeId, PromoChannelEnum channel, bool newAccount, CancellationToken token = default);

        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<PromoCode> CreateAsync(PromoCode obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<PromoCode?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<PromoCode> UpdateAsync(PromoCode obj, CancellationToken token = default);

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
        Task<EnumerationResult<PromoCode>> EnumerateAsync(PromoCodeFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(PromoCodeFilter filter, CancellationToken token = default);

        /// <summary>
        /// Find a code (case-insensitive).
        /// </summary>
        /// <param name="code">Code.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Code, or null.</returns>
        Task<PromoCode?> ReadByCodeAsync(string code, CancellationToken token = default);

        /// <summary>
        /// Count one redemption if the code is enabled, inside its dates, and under its limit, atomically.
        /// </summary>
        /// <param name="id">Code ID.</param>
        /// <param name="nowUtc">Now.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated code, or null when it cannot be redeemed.</returns>
        Task<PromoCode?> TryClaimAsync(string id, DateTime nowUtc, CancellationToken token = default);

        /// <summary>
        /// Give back a claimed redemption (when the rest of the redemption failed).
        /// </summary>
        /// <param name="id">Code ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReleaseAsync(string id, CancellationToken token = default);
    }
}
