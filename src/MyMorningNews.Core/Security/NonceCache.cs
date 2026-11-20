namespace MyMorningNews.Core.Security
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Remembers signed-request nonces until they expire, so a captured request cannot be replayed. In memory, so it
    /// protects a single server process; behind several instances, use sticky routing or keep the skew window short.
    /// Thread safe.
    /// </summary>
    public class NonceCache
    {
        #region Public-Members

        /// <summary>
        /// Maximum remembered nonces. Default 200000, minimum 1000, maximum 5000000. When full after removing expired
        /// entries, new nonces are refused so replay protection never silently lapses.
        /// </summary>
        public int Capacity
        {
            get
            {
                return _Capacity;
            }
            set
            {
                _Capacity = Math.Clamp(value, 1000, 5000000);
            }
        }

        /// <summary>
        /// Nonces currently remembered.
        /// </summary>
        public int Count
        {
            get { return _Entries.Count; }
        }

        #endregion

        #region Private-Members

        private readonly ConcurrentDictionary<string, DateTime> _Entries = new ConcurrentDictionary<string, DateTime>(StringComparer.Ordinal);
        private int _Capacity = 200000;
        private int _Adds = 0;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Record a nonce. Returns false when it was already seen and has not expired, or when the cache is full.
        /// </summary>
        /// <param name="key">Nonce key (scope it by credential).</param>
        /// <param name="nowUtc">Current time.</param>
        /// <param name="expiresUtc">When the nonce may be forgotten.</param>
        /// <returns>True when the nonce is new.</returns>
        /// <exception cref="ArgumentNullException">Key is null or empty.</exception>
        public bool TryUse(string key, DateTime nowUtc, DateTime expiresUtc)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (Interlocked.Increment(ref _Adds) % 1000 == 0 || _Entries.Count >= _Capacity) Sweep(nowUtc);
            if (_Entries.Count >= _Capacity) return false;
            while (true)
            {
                if (_Entries.TryAdd(key, expiresUtc)) return true;
                if (!_Entries.TryGetValue(key, out DateTime existing)) continue;
                if (existing > nowUtc) return false;
                if (_Entries.TryUpdate(key, expiresUtc, existing)) return true;
            }
        }

        /// <summary>
        /// Remove expired nonces.
        /// </summary>
        /// <param name="nowUtc">Current time.</param>
        /// <returns>Entries removed.</returns>
        public int Sweep(DateTime nowUtc)
        {
            int removed = 0;
            List<KeyValuePair<string, DateTime>> expired = _Entries.Where(e => e.Value <= nowUtc).ToList();
            foreach (KeyValuePair<string, DateTime> e in expired)
            {
                if (((ICollection<KeyValuePair<string, DateTime>>)_Entries).Remove(e)) removed++;
            }

            return removed;
        }

        #endregion
    }
}
