namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Benchmark.Metrics;

    /// <summary>
    /// A benchmark report: configuration, provenance, summaries, and every per-probe outcome, so a run can be compared
    /// probe by probe and re-analyzed offline.
    /// </summary>
    public class BenchmarkReport
    {
        /// <summary>
        /// Report format version.
        /// </summary>
        public int Format { get; set; } = 1;

        /// <summary>
        /// Kind: quality, ingest, e2e, load, or resilience.
        /// </summary>
        public string Kind { get; set; } = String.Empty;

        /// <summary>
        /// Dataset name (or "scenarios" for kinds without a dataset).
        /// </summary>
        public string Dataset { get; set; } = String.Empty;

        /// <summary>
        /// Label (round or variant), or empty.
        /// </summary>
        public string Label { get; set; } = String.Empty;

        /// <summary>
        /// Mode: stub (deterministic, no paid calls) or paid.
        /// </summary>
        public string Mode { get; set; } = "stub";

        /// <summary>
        /// Arms measured.
        /// </summary>
        public List<string> Arms { get; set; } = new List<string>();

        /// <summary>
        /// Provenance.
        /// </summary>
        public Provenance Provenance { get; set; } = new Provenance();

        /// <summary>
        /// Every option value used, including defaults. Secrets are masked.
        /// </summary>
        public SortedDictionary<string, string> Options { get; set; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Whether some input failed to load, so the results are not comparable.
        /// </summary>
        public bool Incomplete { get; set; } = false;

        /// <summary>
        /// Why the run is incomplete.
        /// </summary>
        public List<string> IncompleteReasons { get; set; } = new List<string>();

        /// <summary>
        /// Summaries.
        /// </summary>
        public List<MetricSummary> Summaries { get; set; } = new List<MetricSummary>();

        /// <summary>
        /// Per-probe outcomes.
        /// </summary>
        public List<ProbeOutcome> Probes { get; set; } = new List<ProbeOutcome>();

        /// <summary>
        /// Server-side time by stage, from the Prometheus histogram diff.
        /// </summary>
        public List<StageTime> Stages { get; set; } = new List<StageTime>();

        /// <summary>
        /// Free-form notes (environment, sharing, warnings).
        /// </summary>
        public List<string> Notes { get; set; } = new List<string>();

        /// <summary>
        /// Wall-clock duration of the command in seconds.
        /// </summary>
        public double DurationSeconds { get; set; } = 0;
    }
}
