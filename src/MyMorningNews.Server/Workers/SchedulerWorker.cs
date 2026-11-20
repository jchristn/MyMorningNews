namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// Fires due schedules and due one-time requests into queued runs (plan section 14.2).
    /// Thread safety: one loop per instance; several instances (in one or many processes) can run safely because claims use FOR UPDATE SKIP LOCKED.
    /// </summary>
    public class SchedulerWorker
    {
        private DateTime _LastNoticeUtc = DateTime.MinValue;
        private readonly ServerContext _Server;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public SchedulerWorker(ServerContext server)
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
                    await Task.Delay(TimeSpan.FromSeconds(_Server.Settings.Scheduler.PollIntervalSeconds), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// One iteration.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RunOnceAsync(CancellationToken token)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string outcome = "success";
            try
            {
                int queued = await _Server.Scheduling.TickAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                if (queued > 0) _Server.Logging.Debug("[SchedulerWorker] queued " + queued + " runs");
                if ((DateTime.UtcNow - _LastNoticeUtc).TotalMinutes >= 15)
                {
                    _LastNoticeUtc = DateTime.UtcNow;
                    int noticed = await _Server.PredictionNotices.NotifyDueAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                    if (noticed > 0) _Server.Logging.Info("[SchedulerWorker] sent due notices for " + noticed + " predictions");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                outcome = "canceled";
            }
            catch (Exception ex)
            {
                outcome = "error";
                _Server.Logging.Warn("[SchedulerWorker] iteration failed: " + ex.Message);
            }
            finally
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.WorkerIterations, "worker", "scheduler", "outcome", outcome);
                MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.WorkerDuration, sw.Elapsed.TotalSeconds, "worker", "scheduler");
            }
        }
    }
}
