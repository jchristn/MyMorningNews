namespace MyMorningNews.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Analytics;

    /// <summary>
    /// Daily activity records and platform-wide statistics.
    /// Thread safety: implementations are safe for concurrent use.
    /// </summary>
    public interface IPlatformStatsMethods
    {
        /// <summary>
        /// Record that a user was active at a time (one row per user per UTC day).
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="utcNow">Time of the activity, UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">tenantId or userId is null or empty.</exception>
        Task TouchAsync(string tenantId, string userId, DateTime utcNow, CancellationToken token = default);

        /// <summary>
        /// Delete activity days before a date.
        /// </summary>
        /// <param name="beforeUtc">Cutoff.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows deleted.</returns>
        Task<int> PruneActivityAsync(DateTime beforeUtc, CancellationToken token = default);

        /// <summary>
        /// Platform counts for a period.
        /// </summary>
        /// <param name="fromUtc">Period start (inclusive).</param>
        /// <param name="toUtc">Period end (exclusive).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Statistics.</returns>
        /// <exception cref="ArgumentException">toUtc is not after fromUtc.</exception>
        Task<PlatformDailyStats> ReadAsync(DateTime fromUtc, DateTime toUtc, CancellationToken token = default);
    }
}
