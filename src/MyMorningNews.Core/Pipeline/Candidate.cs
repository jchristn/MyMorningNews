namespace MyMorningNews.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;

    /// <summary>
    /// A candidate finding moving through Extract, Embed, Dedupe, and Score.
    /// </summary>
    public class Candidate
    {
        /// <summary>
        /// What the extraction model returned.
        /// </summary>
        public ExtractedFinding Extracted { get; set; } = new ExtractedFinding();

        /// <summary>
        /// Source document.
        /// </summary>
        public SourceItem Source { get; set; } = new SourceItem();

        /// <summary>
        /// Verified evidence rows.
        /// </summary>
        public List<Evidence> Evidence { get; set; } = new List<Evidence>();

        /// <summary>
        /// Dimension it belongs to, when matched.
        /// </summary>
        public TopicDimension? Dimension { get; set; } = null;

        /// <summary>
        /// Embedding vector, when embedding succeeded.
        /// </summary>
        public float[]? Vector { get; set; } = null;

        /// <summary>
        /// Duplicate of an earlier finding.
        /// </summary>
        public bool IsDuplicate { get; set; } = false;

        /// <summary>
        /// Earlier finding it duplicates.
        /// </summary>
        public string? DuplicateOfFindingId { get; set; } = null;

        /// <summary>
        /// Earlier finding it updates.
        /// </summary>
        public string? UpdateOfFindingId { get; set; } = null;

        /// <summary>
        /// Highest similarity to recent findings, for novelty.
        /// </summary>
        public double? MaxSimilarity { get; set; } = null;

        /// <summary>
        /// Saved finding, once scored.
        /// </summary>
        public Finding? Saved { get; set; } = null;

        /// <summary>
        /// Text that was embedded (title and summary).
        /// </summary>
        public string EmbeddingText
        {
            get { return Extracted.Title + ". " + Extracted.Summary; }
        }
    }
}
