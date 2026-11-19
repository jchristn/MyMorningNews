namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Server version, configuration summary, and bundled tool links.
    /// </summary>
    public class ServerInfoResponse
    {
        /// <summary>
        /// Product name.
        /// </summary>
        public string Product { get; set; } = String.Empty;

        /// <summary>
        /// Version.
        /// </summary>
        public string Version { get; set; } = String.Empty;

        /// <summary>
        /// Start time.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Uptime.
        /// </summary>
        public long UptimeSeconds { get; set; } = 0;

        /// <summary>
        /// Database provider.
        /// </summary>
        public string DatabaseType { get; set; } = String.Empty;

        /// <summary>
        /// Applied schema version.
        /// </summary>
        public int SchemaVersion { get; set; } = 0;

        /// <summary>
        /// Whether pgvector is available.
        /// </summary>
        public bool VectorAvailable { get; set; } = false;

        /// <summary>
        /// .NET runtime version.
        /// </summary>
        public string RuntimeVersion { get; set; } = String.Empty;

        /// <summary>
        /// Host operating system.
        /// </summary>
        public string OperatingSystem { get; set; } = String.Empty;

        /// <summary>
        /// Source repository.
        /// </summary>
        public string RepositoryUrl { get; set; } = "https://github.com/jchristn/MyMorningNews";

        /// <summary>
        /// Bundled tools.
        /// </summary>
        public List<ExternalServiceInfo> ExternalServices { get; set; } = new List<ExternalServiceInfo>();

        /// <summary>
        /// Settings saved but awaiting restart.
        /// </summary>
        public List<string> PendingRestart { get; set; } = new List<string>();
    }
}
