namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// Composes queued edition briefings once their fresh research finishes (plan section 21.3).
    /// </summary>
    public class BriefingWorker
    {
        /// <summary>
        /// Time between passes. Default 15 seconds, minimum 1 second.
        /// </summary>
        public TimeSpan Interval
        {
            get { return _Interval; }
            set { _Interval = value < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : value; }
        }

        private readonly ServerContext _Server;
        private TimeSpan _Interval = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public BriefingWorker(ServerContext server)
        {
            ArgumentNullException.ThrowIfNull(server);
            _Server = server;
        }

        /// <summary>
        /// Run until cancelled.
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
                    await Task.Delay(Interval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// One pass.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RunOnceAsync(CancellationToken token)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string outcome = "success";
            try
            {
                int done = await _Server.Briefings.ProcessQueuedAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                if (done > 0) _Server.Logging.Debug("[BriefingWorker] finished " + done + " briefings");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                outcome = "canceled";
            }
            catch (Exception ex)
            {
                outcome = "error";
                _Server.Logging.Warn("[BriefingWorker] iteration failed: " + ex.Message);
            }
            finally
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.WorkerIterations, "worker", "briefing", "outcome", outcome);
                MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.WorkerDuration, sw.Elapsed.TotalSeconds, "worker", "briefing");
            }
        }
    }
}
