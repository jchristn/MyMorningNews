namespace MyMorningNews.Core.Models
{
    using System;

    /// <summary>
    /// A nearest-neighbor match from the embeddings table.
    /// </summary>
    public class EmbeddingMatch
    {
        /// <summary>
        /// Owner ID (for example a finding ID).
        /// </summary>
        public string OwnerId { get; set; } = String.Empty;

        /// <summary>
        /// Cosine similarity, -1 to 1 (1 is identical).
        /// </summary>
        public double Similarity { get; set; } = 0;
    }
}
