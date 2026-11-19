namespace MyMorningNews.Core.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a settings update.
    /// </summary>
    public class SettingsUpdateResult
    {
        /// <summary>
        /// Whether the update was saved.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Per-field validation errors keyed by path.
        /// </summary>
        public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Paths whose values changed.
        /// </summary>
        public List<string> Changed { get; set; } = new List<string>();

        /// <summary>
        /// Paths applied immediately.
        /// </summary>
        public List<string> AppliedLive { get; set; } = new List<string>();

        /// <summary>
        /// Paths that take effect after a restart.
        /// </summary>
        public List<string> PendingRestart { get; set; } = new List<string>();
    }
}
