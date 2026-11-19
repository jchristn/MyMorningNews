namespace MyMorningNews.Core.Briefings
{
    using System;

    /// <summary>
    /// The mmn:section directive of a template section.
    /// </summary>
    public class SectionDirective
    {
        /// <summary>
        /// findings, predictions, suggestions, citations, run-summaries, or none. Default none.
        /// </summary>
        public string Source { get; set; } = "none";

        /// <summary>
        /// Lowest tier for findings (MustRead, WorthALook, Fyi), or null for any displayed tier.
        /// </summary>
        public string? Tier { get; set; } = null;

        /// <summary>
        /// Predictions: new, due, open, resolved. Suggestions: a SuggestionStatusEnum name. Null for the source default.
        /// </summary>
        public string? Status { get; set; } = null;

        /// <summary>
        /// topic, dimension, or category; null for no grouping.
        /// </summary>
        public string? Group { get; set; } = null;

        /// <summary>
        /// Items, or items per group when grouped (1 to 25). Default 5.
        /// </summary>
        public int Limit { get; set; } = 5;

        /// <summary>
        /// relevance, recency, priority, confidence, or horizon; null for the source default.
        /// </summary>
        public string? Order { get; set; } = null;

        /// <summary>
        /// Only this topic (slug or ID), or null.
        /// </summary>
        public string? Topic { get; set; } = null;

        /// <summary>
        /// Only this dimension (name), or null.
        /// </summary>
        public string? Dimension { get; set; } = null;
    }
}
