namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Checks every 15 minutes whether the daily administrator summary is due and sends it. Sending is idempotent per
    /// day and recipient, so restarts and several server instances send it once.
    /// </summary>
    public class AdminReportWorker
    {
        private readonly ServerContext _Server;
        private TimeSpan _Interval = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Time between checks. Default 15 minutes, minimum one minute, maximum one hour.
        /// </summary>
        public TimeSpan Interval
        {
            get { return _Interval; }
            set { _Interval = value < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : value > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : value; }
        }

        /// <summary>
        /// Bind to the running server's services.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public AdminReportWorker(ServerContext server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        /// <summary>
        /// Loop until canceled. Failures are logged and retried on the next check.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try { await _Server.AdminReports.SendDueAsync(DateTime.UtcNow, token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex) { _Server.Logging.Warn("[AdminReportWorker] " + ex.GetType().Name + ": " + ex.Message); }
                try { await Task.Delay(Interval, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
