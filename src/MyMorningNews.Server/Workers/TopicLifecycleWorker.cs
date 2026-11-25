namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs the stale topic check (prompts, reminders, recoveries, deletions) every
    /// <see cref="MyMorningNews.Core.Settings.TopicLifecycleSettings.IntervalMinutes"/>. The first pass waits a minute
    /// after start so a restart never races the scheduler.
    /// </summary>
    public class TopicLifecycleWorker
    {
        private readonly ServerContext _Server;
        private TimeSpan _StartDelay = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Delay before the first pass. Default one minute, minimum zero, maximum one hour.
        /// </summary>
        public TimeSpan StartDelay
        {
            get { return _StartDelay; }
            set { _StartDelay = value < TimeSpan.Zero ? TimeSpan.Zero : value > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : value; }
        }

        /// <summary>
        /// Bind to the running server's services.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public TopicLifecycleWorker(ServerContext server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        /// <summary>
        /// Loop until canceled. Failures are logged and retried on the next pass.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RunAsync(CancellationToken token)
        {
            try { await Task.Delay(StartDelay, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            while (!token.IsCancellationRequested)
            {
                try { await _Server.TopicLifecycle.RunOnceAsync(DateTime.UtcNow, token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex) { _Server.Logging.Warn("[TopicLifecycleWorker] " + ex.GetType().Name + ": " + ex.Message); }
                try { await Task.Delay(TimeSpan.FromMinutes(_Server.Settings.TopicLifecycle.IntervalMinutes), token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
