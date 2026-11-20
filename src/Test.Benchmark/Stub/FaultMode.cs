namespace MyMorningNews.Benchmark.Stub
{
    /// <summary>
    /// A failure the stub injects for one dependency.
    /// </summary>
    public enum FaultMode
    {
        /// <summary>
        /// Healthy.
        /// </summary>
        None,

        /// <summary>
        /// Healthy but slow: every response waits the configured latency first.
        /// </summary>
        Slow,

        /// <summary>
        /// Down: every request gets 503 Service Unavailable.
        /// </summary>
        Down,

        /// <summary>
        /// Rate limited: every request gets 429 Too Many Requests with Retry-After: 1.
        /// </summary>
        RateLimited,

        /// <summary>
        /// Rate limited once: the first sighting of each distinct request gets 429; an identical retry succeeds.
        /// </summary>
        RateLimitedOnce
    }
}
