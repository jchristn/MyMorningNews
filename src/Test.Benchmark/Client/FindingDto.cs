namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// A finding with its scores.
    /// </summary>
    public class FindingDto
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Run.
        /// </summary>
        public string RunId { get; set; } = String.Empty;

        /// <summary>
        /// Source.
        /// </summary>
        public string? SourceItemId { get; set; } = null;

        /// <summary>
        /// Title.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Summary.
        /// </summary>
        public string? Summary { get; set; } = null;

        /// <summary>
        /// Why it matters.
        /// </summary>
        public string? WhyItMatters { get; set; } = null;

        /// <summary>
        /// Composite relevance score.
        /// </summary>
        public double RelevanceScore { get; set; } = 0;

        /// <summary>
        /// Model judgment.
        /// </summary>
        public double LlmScore { get; set; } = 0;

        /// <summary>
        /// Feedback component.
        /// </summary>
        public double FeedbackScore { get; set; } = 0;

        /// <summary>
        /// Novelty component.
        /// </summary>
        public double NoveltyScore { get; set; } = 0;

        /// <summary>
        /// Source component.
        /// </summary>
        public double SourceScore { get; set; } = 0;

        /// <summary>
        /// Recency component.
        /// </summary>
        public double RecencyScore { get; set; } = 0;

        /// <summary>
        /// Tier: MustRead, WorthALook, Fyi, or BelowThreshold.
        /// </summary>
        public string Tier { get; set; } = String.Empty;

        /// <summary>
        /// Whether it repeats an earlier finding.
        /// </summary>
        public bool IsDuplicate { get; set; } = false;

        /// <summary>
        /// The finding it repeats, or null.
        /// </summary>
        public string? DuplicateOfFindingId { get; set; } = null;

        /// <summary>
        /// The finding it updates, or null.
        /// </summary>
        public string? IsUpdateOfFindingId { get; set; } = null;

        /// <summary>
        /// Rank in its run (1 is best; 0 for duplicates).
        /// </summary>
        public int Rank { get; set; } = 0;

        /// <summary>
        /// Whether the finding is shown to the reader (not a duplicate and above the display threshold).
        /// </summary>
        public bool Shown
        {
            get { return !IsDuplicate && Tier != "BelowThreshold"; }
        }
    }
}
