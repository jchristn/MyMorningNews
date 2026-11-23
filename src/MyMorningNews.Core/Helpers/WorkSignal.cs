namespace MyMorningNews.Core.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Wakes a polling worker when work is queued in this process, so it does not wait for its next poll. The signal is a
    /// single latch: any number of <see cref="Signal"/> calls before a wait collapse into one wake, and a signal sent
    /// while nobody waits is kept for the next wait. It carries no payload and never crosses processes, so a worker must
    /// keep its poll interval as the timeout to pick up work queued elsewhere.
    /// Thread safety: safe for concurrent use. Intended for one waiter at a time; with several waiters one signal wakes one.
    /// </summary>
    public class WorkSignal : IDisposable
    {
        private readonly SemaphoreSlim _Latch = new SemaphoreSlim(0, 1);
        private int _Disposed = 0;

        /// <summary>
        /// Report that work is available. Never blocks and never throws; a no-op when already signalled or disposed.
        /// </summary>
        public void Signal()
        {
            if (Volatile.Read(ref _Disposed) != 0) return;
            try
            {
                if (_Latch.CurrentCount == 0) _Latch.Release();
            }
            catch (SemaphoreFullException)
            {
                // Another caller signalled first: the latch is already set.
            }
            catch (ObjectDisposedException)
            {
                // Disposed between the check and the release: nobody is left to wake.
            }
        }

        /// <summary>
        /// Wait for a signal, a timeout, or cancellation. A signal is consumed by the wait that returns true.
        /// </summary>
        /// <param name="timeout">Longest wait. <see cref="Timeout.InfiniteTimeSpan"/> waits without a limit; other negative values are treated as zero; values above about 24 days are capped.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when signalled, false when the timeout passed first.</returns>
        /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
        /// <exception cref="ObjectDisposedException">The signal was disposed.</exception>
        public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _Disposed) != 0, this);
            int milliseconds = timeout == Timeout.InfiniteTimeSpan ? Timeout.Infinite : (int)Math.Clamp(timeout.TotalMilliseconds, 0, Int32.MaxValue);
            return await _Latch.WaitAsync(milliseconds, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _Disposed, 1) != 0) return;
            if (disposing) _Latch.Dispose();
        }
    }
}
