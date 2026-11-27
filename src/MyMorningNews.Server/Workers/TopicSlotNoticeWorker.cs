namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Checks every account against its custom topic limit every <see cref="Interval"/> and tells owners who just went
    /// over (<see cref="MyMorningNews.Core.Services.TopicSlotNoticeService"/>). One central check covers every way the
    /// limit can drop: downgrades, expired or canceled subscriptions, administrator plan changes and overrides, and
    /// billing provider updates. The first pass waits <see cref="StartDelay"/> after start.
    /// </summary>
    public class TopicSlotNoticeWorker
    {
        private readonly ServerContext _Server;
        private TimeSpan _StartDelay = TimeSpan.FromMinutes(2);
        private TimeSpan _Interval = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Delay before the first pass. Default two minutes, minimum zero, maximum one hour.
        /// </summary>
        public TimeSpan StartDelay
        {
            get { return _StartDelay; }
            set { _StartDelay = value < TimeSpan.Zero ? TimeSpan.Zero : value > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : value; }
        }

        /// <summary>
        /// Time between passes. Default 15 minutes, minimum one minute, maximum one day.
        /// </summary>
        public TimeSpan Interval
        {
            get { return _Interval; }
            set { _Interval = value < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : value > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : value; }
        }

        /// <summary>
        /// Bind to the running server's services.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public TopicSlotNoticeWorker(ServerContext server)
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
                try
                {
                    int notified = await _Server.TopicSlotNotices.RunOnceAsync(token).ConfigureAwait(false);
                    if (notified > 0) _Server.Logging.Info("[TopicSlotNoticeWorker] told " + notified + " owners that topics are past their plan's limit");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex) { _Server.Logging.Warn("[TopicSlotNoticeWorker] " + ex.GetType().Name + ": " + ex.Message); }
                try { await Task.Delay(Interval, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
