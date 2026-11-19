namespace MyMorningNews.Server
{
    using System;
    using System.Threading;

    /// <summary>
    /// Process lifetime: shutdown and operator-requested restart. A restart exits with <see cref="RestartExitCode"/> so the
    /// container restart policy relaunches the process, which reloads settings from the mounted file.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class ServerLifetime : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Exit code used for restarts. Default 75 (EX_TEMPFAIL).
        /// </summary>
        public int RestartExitCode { get; set; } = 75;

        /// <summary>
        /// Token cancelled on shutdown or restart.
        /// </summary>
        public CancellationToken Token
        {
            get { return _Source.Token; }
        }

        /// <summary>
        /// Whether a restart was requested.
        /// </summary>
        public bool RestartRequested
        {
            get { return Interlocked.CompareExchange(ref _Restart, 0, 0) == 1; }
        }

        /// <summary>
        /// UTC start time.
        /// </summary>
        public DateTime StartedUtc { get; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private readonly CancellationTokenSource _Source = new CancellationTokenSource();
        private int _Restart = 0;
        private bool _Disposed = false;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Request a graceful shutdown.
        /// </summary>
        public void RequestShutdown()
        {
            if (!_Source.IsCancellationRequested) _Source.Cancel();
        }

        /// <summary>
        /// Request a restart after a short delay so the HTTP response can complete.
        /// </summary>
        /// <param name="delayMs">Delay before shutdown in milliseconds. Default 500.</param>
        public void RequestRestart(int delayMs = 500)
        {
            Interlocked.Exchange(ref _Restart, 1);
            _Source.CancelAfter(Math.Clamp(delayMs, 0, 10000));
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) _Source.Dispose();
            _Disposed = true;
        }

        #endregion
    }
}
