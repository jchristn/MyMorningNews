namespace MyMorningNews.Benchmark.Datasets
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One page. The stub serves it at /corpus/{corpus}/{id}.html unless Url names an outside address (such as a
    /// blocked domain, which the product must never fetch). An alias item has no content of its own: it is the page of
    /// AliasOf under a URL with tracking parameters.
    /// </summary>
    public class BenchmarkItem
    {
        /// <summary>
        /// Stable identifier, unique in the corpus.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Title (the page heading).
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Body paragraphs.
        /// </summary>
        public List<string> Paragraphs { get; set; } = new List<string>();

        /// <summary>
        /// Rounds in which search returns this page. Default round 1.
        /// </summary>
        public List<int> Rounds { get; set; } = new List<int> { 1 };

        /// <summary>
        /// Age at search time in hours (the search result's published date). Default 12.
        /// </summary>
        public double HoursAgo { get; set; } = 12;

        /// <summary>
        /// Graded relevance to the topic, 0 (none) to 3 (must read). Used by ranking probes.
        /// </summary>
        public int Relevance { get; set; } = 0;

        /// <summary>
        /// Outside URL override, or null to serve from the stub.
        /// </summary>
        public string? Url { get; set; } = null;

        /// <summary>
        /// The item this one aliases, or null.
        /// </summary>
        public string? AliasOf { get; set; } = null;

        /// <summary>
        /// Stub model script for the quotes it returns when extracting from this page: verbatim (default), typography,
        /// fabricated, fabricated-only, altered, or short. Real models ignore it.
        /// </summary>
        public string Quotes { get; set; } = "verbatim";
    }
}
