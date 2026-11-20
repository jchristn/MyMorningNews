namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// Sliding one-minute window rate limiter for authentication attempts.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class LoginRateLimiter
    {
        #region Public-Members

        /// <summary>
        /// Attempts permitted per key per window (WindowSeconds, default one minute). Minimum 1, maximum 1000.
        /// </summary>
        public int LimitPerMinute
        {
            get
            {
                return _LimitPerMinute;
            }
            set
            {
                _LimitPerMinute = Math.Clamp(value, 1, 100000);
            }
        }

        /// <summary>
        /// Length of the counting window in seconds (the limit applies per window). Default 60 (per minute). Minimum 1,
        /// maximum 86400.
        /// </summary>
        public int WindowSeconds
        {
            get
            {
                return _WindowSeconds;
            }
            set
            {
                _WindowSeconds = Math.Clamp(value, 1, 86400);
            }
        }

        #endregion

        #region Private-Members

        private readonly ConcurrentDictionary<string, Queue<DateTime>> _Attempts = new ConcurrentDictionary<string, Queue<DateTime>>(StringComparer.OrdinalIgnoreCase);
        private const int _PruneEvery = 1024;
        private int _Calls = 0;
        private int _LimitPerMinute = 10;
        private int _WindowSeconds = 60;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="limitPerMinute">Attempts per minute.</param>
        public LoginRateLimiter(int limitPerMinute)
        {
            LimitPerMinute = limitPerMinute;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Record an attempt and report whether it is allowed.
        /// </summary>
        /// <param name="key">Key, for example source IP plus email.</param>
        /// <returns>True when within the limit.</returns>
        public bool TryAcquire(string key)
        {
            if (String.IsNullOrEmpty(key)) key = "anonymous";
            DateTime now = DateTime.UtcNow;
            if (System.Threading.Interlocked.Increment(ref _Calls) % _PruneEvery == 0) Prune(now);
            Queue<DateTime> queue = _Attempts.GetOrAdd(key, k => new Queue<DateTime>());
            lock (queue)
            {
                while (queue.Count > 0 && (now - queue.Peek()).TotalSeconds >= _WindowSeconds) queue.Dequeue();
                if (queue.Count >= _LimitPerMinute) return false;
                queue.Enqueue(now);
                return true;
            }
        }

        /// <summary>
        /// Number of keys being tracked. Expired keys are removed every 1024 attempts, so memory stays bounded by the
        /// keys seen within one window.
        /// </summary>
        public int TrackedKeys
        {
            get { return _Attempts.Count; }
        }

        /// <summary>
        /// Remove keys with no attempt inside the window.
        /// </summary>
        /// <param name="nowUtc">Current time.</param>
        /// <returns>Keys removed.</returns>
        public int Prune(DateTime nowUtc)
        {
            int removed = 0;
            foreach (KeyValuePair<string, Queue<DateTime>> pair in _Attempts)
            {
                bool stale;
                lock (pair.Value)
                {
                    while (pair.Value.Count > 0 && (nowUtc - pair.Value.Peek()).TotalSeconds >= _WindowSeconds) pair.Value.Dequeue();
                    stale = pair.Value.Count == 0;
                }

                if (stale && _Attempts.TryRemove(pair.Key, out Queue<DateTime>? _)) removed++;
            }

            return removed;
        }

        /// <summary>
        /// Forget attempts for a key, for example after a successful login.
        /// </summary>
        /// <param name="key">Key.</param>
        public void Reset(string key)
        {
            if (!String.IsNullOrEmpty(key)) _Attempts.TryRemove(key, out Queue<DateTime>? _);
        }

        #endregion
    }
}
