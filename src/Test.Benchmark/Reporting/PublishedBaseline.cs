namespace MyMorningNews.Benchmark.Reporting
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A published result for a dataset, from benchmarks/baselines.json.
    /// </summary>
    public class PublishedBaseline
    {
        /// <summary>
        /// System or method name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Metric (as the report names it, such as type:ranking/ndcg10).
        /// </summary>
        public string Metric { get; set; } = String.Empty;

        /// <summary>
        /// Value.
        /// </summary>
        public double Value { get; set; } = 0;

        /// <summary>
        /// Source citation.
        /// </summary>
        public string Source { get; set; } = String.Empty;

        /// <summary>
        /// Link.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Arms it should be compared against.
        /// </summary>
        public List<string> Arms { get; set; } = new List<string>();
    }
}
