namespace MyMorningNews.Benchmark.Stub
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Fault injection settings for the stub, per dependency, plus counters of the faults actually injected.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class StubFaults
    {
        #region Public-Members

        /// <summary>
        /// Latency added in Slow mode, in milliseconds. Default 1500, minimum 0, maximum 120000.
        /// </summary>
        public int SlowLatencyMs
        {
            get { return Volatile.Read(ref _SlowLatencyMs); }
            set { Volatile.Write(ref _SlowLatencyMs, Math.Clamp(value, 0, 120000)); }
        }

        /// <summary>
        /// Latency added to every model response in every mode, in milliseconds. Default 0, minimum 0, maximum 60000.
        /// </summary>
        public int BaseModelLatencyMs
        {
            get { return Volatile.Read(ref _BaseModelLatencyMs); }
            set { Volatile.Write(ref _BaseModelLatencyMs, Math.Clamp(value, 0, 60000)); }
        }

        #endregion

        #region Private-Members

        private readonly ConcurrentDictionary<FaultTarget, FaultMode> _Modes = new ConcurrentDictionary<FaultTarget, FaultMode>();
        private readonly ConcurrentDictionary<string, byte> _Seen = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<FaultTarget, int> _Injected = new ConcurrentDictionary<FaultTarget, int>();
        private int _SlowLatencyMs = 1500;
        private int _BaseModelLatencyMs = 0;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Set the mode for a dependency.
        /// </summary>
        /// <param name="target">Dependency.</param>
        /// <param name="mode">Mode.</param>
        public void Set(FaultTarget target, FaultMode mode)
        {
            _Modes[target] = mode;
            if (mode == FaultMode.RateLimitedOnce) _Seen.Clear();
        }

        /// <summary>
        /// Clear every fault.
        /// </summary>
        public void Clear()
        {
            _Modes.Clear();
            _Seen.Clear();
        }

        /// <summary>
        /// The mode for a dependency.
        /// </summary>
        /// <param name="target">Dependency.</param>
        /// <returns>Mode.</returns>
        public FaultMode Get(FaultTarget target)
        {
            return _Modes.TryGetValue(target, out FaultMode m) ? m : FaultMode.None;
        }

        /// <summary>
        /// Decide what to do with a request: 0 to serve it normally, or the HTTP status to fail it with. Counts the
        /// faults injected.
        /// </summary>
        /// <param name="target">Dependency.</param>
        /// <param name="requestKey">Stable description of the request (method, path, and body).</param>
        /// <returns>0, 429, or 503.</returns>
        public int Decide(FaultTarget target, string requestKey)
        {
            ArgumentNullException.ThrowIfNull(requestKey);
            FaultMode mode = Get(target);
            int status = mode switch
            {
                FaultMode.Down => 503,
                FaultMode.RateLimited => 429,
                FaultMode.RateLimitedOnce => _Seen.TryAdd(target + "|" + requestKey, 0) ? 429 : 0,
                _ => 0
            };
            if (status != 0) _Injected.AddOrUpdate(target, 1, (k, v) => v + 1);
            return status;
        }

        /// <summary>
        /// Faults injected so far, per dependency.
        /// </summary>
        /// <returns>Counts.</returns>
        public Dictionary<string, int> InjectedCounts()
        {
            return _Injected.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value, StringComparer.Ordinal);
        }

        #endregion
    }
}
