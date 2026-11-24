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
    /// Data access methods for BillingSubscription records. Every method is tenant scoped.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IBillingSubscriptionMethods
    {
        /// <summary>Atomically bind verified state and supersede its previous provider token. Old tokens cannot reactivate. Ownership conflicts throw.</summary>
        Task<BillingSubscription?> SaveReplacementAsync(BillingSubscription subscription, string? replacedProviderId, CancellationToken token = default);
        /// <summary>Find verified Stripe subscriptions of a customer for provider-authenticated reconciliation only.</summary>
        Task<List<BillingSubscription>> StripeCustomerAsync(string environment, string customerId, CancellationToken token = default);
        /// <summary>Find a provider subscription for authenticated callback reconciliation only.</summary>
        Task<BillingSubscription?> ReadExternalAsync(string provider, string environment, string providerId, CancellationToken token = default);
        /// <summary>Read up to 50 subscriptions whose last verification is older than the cutoff, oldest first.</summary>
        Task<List<BillingSubscription>> DueAsync(DateTime cutoffUtc, CancellationToken token = default);
        /// <summary>Persist verified state with immutable account ownership and monotonic reconciliation timestamps. Returns null for an older snapshot; ownership conflicts throw.</summary>
        Task<BillingSubscription?> SaveVerifiedAsync(BillingSubscription subscription, CancellationToken token = default);

        /// <summary>
        /// Create a record. An empty identifier is backfilled.
        /// </summary>
        /// <param name="obj">Record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created record.</returns>
        Task<BillingSubscription> CreateAsync(BillingSubscription obj, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<BillingSubscription?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a record by identifier without tenant scoping. Only for control-plane and internal use.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Record, or null when not found.</returns>
        Task<BillingSubscription?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a record. LastUpdateUtc is set by the implementation.
        /// </summary>
        /// <param name="obj">Record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated record.</returns>
        Task<BillingSubscription> UpdateAsync(BillingSubscription obj, CancellationToken token = default);

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
        Task<EnumerationResult<BillingSubscription>> EnumerateAsync(BillingSubscriptionFilter filter, CancellationToken token = default);

        /// <summary>
        /// Count records matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Count.</returns>
        Task<long> CountAsync(BillingSubscriptionFilter filter, CancellationToken token = default);
    }
}
