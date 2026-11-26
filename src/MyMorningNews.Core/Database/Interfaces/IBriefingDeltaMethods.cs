namespace MyMorningNews.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// Data access methods for BriefingDelta records. Every method is tenant scoped.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IBriefingDeltaMethods
    {
        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        /// <exception cref="System.ArgumentNullException">obj is null.</exception>
        Task<BriefingDelta> CreateAsync(BriefingDelta obj, CancellationToken token = default);

        /// <summary>
        /// Enumerate records in display order (sortOrder, then id) with paging. TenantId on the filter is required.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>One page of results.</returns>
        /// <exception cref="System.ArgumentNullException">filter or filter.TenantId is null.</exception>
        Task<EnumerationResult<BriefingDelta>> EnumerateAsync(BriefingDeltaFilter filter, CancellationToken token = default);

        /// <summary>
        /// Remove every change of one briefing, before it is recomputed.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="briefingId">Briefing.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows removed.</returns>
        /// <exception cref="System.ArgumentNullException">An argument is null or empty.</exception>
        Task<int> DeleteByBriefingAsync(string tenantId, string briefingId, CancellationToken token = default);
    }
}
