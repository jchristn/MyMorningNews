namespace MyMorningNews.Benchmark.Datasets
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A dataset in the neutral format (benchmarks/datasets/SCHEMA.md): a name, a tier, and corpora. Each corpus is
    /// provisioned as its own workspace (a tenant with one topic per arm).
    /// </summary>
    public class BenchmarkDataset
    {
        /// <summary>
        /// Dataset name, used in report file names. Required.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Tier: live, synthetic, public, or local.
        /// </summary>
        public string Tier { get; set; } = "synthetic";

        /// <summary>
        /// Snapshot or build date (yyyy-MM-dd).
        /// </summary>
        public string Snapshot { get; set; } = String.Empty;

        /// <summary>
        /// Corpora. At least one.
        /// </summary>
        public List<BenchmarkCorpus> Corpora { get; set; } = new List<BenchmarkCorpus>();
    }
}
