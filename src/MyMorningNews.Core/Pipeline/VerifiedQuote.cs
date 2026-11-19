namespace MyMorningNews.Core.Pipeline
{
    using System;

    /// <summary>
    /// The result of looking for a quote in a source text.
    /// </summary>
    public class VerifiedQuote
    {
        /// <summary>
        /// Whether the quote was found.
        /// </summary>
        public bool Found { get; set; } = false;

        /// <summary>
        /// Start offset in the source text, or -1.
        /// </summary>
        public int Start { get; set; } = -1;

        /// <summary>
        /// End offset (exclusive) in the source text, or -1.
        /// </summary>
        public int End { get; set; } = -1;

        /// <summary>
        /// The quote exactly as it appears in the source.
        /// </summary>
        public string Text { get; set; } = String.Empty;

        /// <summary>
        /// The surrounding paragraph.
        /// </summary>
        public string Context { get; set; } = String.Empty;
    }
}
