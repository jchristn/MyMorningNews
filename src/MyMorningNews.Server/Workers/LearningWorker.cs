namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// Learns from feedback every Learning.IntervalMinutes (plan section 18): tags reasons and proposes preferences for
    /// topics with enough new feedback.
    /// </summary>
    public class LearningWorker
    {
        private readonly ServerContext _Server;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public LearningWorker(ServerContext server)
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
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_Server.Settings.Learning.IntervalMinutes), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                await RunOnceAsync(token).ConfigureAwait(false);
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
                int proposed = await _Server.Learning.ProcessDueAsync(token).ConfigureAwait(false);
                if (proposed > 0) _Server.Logging.Info("[LearningWorker] proposed " + proposed + " preferences");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                outcome = "canceled";
            }
            catch (Exception ex)
            {
                outcome = "error";
                _Server.Logging.Warn("[LearningWorker] iteration failed: " + ex.Message);
            }
            finally
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.WorkerIterations, "worker", "learning", "outcome", outcome);
                MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.WorkerDuration, sw.Elapsed.TotalSeconds, "worker", "learning");
            }
        }
    }
}
