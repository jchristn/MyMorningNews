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
    }
}
