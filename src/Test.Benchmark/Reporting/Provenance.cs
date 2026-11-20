namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Everything needed to know what produced a report.
    /// </summary>
    public class Provenance
    {
        /// <summary>
        /// Start time (UTC).
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Server under test.
        /// </summary>
        public string SutUrl { get; set; } = String.Empty;

        /// <summary>
        /// Server version reported by the server.
        /// </summary>
        public string SutVersion { get; set; } = "unknown";

        /// <summary>
        /// Product commit, with -dirty when the working tree has any change, including untracked files.
        /// </summary>
        public string ProductCommit { get; set; } = "unknown";

        /// <summary>
        /// Harness version.
        /// </summary>
        public string HarnessVersion { get; set; } = "unknown";

        /// <summary>
        /// Last commit that touched the harness or benchmarks/, with -dirty when those paths have changes.
        /// </summary>
        public string HarnessCommit { get; set; } = "unknown";

        /// <summary>
        /// Machine name.
        /// </summary>
        public string Machine { get; set; } = String.Empty;

        /// <summary>
        /// Operating system.
        /// </summary>
        public string Os { get; set; } = String.Empty;

        /// <summary>
        /// CPU model and core count.
        /// </summary>
        public string Cpu { get; set; } = String.Empty;

        /// <summary>
        /// .NET runtime of the harness.
        /// </summary>
        public string Runtime { get; set; } = String.Empty;

        /// <summary>
        /// Image digests of every container in the bench compose project.
        /// </summary>
        public SortedDictionary<string, string> Images { get; set; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// External models and services this run used.
        /// </summary>
        public SortedDictionary<string, string> Services { get; set; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Dataset name.
        /// </summary>
        public string Dataset { get; set; } = String.Empty;

        /// <summary>
        /// Dataset SHA-256.
        /// </summary>
        public string DatasetSha256 { get; set; } = String.Empty;

        /// <summary>
        /// Command line with secrets masked.
        /// </summary>
        public string CommandLine { get; set; } = String.Empty;

        /// <summary>
        /// HTTP client settings (connection limit and timeout).
        /// </summary>
        public string HttpClient { get; set; } = String.Empty;
    }
}
