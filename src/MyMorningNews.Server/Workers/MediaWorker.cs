namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// Produces queued audio episodes (plan section 22).
    /// </summary>
    public class MediaWorker
    {
        /// <summary>
        /// Time between passes. Default 10 seconds, minimum 1 second.
        /// </summary>
        public TimeSpan Interval
        {
            get { return _Interval; }
            set { _Interval = value < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : value; }
        }

        private readonly ServerContext _Server;
        private TimeSpan _Interval = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public MediaWorker(ServerContext server)
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
                int done = await _Server.Audio.ProcessQueuedAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                if (done > 0) _Server.Logging.Debug("[MediaWorker] finished " + done + " audio episodes");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                outcome = "canceled";
            }
            catch (Exception ex)
            {
                outcome = "error";
                _Server.Logging.Warn("[MediaWorker] iteration failed: " + ex.Message);
            }
            finally
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.WorkerIterations, "worker", "media", "outcome", outcome);
                MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.WorkerDuration, sw.Elapsed.TotalSeconds, "worker", "media");
            }
        }
    }
}
