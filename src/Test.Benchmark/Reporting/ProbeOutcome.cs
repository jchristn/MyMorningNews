namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Benchmark.Metrics;

    /// <summary>
    /// The outcome of one probe in one arm: the primary score (0 to 1), secondary metrics, and what happened. Paired
    /// comparison matches outcomes by Key.
    /// </summary>
    public class ProbeOutcome
    {
        /// <summary>
        /// Arm.
        /// </summary>
        public string Arm { get; set; } = String.Empty;

        /// <summary>
        /// Corpus (or scenario group).
        /// </summary>
        public string Corpus { get; set; } = String.Empty;

        /// <summary>
        /// Probe identifier.
        /// </summary>
        public string ProbeId { get; set; } = String.Empty;

        /// <summary>
        /// Probe type.
        /// </summary>
        public string Type { get; set; } = String.Empty;

        /// <summary>
        /// Category.
        /// </summary>
        public string Category { get; set; } = String.Empty;

        /// <summary>
        /// Round.
        /// </summary>
        public int Round { get; set; } = 1;

        /// <summary>
        /// Whether the probe is negative.
        /// </summary>
        public bool Negative { get; set; } = false;

        /// <summary>
        /// Whether the probe applies to this arm (reference arms skip some types). Inapplicable probes are listed but
        /// excluded from summaries; errors are never inapplicable, they score zero.
        /// </summary>
        public bool Applicable { get; set; } = true;

        /// <summary>
        /// Primary score, 0 to 1 (1 is correct).
        /// </summary>
        public double Score { get; set; } = 0;

        /// <summary>
        /// Secondary metrics.
        /// </summary>
        public SortedDictionary<string, double> Metrics { get; set; } = new SortedDictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// Latency statistics, for timed probes.
        /// </summary>
        public LatencyStats? Latency { get; set; } = null;

        /// <summary>
        /// What was returned or observed.
        /// </summary>
        public string Detail { get; set; } = String.Empty;

        /// <summary>
        /// Error that made the probe fail, or null.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Pairing key: arm/corpus/probe.
        /// </summary>
        public string Key
        {
            get { return Arm + "/" + Corpus + "/" + ProbeId; }
        }
    }
}
