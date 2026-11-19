namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Health check response.
    /// </summary>
    public class HealthResponse
    {
        /// <summary>
        /// Overall status: Healthy, Degraded, or Unhealthy.
        /// </summary>
        public string Status { get; set; } = "Healthy";

        /// <summary>
        /// Product version.
        /// </summary>
        public string Version { get; set; } = String.Empty;

        /// <summary>
        /// UTC time the server started.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Seconds since start.
        /// </summary>
        public long UptimeSeconds { get; set; } = 0;

        /// <summary>
        /// Per-component status, keyed by component name.
        /// </summary>
        public Dictionary<string, string> Components { get; set; } = new Dictionary<string, string>();
    }
}
