namespace MyMorningNews.Benchmark.Client
{
    using System;

    /// <summary>
    /// A stored evidence quote.
    /// </summary>
    public class EvidenceDto
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Source.
        /// </summary>
        public string SourceItemId { get; set; } = String.Empty;

        /// <summary>
        /// Quote as stored (the matched span of the source text).
        /// </summary>
        public string Quote { get; set; } = String.Empty;

        /// <summary>
        /// Whether the quote was verified against the source.
        /// </summary>
        public bool Verified { get; set; } = false;
    }
}
