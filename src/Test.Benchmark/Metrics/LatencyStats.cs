namespace MyMorningNews.Benchmark.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Latency percentiles (nearest rank) over successful operations, in milliseconds.
    /// </summary>
    public class LatencyStats
    {
        #region Public-Members

        /// <summary>
        /// Samples.
        /// </summary>
        public int Count { get; set; } = 0;

        /// <summary>
        /// Mean.
        /// </summary>
        public double Mean { get; set; } = 0;

        /// <summary>
        /// Median.
        /// </summary>
        public double P50 { get; set; } = 0;

        /// <summary>
        /// 90th percentile.
        /// </summary>
        public double P90 { get; set; } = 0;

        /// <summary>
        /// 95th percentile.
        /// </summary>
        public double P95 { get; set; } = 0;

        /// <summary>
        /// 99th percentile.
        /// </summary>
        public double P99 { get; set; } = 0;

        /// <summary>
        /// Maximum.
        /// </summary>
        public double Max { get; set; } = 0;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Compute from samples.
        /// </summary>
        /// <param name="samples">Latencies in milliseconds.</param>
        /// <returns>Stats; all zeros when empty.</returns>
        public static LatencyStats From(IEnumerable<double> samples)
        {
            ArgumentNullException.ThrowIfNull(samples);
            List<double> s = samples.OrderBy(x => x).ToList();
            LatencyStats stats = new LatencyStats { Count = s.Count };
            if (s.Count == 0) return stats;
            stats.Mean = Math.Round(s.Average(), 2);
            stats.P50 = Rank(s, 0.50);
            stats.P90 = Rank(s, 0.90);
            stats.P95 = Rank(s, 0.95);
            stats.P99 = Rank(s, 0.99);
            stats.Max = Math.Round(s[s.Count - 1], 2);
            return stats;
        }

        #endregion

        #region Private-Methods

        private static double Rank(List<double> sorted, double p)
        {
            int index = (int)Math.Ceiling(p * sorted.Count) - 1;
            return Math.Round(sorted[Math.Clamp(index, 0, sorted.Count - 1)], 2);
        }

        #endregion
    }
}
