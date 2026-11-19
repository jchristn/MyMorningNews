namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Evidence quality: the share of extracted quotes that fail verification (plan section 19).
    /// </summary>
    public class QualityReport
    {
        /// <summary>
        /// Quotes extracted.
        /// </summary>
        public int Quotes { get; set; } = 0;

        /// <summary>
        /// Quotes verified.
        /// </summary>
        public int Verified { get; set; } = 0;

        /// <summary>
        /// Share that failed verification.
        /// </summary>
        public double? FailureRate { get; set; } = null;

        /// <summary>
        /// By extraction model (rate is the verified share).
        /// </summary>
        public List<BreakdownRow> ByModel { get; set; } = new List<BreakdownRow>();

        /// <summary>
        /// By day.
        /// </summary>
        public List<BreakdownRow> ByDay { get; set; } = new List<BreakdownRow>();
    }
}
