namespace MyMorningNews.Core.Scheduling
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Handles due edition schedules for the scheduler (implemented by the briefing service).
    /// </summary>
    public interface IEditionTrigger
    {
        /// <summary>
        /// Queue an edition briefing (and its fresh research) for a due schedule.
        /// </summary>
        /// <param name="schedule">Due schedule owned by an edition.</param>
        /// <param name="nowUtc">Now.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when a briefing was queued.</returns>
        Task<bool> FireEditionAsync(Schedule schedule, DateTime nowUtc, CancellationToken token);
    }
}
