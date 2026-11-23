namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// Executes queued runs up to Pipeline.MaxConcurrentRuns through the server run executor (plan section 14.2). A pass
    /// keeps claiming as slots free; between passes the loop waits for the scheduling service's run signal (a run queued
    /// or finished in this process) with Scheduler.RunPollSeconds as the fallback for runs queued by another process.
    /// Thread safety: one loop per instance; several instances (in one or many processes) can run safely because claims use FOR UPDATE SKIP LOCKED.
    /// </summary>
    public class RunWorker
    {
        private readonly ServerContext _Server;
        private readonly string _WorkerId = Environment.MachineName + ":" + Environment.ProcessId + ":" + Guid.NewGuid().ToString("N").Substring(0, 6);

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public RunWorker(ServerContext server)
        {
            ArgumentNullException.ThrowIfNull(server);
            _Server = server;
        }

        /// <summary>
        /// Loop until cancelled.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await RunOnceAsync(token).ConfigureAwait(false);
                try
                {
                    await _Server.Scheduling.RunSignal.WaitAsync(TimeSpan.FromSeconds(_Server.Settings.Scheduler.RunPollSeconds), token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is OperationCanceledException || ex is ObjectDisposedException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// One pass: work the queue until it is idle.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RunOnceAsync(CancellationToken token)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string outcome = "success";
            try
            {
                await _Server.Scheduling.DrainQueuedAsync(_Server.RunExecutor, _WorkerId, null, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                outcome = "canceled";
            }
            catch (Exception ex)
            {
                outcome = "error";
                _Server.Logging.Warn("[RunWorker] iteration failed: " + ex.Message);
            }
            finally
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.WorkerIterations, "worker", "runs", "outcome", outcome);
                MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.WorkerDuration, sw.Elapsed.TotalSeconds, "worker", "runs");
            }
        }
    }
}
