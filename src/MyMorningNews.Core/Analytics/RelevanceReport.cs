namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Relevance precision (plan section 20).
    /// </summary>
    public class RelevanceReport
    {
        /// <summary>
        /// Findings shown in the period.
        /// </summary>
        public int Shown { get; set; } = 0;

        /// <summary>
        /// Shown findings with a current rating.
        /// </summary>
        public int Rated { get; set; } = 0;

        /// <summary>
        /// Share rated Relevant or Somewhat.
        /// </summary>
        public double? Precision { get; set; } = null;

        /// <summary>
        /// Share of shown findings that were rated.
        /// </summary>
        public double? Coverage { get; set; } = null;

        /// <summary>
        /// By topic.
        /// </summary>
        public List<BreakdownRow> ByTopic { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// By tier.
        /// </summary>
        public List<BreakdownRow> ByTier { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// By source domain (top 20).
        /// </summary>
        public List<BreakdownRow> ByDomain { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// By dimension.
        /// </summary>
        public List<BreakdownRow> ByDimension { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// By day of the finding.
        /// </summary>
        public List<BreakdownRow> ByDay { get; set; } = new List<BreakdownRow>();
    }
}
