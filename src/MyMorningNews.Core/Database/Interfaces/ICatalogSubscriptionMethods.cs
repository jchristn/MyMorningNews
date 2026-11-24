namespace MyMorningNews.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Data access methods for CatalogSubscription records. Every method is tenant scoped.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface ICatalogSubscriptionMethods
    {
        /// <summary>Reconcile eligible retained slots under the account subscription lock. Oldest eligible subscriptions are retained; previously suspended subscriptions restore on upgrade.</summary>
        Task ReconcileAsync(string tenantId, string userId, long? maximum, bool freeOnly, CancellationToken token = default);
        /// <summary>Page the caller's shared issues across eligible subscriptions using a content-only projection.</summary>
        Task<EnumerationResult<CatalogIssueResponse>> IssuesAsync(string tenantId, string userId, bool freeOnly, EnumerationFilter filter, CancellationToken token = default, bool audioOnly = false);

        /// <summary>Ready shared audio for reconciled subscriptions. Newest rendition per issue, maximum 1 to 500; optional ID authorizes one rendition.</summary>
        Task<List<MediaAsset>> FeedMediaAsync(string tenantId, string userId, bool freeOnly, int maximum, string? mediaId = null, CancellationToken token = default);

        /// <summary>Atomically subscribe or restore a subscription within the account limit. Null means unavailable or full. Null maximum is unlimited.</summary>
        Task<CatalogSubscription?> TrySubscribeAsync(CatalogSubscription subscription, long? maximum, bool freeOnly, CancellationToken token = default);

        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<CatalogSubscription> CreateAsync(CatalogSubscription obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<CatalogSubscription?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier without tenant scoping. Only for control-plane and internal use.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<CatalogSubscription?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<CatalogSubscription> UpdateAsync(CatalogSubscription obj, CancellationToken token = default);

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
        Task<EnumerationResult<CatalogSubscription>> EnumerateAsync(CatalogSubscriptionFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(CatalogSubscriptionFilter filter, CancellationToken token = default);
    }
}
