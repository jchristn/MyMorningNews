namespace MyMorningNews.Server.Workers
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// Hourly maintenance: prunes request history past its retention window and expired sessions.
    /// Thread safety: runs on its own task.
    /// </summary>
    public class RetentionWorker
    {
        #region Public-Members

        /// <summary>
        /// Interval between runs. Default one hour. Minimum one minute.
        /// </summary>
        public TimeSpan Interval
        {
            get
            {
                return _Interval;
            }
            set
            {
                _Interval = value < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : value;
            }
        }

        /// <summary>
        /// Days to keep expired or revoked sessions. Default 7. Minimum 1, maximum 365.
        /// </summary>
        public int ExpiredSessionRetentionDays
        {
            get
            {
                return _ExpiredSessionRetentionDays;
            }
            set
            {
                _ExpiredSessionRetentionDays = Math.Clamp(value, 1, 365);
            }
        }

        #endregion

        #region Private-Members

        private readonly ServerContext _Server;
        private TimeSpan _Interval = TimeSpan.FromHours(1);
        private int _ExpiredSessionRetentionDays = 7;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public RetentionWorker(ServerContext server)
        {
            ArgumentNullException.ThrowIfNull(server);
            _Server = server;
        }

        #endregion

        #region Public-Methods

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
        /// Run one iteration.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RunOnceAsync(CancellationToken token)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string outcome = "success";
            try
            {
                DateTime cutoff = DateTime.UtcNow.AddDays(-_Server.Settings.RequestHistory.RetentionDays);
                int pruned = await _Server.Database.RequestHistory.PruneAsync(cutoff, token).ConfigureAwait(false);
                int sessions = await _Server.Database.AuthSessions.DeleteExpiredAsync(DateTime.UtcNow.AddDays(-ExpiredSessionRetentionDays), token).ConfigureAwait(false);
                if (pruned > 0 || sessions > 0) _Server.Logging.Info("[RetentionWorker] pruned " + pruned + " requests and " + sessions + " expired sessions");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                outcome = "canceled";
            }
            catch (Exception ex)
            {
                outcome = "error";
                _Server.Logging.Warn("[RetentionWorker] iteration failed: " + ex.Message);
            }
            finally
            {
                MyMorningNewsTelemetry.Increment(MyMorningNewsTelemetry.WorkerIterations, "worker", "retention", "outcome", outcome);
                MyMorningNewsTelemetry.Record(MyMorningNewsTelemetry.WorkerDuration, sw.Elapsed.TotalSeconds, "worker", "retention");
            }
        }

        #endregion
    }
}
