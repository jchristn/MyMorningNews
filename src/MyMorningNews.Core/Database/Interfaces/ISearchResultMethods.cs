namespace MyMorningNews.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Data access methods for SearchResult records. Every method is tenant scoped.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface ISearchResultMethods
    {
        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<SearchResult> CreateAsync(SearchResult obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<SearchResult?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier without tenant scoping. Only for control-plane and internal use.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<SearchResult?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<SearchResult> UpdateAsync(SearchResult obj, CancellationToken token = default);

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
        Task<EnumerationResult<SearchResult>> EnumerateAsync(SearchResultFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(SearchResultFilter filter, CancellationToken token = default);

        /// <summary>
        /// Of the given canonical URL hashes, those a finished run of the topic already fetched: a result with the
        /// Fetched disposition whose run is Completed or PartiallySucceeded and not a dry run. Pages fetched by a failed,
        /// cancelled, or dry run do not count. The whole history of the topic is checked; there is no row limit.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="topicId">Topic identifier.</param>
        /// <param name="canonicalUrlHashes">Hashes to check. Null or empty returns an empty set.</param>
        /// <param name="excludeRunId">Run whose own results are ignored (the run asking), or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The subset of the given hashes that count as seen.</returns>
        Task<HashSet<string>> SeenUrlHashesAsync(string tenantId, string topicId, IEnumerable<string>? canonicalUrlHashes, string? excludeRunId, CancellationToken token = default);
    }
}
