namespace MyMorningNews.Benchmark.Datasets
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The topic a corpus is researched under.
    /// </summary>
    public class BenchmarkTopic
    {
        /// <summary>
        /// Topic name. Required.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Brief. Required.
        /// </summary>
        public string Brief { get; set; } = String.Empty;

        /// <summary>
        /// Novelty preference, 0 to 200. Default 50.
        /// </summary>
        public int NoveltyPreference { get; set; } = 50;

        /// <summary>
        /// Seed queries, used by the product when query planning fails.
        /// </summary>
        public List<string> SeedQueries { get; set; } = new List<string>();

        /// <summary>
        /// Dimensions.
        /// </summary>
        public List<BenchmarkDimension> Dimensions { get; set; } = new List<BenchmarkDimension>();

        /// <summary>
        /// Constraints.
        /// </summary>
        public List<BenchmarkConstraint> Constraints { get; set; } = new List<BenchmarkConstraint>();
    }
}
