namespace MyMorningNews.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Server version, configuration summary, and bundled tool links. Everyone signed in sees the product, version, and
    /// uptime; the runtime, database, pending restart, and bundled tool details go only to global administrators.
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
        /// Database provider. Null unless the caller is a global administrator.
        /// </summary>
        public string? DatabaseType { get; set; } = null;

        /// <summary>
        /// Applied schema version. Null unless the caller is a global administrator.
        /// </summary>
        public int? SchemaVersion { get; set; } = null;

        /// <summary>
        /// Whether pgvector is available. Null unless the caller is a global administrator.
        /// </summary>
        public bool? VectorAvailable { get; set; } = null;

        /// <summary>
        /// .NET runtime version. Null unless the caller is a global administrator.
        /// </summary>
        public string? RuntimeVersion { get; set; } = null;

        /// <summary>
        /// Host operating system. Null unless the caller is a global administrator.
        /// </summary>
        public string? OperatingSystem { get; set; } = null;

        /// <summary>
        /// Source repository.
        /// </summary>
        public string RepositoryUrl { get; set; } = "https://github.com/jchristn/MyMorningNews";

        /// <summary>
        /// Bundled tools (Grafana, Prometheus, Tempo, Loki). Empty unless the caller is a global administrator.
        /// </summary>
        public List<ExternalServiceInfo> ExternalServices { get; set; } = new List<ExternalServiceInfo>();

        /// <summary>
        /// Settings saved but awaiting restart. Empty unless the caller is a global administrator.
        /// </summary>
        public List<string> PendingRestart { get; set; } = new List<string>();
    }
}
