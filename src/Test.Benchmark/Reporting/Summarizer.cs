namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Benchmark.Metrics;

    /// <summary>
    /// Builds the summaries of a report from its probe outcomes: per arm, the primary score overall, for positive and
    /// negative probes, per probe type, and per category, and every secondary metric per type. Inapplicable probes are
    /// left out; failed probes count as zero.
    /// </summary>
    public static class Summarizer
    {
        #region Public-Methods

        /// <summary>
        /// Summarize a report in place.
        /// </summary>
        /// <param name="report">Report.</param>
        /// <param name="resamples">Bootstrap resamples.</param>
        /// <param name="seed">Bootstrap seed.</param>
        public static void Summarize(BenchmarkReport report, int resamples, int seed)
        {
            ArgumentNullException.ThrowIfNull(report);
            report.Summaries.Clear();
            foreach (string arm in report.Probes.Select(p => p.Arm).Distinct(StringComparer.Ordinal).OrderBy(a => report.Arms.IndexOf(a) < 0 ? 99 : report.Arms.IndexOf(a)))
            {
                List<ProbeOutcome> probes = report.Probes.Where(p => p.Arm == arm && p.Applicable).ToList();
                Add(report, arm, "overall", "score", probes, resamples, seed);
                Add(report, arm, "positive", "score", probes.Where(p => !p.Negative).ToList(), resamples, seed);
                Add(report, arm, "negative", "score", probes.Where(p => p.Negative).ToList(), resamples, seed);
                foreach (string type in probes.Select(p => p.Type).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal))
                {
                    List<ProbeOutcome> ofType = probes.Where(p => p.Type == type).ToList();
                    Add(report, arm, "type:" + type, "score", ofType, resamples, seed);
                    foreach (string metric in ofType.SelectMany(p => p.Metrics.Keys).Distinct(StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal))
                    {
                        List<double> values = ofType.Where(p => p.Metrics.ContainsKey(metric)).Select(p => p.Metrics[metric]).ToList();
                        report.Summaries.Add(new MetricSummary { Arm = arm, Group = "type:" + type, Metric = metric, Value = Bootstrap.Mean(values, resamples, seed) });
                    }
                }

                foreach (string category in probes.Select(p => p.Category).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal))
                    Add(report, arm, "category:" + category, "score", probes.Where(p => p.Category == category).ToList(), resamples, seed);
            }
        }

        /// <summary>
        /// Find a summary.
        /// </summary>
        /// <param name="report">Report.</param>
        /// <param name="arm">Arm.</param>
        /// <param name="group">Group.</param>
        /// <param name="metric">Metric.</param>
        /// <returns>Summary, or null.</returns>
        public static MetricSummary? Find(BenchmarkReport report, string arm, string group, string metric)
        {
            ArgumentNullException.ThrowIfNull(report);
            return report.Summaries.FirstOrDefault(s => s.Arm == arm && s.Group == group && s.Metric == metric);
        }

        #endregion

        #region Private-Methods

        private static void Add(BenchmarkReport report, string arm, string group, string metric, List<ProbeOutcome> probes, int resamples, int seed)
        {
            if (probes.Count == 0) return;
            report.Summaries.Add(new MetricSummary { Arm = arm, Group = group, Metric = metric, Value = Bootstrap.Mean(probes.Select(p => p.Score).ToList(), resamples, seed) });
        }

        #endregion
    }
}
