namespace MyMorningNews.Benchmark.Datasets
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One corpus: a topic, the pages the stub search engine serves for it round by round, the feedback a reader gives
    /// between rounds, and the probes that score the results.
    /// </summary>
    public class BenchmarkCorpus
    {
        /// <summary>
        /// Stable identifier, unique in the dataset (lowercase letters, digits, and dashes).
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Number of research runs (rounds), 1 or more. Items list the rounds in which search returns them.
        /// </summary>
        public int Rounds { get; set; } = 1;

        /// <summary>
        /// The topic created for this corpus.
        /// </summary>
        public BenchmarkTopic Topic { get; set; } = new BenchmarkTopic();

        /// <summary>
        /// Pages.
        /// </summary>
        public List<BenchmarkItem> Items { get; set; } = new List<BenchmarkItem>();

        /// <summary>
        /// Feedback the reader gives after a round.
        /// </summary>
        public List<BenchmarkFeedback> Feedback { get; set; } = new List<BenchmarkFeedback>();

        /// <summary>
        /// Probes.
        /// </summary>
        public List<BenchmarkProbe> Probes { get; set; } = new List<BenchmarkProbe>();
    }
}
