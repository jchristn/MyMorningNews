namespace MyMorningNews.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Suggestion outcomes (plan section 20).
    /// </summary>
    public class SuggestionReport
    {
        /// <summary>
        /// Suggestions.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Undecided.
        /// </summary>
        public int New { get; set; } = 0;

        /// <summary>
        /// Accepted.
        /// </summary>
        public int Accepted { get; set; } = 0;

        /// <summary>
        /// Done.
        /// </summary>
        public int Done { get; set; } = 0;

        /// <summary>
        /// Dismissed.
        /// </summary>
        public int Dismissed { get; set; } = 0;

        /// <summary>
        /// Snoozed.
        /// </summary>
        public int Snoozed { get; set; } = 0;

        /// <summary>
        /// Mean days from creation to the last status change.
        /// </summary>
        public double? MeanDaysToDecision { get; set; } = null;

        /// <summary>
        /// By priority (rate is done or accepted).
        /// </summary>
        public List<BreakdownRow> ByPriority { get; set; } = new List<BreakdownRow>();
    }
}
