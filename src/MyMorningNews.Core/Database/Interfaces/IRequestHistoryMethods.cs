namespace MyMorningNews.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Request history data access methods. A null tenant identifier widens scope to all tenants (administrators only).
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IRequestHistoryMethods
    {
        /// <summary>
        /// Insert a captured request.
        /// </summary>
        /// <param name="entry">Entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task CreateAsync(RequestHistoryEntry entry, CancellationToken token = default);

        /// <summary>
        /// Read one entry including headers and bodies.
        /// </summary>
        /// <param name="tenantId">Tenant scope, or null for all tenants.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entry, or null when not found.</returns>
        Task<RequestHistoryEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Page through entries. Bodies and headers are omitted.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Page.</returns>
        Task<EnumerationResult<RequestHistoryEntry>> EnumerateAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>
        /// Time-bucketed counts and averages.
        /// </summary>
        /// <param name="filter">Filter; FromUtc and ToUtc are required.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Summary.</returns>
        Task<RequestHistorySummary> SummarizeAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>
        /// Delete one entry.
        /// </summary>
        /// <param name="tenantId">Tenant scope, or null for all tenants.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when deleted.</returns>
        Task<bool> DeleteAsync(string? tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete all entries matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows deleted.</returns>
        Task<int> DeleteManyAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>
        /// Delete entries older than a cutoff.
        /// </summary>
        /// <param name="olderThanUtc">Cutoff (UTC).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows deleted.</returns>
        Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken token = default);
    }
}
