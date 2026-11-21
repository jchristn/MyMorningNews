namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Whether the workspace has everything it needs, checked with live calls.
    /// </summary>
    public class SetupStatusResponse
    {
        /// <summary>
        /// True when every required check is ready.
        /// </summary>
        public bool Ready { get; set; } = false;

        /// <summary>
        /// Whether the caller can change the configuration (a global or tenant administrator).
        /// </summary>
        public bool CanConfigure { get; set; } = false;

        /// <summary>
        /// Checks, required ones first.
        /// </summary>
        public List<SetupCheck> Checks { get; set; } = new List<SetupCheck>();

        /// <summary>
        /// When the status was computed (UTC).
        /// </summary>
        public DateTime CheckedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Result of the connection attempt, on responses to POST /v1.0/api/setup/connect. Null otherwise.
        /// </summary>
        public SetupConnectionResult? Connection { get; set; } = null;
    }
}
