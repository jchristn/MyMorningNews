namespace Test.Shared.Helpers
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Server;

    /// <summary>
    /// Drains the briefing queue for one tenant until a briefing is ready or failed. Scoping to the tenant keeps suites
    /// running in parallel from claiming each other's briefings.
    /// </summary>
    public static class BriefingWaiter
    {
        /// <summary>
        /// Wait for a briefing to finish composing.
        /// </summary>
        /// <param name="server">Server.</param>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="briefingId">Briefing.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="timeoutSeconds">Longest wait. Default 60.</param>
        /// <returns>Finished briefing.</returns>
        /// <exception cref="TimeoutException">It did not finish in time.</exception>
        public static async Task<Briefing> FinishAsync(MyMorningNewsServer server, string tenantId, string briefingId, CancellationToken token, int timeoutSeconds = 60)
        {
            ArgumentNullException.ThrowIfNull(server);
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
            {
                await server.Context.Briefings.ProcessQueuedAsync(DateTime.UtcNow, new[] { tenantId }, token).ConfigureAwait(false);
                Briefing b = (await server.Context.Database.Briefings.ReadAsync(tenantId, briefingId, token).ConfigureAwait(false))!;
                if (b.Status == BriefingStatusEnum.Ready || b.Status == BriefingStatusEnum.Failed) return b;
                await Task.Delay(50, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Briefing " + briefingId + " did not finish within " + timeoutSeconds + " seconds.");
        }
    }
}
