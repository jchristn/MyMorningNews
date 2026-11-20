namespace MyMorningNews.Benchmark.Datasets
{
    using System;

    /// <summary>
    /// Feedback the reader gives on the finding an item produced, after a round finishes.
    /// </summary>
    public class BenchmarkFeedback
    {
        /// <summary>
        /// Round after which the feedback is given. Default 1.
        /// </summary>
        public int AfterRound { get; set; } = 1;

        /// <summary>
        /// Item whose finding is rated.
        /// </summary>
        public string Item { get; set; } = String.Empty;

        /// <summary>
        /// Relevance rating: Relevant, SomewhatRelevant, or NotRelevant. Default Relevant.
        /// </summary>
        public string Relevance { get; set; } = "Relevant";

        /// <summary>
        /// Reason, or null.
        /// </summary>
        public string? Reason { get; set; } = null;
    }
}
