namespace MyMorningNews.Benchmark.Stub
{
    using System;

    /// <summary>
    /// One quote the stub model returned while extracting from a page, and whether it is really in the page.
    /// </summary>
    public class StubQuoteRecord
    {
        /// <summary>
        /// Ledger tag when returned (the workspace being run).
        /// </summary>
        public string Tag { get; set; } = String.Empty;

        /// <summary>
        /// Corpus.
        /// </summary>
        public string Corpus { get; set; } = String.Empty;

        /// <summary>
        /// Content item the page shows.
        /// </summary>
        public string Item { get; set; } = String.Empty;

        /// <summary>
        /// Topic named in the prompt.
        /// </summary>
        public string Topic { get; set; } = String.Empty;

        /// <summary>
        /// Quote text as returned.
        /// </summary>
        public string Quote { get; set; } = String.Empty;

        /// <summary>
        /// Whether the quote is present in the page (so the product should verify it).
        /// </summary>
        public bool Genuine { get; set; } = true;

        /// <summary>
        /// Quote mode that produced it.
        /// </summary>
        public string Mode { get; set; } = "verbatim";
    }
}
