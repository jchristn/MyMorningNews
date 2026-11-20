namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Benchmark.Metrics;

    /// <summary>
    /// Finds the server's histograms in a scrape (names depend on the exporter's unit suffixes) and diffs them.
    /// </summary>
    public static class StageMetrics
    {
        #region Public-Methods

        /// <summary>
        /// Time by label value for the first histogram whose name contains a fragment.
        /// </summary>
        /// <param name="before">Earlier scrape.</param>
        /// <param name="after">Later scrape.</param>
        /// <param name="fragment">Name fragment, such as run_stage_duration.</param>
        /// <param name="label">Label to group by.</param>
        /// <returns>Rows; empty when either scrape failed or the histogram is absent.</returns>
        public static List<StageTime> Diff(PrometheusSnapshot before, PrometheusSnapshot after, string fragment, string label)
        {
            ArgumentNullException.ThrowIfNull(before);
            ArgumentNullException.ThrowIfNull(after);
            if (!before.Available || !after.Available) return new List<StageTime>();
            string? sum = after.Samples.Keys.FirstOrDefault(k => k.Contains(fragment, StringComparison.Ordinal) && k.Contains("_sum", StringComparison.Ordinal));
            if (sum == null) return new List<StageTime>();
            string name = sum.Substring(0, sum.IndexOf("_sum", StringComparison.Ordinal));
            return PrometheusSnapshot.Diff(before, after, name, label);
        }

        #endregion
    }
}
