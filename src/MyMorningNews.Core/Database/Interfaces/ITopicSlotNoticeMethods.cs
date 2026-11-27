namespace MyMorningNews.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// The last over-limit notice per account (plan section 8.4), so a plan change that leaves too many custom topics
    /// is announced once. Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface ITopicSlotNoticeMethods
    {
        /// <summary>
        /// The recorded notice for an account.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The notice, or null when none is recorded.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty.</exception>
        Task<TopicSlotNotice?> ReadAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// Record a notice unless one is already recorded for the account. Atomic, so two passes at once record (and
        /// notify) only once.
        /// </summary>
        /// <param name="notice">Notice; NotifiedUtc is set to now when null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when this call recorded it; false when the account already had one.</returns>
        /// <exception cref="ArgumentNullException">notice is null, or its tenant or user is empty.</exception>
        Task<bool> TryCreateAsync(TopicSlotNotice notice, CancellationToken token = default);

        /// <summary>
        /// Remove the recorded notice for an account (it is back within its limit).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">Owner.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a notice was removed.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty.</exception>
        Task<bool> DeleteAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// One page of the accounts to check: active users who own a recurring, non-archived topic or have a recorded
        /// notice, in user ID order. NotifiedUtc is set for accounts with a recorded notice.
        /// </summary>
        /// <param name="afterUserId">Continue after this user ID, or null for the first page.</param>
        /// <param name="limit">Page size. Minimum 1, maximum 5000.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Accounts.</returns>
        Task<List<TopicSlotNotice>> ListAccountsAsync(string? afterUserId, int limit, CancellationToken token = default);
    }
}
