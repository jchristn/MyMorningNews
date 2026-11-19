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
    /// Drains the shared run queue (through <see cref="TestRuns"/>) until a run finishes. Other suites may be draining the
    /// same queue, so this waits on the run's status rather than counting claims.
    /// </summary>
    public static class RunWaiter
    {
        /// <summary>
        /// Wait for a run to leave Queued and Running.
        /// </summary>
        /// <param name="server">Server.</param>
        /// <param name="run">Run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="timeoutSeconds">Longest wait. Default 60.</param>
        /// <returns>Finished run.</returns>
        /// <exception cref="TimeoutException">The run did not finish in time.</exception>
        public static async Task<Run> FinishAsync(MyMorningNewsServer server, Run run, CancellationToken token, int timeoutSeconds = 60)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(run);
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
            {
                int claimed = await server.Context.Scheduling.ExecuteQueuedAsync(TestRuns.Executor(server), "test", new[] { run.TenantId }, token).ConfigureAwait(false);
                Run current = (await server.Context.Database.Runs.ReadAsync(run.TenantId, run.Id, token).ConfigureAwait(false))!;
                if (current.Status != RunStatusEnum.Queued && current.Status != RunStatusEnum.Running) return current;
                if (claimed == 0) await Task.Delay(50, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Run " + run.Id + " did not finish within " + timeoutSeconds + " seconds.");
        }
    }
}
