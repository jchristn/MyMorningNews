namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The saved output of compare.
    /// </summary>
    public class CompareReport
    {
        /// <summary>
        /// Time of the comparison (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Baseline report path.
        /// </summary>
        public string Baseline { get; set; } = String.Empty;

        /// <summary>
        /// Candidate report path.
        /// </summary>
        public string Candidate { get; set; } = String.Empty;

        /// <summary>
        /// Report kind.
        /// </summary>
        public string Kind { get; set; } = String.Empty;

        /// <summary>
        /// Settings used (tolerance, alpha, latency tolerance, mode).
        /// </summary>
        public SortedDictionary<string, string> Settings { get; set; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Rows.
        /// </summary>
        public List<CompareRow> Rows { get; set; } = new List<CompareRow>();

        /// <summary>
        /// Probes only in the baseline.
        /// </summary>
        public List<string> MissingFromCandidate { get; set; } = new List<string>();

        /// <summary>
        /// Probes only in the candidate.
        /// </summary>
        public List<string> MissingFromBaseline { get; set; } = new List<string>();

        /// <summary>
        /// Problems that make the comparison fail regardless of the rows (incomplete runs, mismatched sets).
        /// </summary>
        public List<string> Problems { get; set; } = new List<string>();

        /// <summary>
        /// Whether a regression (or a problem) was found.
        /// </summary>
        public bool Regression { get; set; } = false;
    }
}
