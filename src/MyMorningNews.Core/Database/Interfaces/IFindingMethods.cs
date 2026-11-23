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
    /// Data access methods for Finding records. Every method is tenant scoped.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IFindingMethods
    {
        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<Finding> CreateAsync(Finding obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<Finding?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier without tenant scoping. Only for control-plane and internal use.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<Finding?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<Finding> UpdateAsync(Finding obj, CancellationToken token = default);

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
        Task<EnumerationResult<Finding>> EnumerateAsync(FindingFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(FindingFilter filter, CancellationToken token = default);

        /// <summary>
        /// Read several records by identifier in one query.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="ids">Identifiers. Null or empty returns an empty list.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The records found, in no particular order; unknown identifiers are skipped.</returns>
        Task<List<Finding>> ReadManyAsync(string tenantId, IEnumerable<string>? ids, CancellationToken token = default);

        /// <summary>
        /// Earlier findings of a topic that a new candidate can repeat: not from a dry run, not themselves duplicates,
        /// created at or after <paramref name="sinceUtc"/>, and not from <paramref name="excludeRunId"/>. Newest first.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="topicId">Topic identifier.</param>
        /// <param name="sinceUtc">Oldest creation time included (UTC).</param>
        /// <param name="excludeRunId">Run whose own findings are left out, or null.</param>
        /// <param name="limit">Most records returned. Minimum 1, maximum 50000.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The newest matching findings, up to the limit.</returns>
        Task<List<Finding>> DedupeHistoryAsync(string tenantId, string topicId, DateTime sinceUtc, string? excludeRunId, int limit, CancellationToken token = default);

        /// <summary>
        /// Earlier findings of a topic whose title equals one of the given titles, ignoring case and surrounding white
        /// space, under the same rules as <see cref="DedupeHistoryAsync"/>. An indexed lookup over the whole window, with
        /// no row limit, so an exact repeat is found however many findings the topic has.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="topicId">Topic identifier.</param>
        /// <param name="titles">Titles to look for. Null or empty returns an empty list.</param>
        /// <param name="sinceUtc">Oldest creation time included (UTC).</param>
        /// <param name="excludeRunId">Run whose own findings are left out, or null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matching findings, newest first.</returns>
        Task<List<Finding>> DedupeTitleMatchesAsync(string tenantId, string topicId, IEnumerable<string>? titles, DateTime sinceUtc, string? excludeRunId, CancellationToken token = default);
    }
}
