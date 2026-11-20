namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using MyMorningNews.Benchmark.Metrics;

    /// <summary>
    /// A summarized metric for one arm and group (overall, a probe type, or a category), with a bootstrap interval.
    /// </summary>
    public class MetricSummary
    {
        /// <summary>
        /// Arm.
        /// </summary>
        public string Arm { get; set; } = String.Empty;

        /// <summary>
        /// Group: "overall", "type:ranking", "category:dedupe", "negative", and so on.
        /// </summary>
        public string Group { get; set; } = "overall";

        /// <summary>
        /// Metric name (score, ndcg10, and so on).
        /// </summary>
        public string Metric { get; set; } = "score";

        /// <summary>
        /// Value with its interval.
        /// </summary>
        public ConfidenceInterval Value { get; set; } = new ConfidenceInterval();
    }
}
