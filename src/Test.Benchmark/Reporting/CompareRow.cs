namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One compared metric: the paired difference and whether it is a regression.
    /// </summary>
    public class CompareRow
    {
        /// <summary>
        /// Arm.
        /// </summary>
        public string Arm { get; set; } = String.Empty;

        /// <summary>
        /// Group (overall, type:..., or a latency probe).
        /// </summary>
        public string Group { get; set; } = String.Empty;

        /// <summary>
        /// Metric.
        /// </summary>
        public string Metric { get; set; } = String.Empty;

        /// <summary>
        /// Pairs compared.
        /// </summary>
        public int N { get; set; } = 0;

        /// <summary>
        /// Baseline mean.
        /// </summary>
        public double Baseline { get; set; } = 0;

        /// <summary>
        /// Candidate mean.
        /// </summary>
        public double Candidate { get; set; } = 0;

        /// <summary>
        /// Candidate minus baseline (for latency: relative change).
        /// </summary>
        public double Delta { get; set; } = 0;

        /// <summary>
        /// Lower bound of the paired 95% interval.
        /// </summary>
        public double Low { get; set; } = 0;

        /// <summary>
        /// Upper bound of the paired 95% interval.
        /// </summary>
        public double High { get; set; } = 0;

        /// <summary>
        /// Two-sided p-value.
        /// </summary>
        public double P { get; set; } = 1;

        /// <summary>
        /// Whether this row is a regression.
        /// </summary>
        public bool Regression { get; set; } = false;

        /// <summary>
        /// Whether this row is a significant improvement.
        /// </summary>
        public bool Improvement { get; set; } = false;

        /// <summary>
        /// Probes that got worse (deterministic mode), by key.
        /// </summary>
        public List<string> Worse { get; set; } = new List<string>();
    }
}
